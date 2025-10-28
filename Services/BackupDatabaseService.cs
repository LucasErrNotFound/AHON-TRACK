using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace AHON_TRACK.Services
{
    public class BackupDatabaseService
    {
        private readonly string _connectionString;

        public BackupDatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task BackupDatabaseAsync(string filePath)
        {
            try
            {
                // Normalize the path
                filePath = Path.GetFullPath(filePath);

                // Ensure the directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Verify we have write permissions by attempting to create a test file
                var testFile = Path.Combine(directory!, ".write_test");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch (UnauthorizedAccessException)
                {
                    throw new InvalidOperationException($"No write permission to directory: {directory}");
                }

                // Use proper T-SQL syntax with escaped path
                // NOTE: COMPRESSION is not supported in SQL Server Express Edition
                var sql = @"
                    BACKUP DATABASE [AHON_TRACK] 
                    TO DISK = @backupPath
                    WITH INIT, STATS = 10, FORMAT;
                ";

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@backupPath", filePath);
                command.CommandTimeout = 300; // 5 minutes timeout

                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException sqlEx)
            {
                // Provide more detailed error information
                throw new InvalidOperationException(
                    $"SQL Server backup failed: {sqlEx.Message}\n" +
                    $"Error Number: {sqlEx.Number}\n" +
                    $"Path: {filePath}\n" +
                    $"Ensure SQL Server service account has write access to this location.",
                    sqlEx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to backup database: {ex.Message}", ex);
            }
        }

        public async Task RestoreDatabaseAsync(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                {
                    throw new FileNotFoundException("Backup file not found", backupFilePath);
                }

                backupFilePath = Path.GetFullPath(backupFilePath);

                // First, get logical file names from the backup
                var fileListSql = "RESTORE FILELISTONLY FROM DISK = @backupPath";

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Close all existing connections to the database
                var killConnectionsSql = @"
                    USE master;
                    DECLARE @kill varchar(8000) = '';
                    SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(5), session_id) + ';'
                    FROM sys.dm_exec_sessions
                    WHERE database_id = DB_ID('AHON_TRACK') AND session_id <> @@SPID;
                    EXEC(@kill);
                ";

                await using (var killCommand = new SqlCommand(killConnectionsSql, connection))
                {
                    killCommand.CommandTimeout = 60;
                    await killCommand.ExecuteNonQueryAsync();
                }

                // Perform the restore
                var restoreSql = @"
                    USE master;
                    ALTER DATABASE [AHON_TRACK] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    
                    RESTORE DATABASE [AHON_TRACK] 
                    FROM DISK = @backupPath
                    WITH REPLACE, STATS = 10;
                    
                    ALTER DATABASE [AHON_TRACK] SET MULTI_USER;
                ";

                await using var command = new SqlCommand(restoreSql, connection);
                command.Parameters.AddWithValue("@backupPath", backupFilePath);
                command.CommandTimeout = 600; // 10 minutes timeout

                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException sqlEx)
            {
                throw new InvalidOperationException(
                    $"SQL Server restore failed: {sqlEx.Message}\n" +
                    $"Error Number: {sqlEx.Number}\n" +
                    $"Backup file: {backupFilePath}",
                    sqlEx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to restore database: {ex.Message}", ex);
            }
        }
    }
}