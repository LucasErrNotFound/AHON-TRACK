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

        #region Database Backup Operations

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

        #endregion

        #region Index Maintenance Operations

        public async Task PerformIndexMaintenanceAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("===== STARTING INDEX MAINTENANCE =====");

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // SQL script for comprehensive index maintenance
                var maintenanceSql = @"
                    USE AHON_TRACK;
                    
                    -- Create maintenance log table if it doesn't exist
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'IndexMaintenanceLog')
                    BEGIN
                        CREATE TABLE dbo.IndexMaintenanceLog (
                            LogID INT IDENTITY(1,1) PRIMARY KEY,
                            TableName NVARCHAR(255) NOT NULL,
                            IndexName NVARCHAR(255) NOT NULL,
                            FragmentationPercent DECIMAL(10,2) NOT NULL,
                            PageCount BIGINT NULL,
                            ActionTaken NVARCHAR(50) NOT NULL,
                            MaintenanceDate DATETIME DEFAULT GETDATE(),
                            DurationSeconds INT NULL
                        );
                    END

                    -- Temporary table to store fragmentation
                    IF OBJECT_ID('tempdb..#frag') IS NOT NULL DROP TABLE #frag;
                    CREATE TABLE #frag (
                        TableName NVARCHAR(255),
                        IndexName NVARCHAR(255),
                        Frag DECIMAL(10,2),
                        PageCount BIGINT
                    );

                    -- Collect fragmentation info
                    INSERT INTO #frag (TableName, IndexName, Frag, PageCount)
                    SELECT 
                        t.name AS TableName,
                        i.name AS IndexName,
                        ps.avg_fragmentation_in_percent AS Frag,
                        ps.page_count AS PageCount
                    FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
                    INNER JOIN sys.indexes i ON i.object_id = ps.object_id AND i.index_id = ps.index_id
                    INNER JOIN sys.tables t ON t.object_id = ps.object_id
                    WHERE i.name IS NOT NULL
                        AND ps.avg_fragmentation_in_percent > 0
                        AND ps.page_count > 100
                        AND t.is_ms_shipped = 0
                    ORDER BY ps.avg_fragmentation_in_percent DESC;

                    -- Declare variables for cursor
                    DECLARE @TableName NVARCHAR(255);
                    DECLARE @IndexName NVARCHAR(255);
                    DECLARE @SQL NVARCHAR(MAX);
                    DECLARE @Frag DECIMAL(10,2);
                    DECLARE @PageCount BIGINT;
                    DECLARE @RebuildCount INT = 0;
                    DECLARE @ReorganizeCount INT = 0;
                    DECLARE @IndexStartTime DATETIME;
                    DECLARE @IndexDuration INT;

                    -- Cursor through fragmented indexes
                    DECLARE frag_cursor CURSOR FAST_FORWARD FOR
                    SELECT TableName, IndexName, Frag, PageCount FROM #frag;

                    OPEN frag_cursor;
                    FETCH NEXT FROM frag_cursor INTO @TableName, @IndexName, @Frag, @PageCount;

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        SET @IndexStartTime = GETDATE();
                        
                        BEGIN TRY
                            IF @Frag >= 30
                            BEGIN
                                SET @SQL = 'ALTER INDEX [' + @IndexName + '] ON [dbo].[' + @TableName + '] REBUILD WITH (ONLINE = OFF, SORT_IN_TEMPDB = ON);';
                                EXEC sp_executesql @SQL;
                                
                                SET @IndexDuration = DATEDIFF(SECOND, @IndexStartTime, GETDATE());
                                
                                INSERT INTO dbo.IndexMaintenanceLog (TableName, IndexName, FragmentationPercent, PageCount, ActionTaken, DurationSeconds)
                                VALUES (@TableName, @IndexName, @Frag, @PageCount, 'REBUILD', @IndexDuration);
                                
                                SET @RebuildCount = @RebuildCount + 1;
                            END
                            ELSE IF @Frag BETWEEN 10 AND 30
                            BEGIN
                                SET @SQL = 'ALTER INDEX [' + @IndexName + '] ON [dbo].[' + @TableName + '] REORGANIZE;';
                                EXEC sp_executesql @SQL;
                                
                                SET @IndexDuration = DATEDIFF(SECOND, @IndexStartTime, GETDATE());
                                
                                INSERT INTO dbo.IndexMaintenanceLog (TableName, IndexName, FragmentationPercent, PageCount, ActionTaken, DurationSeconds)
                                VALUES (@TableName, @IndexName, @Frag, @PageCount, 'REORGANIZE', @IndexDuration);
                                
                                SET @ReorganizeCount = @ReorganizeCount + 1;
                            END
                            ELSE
                            BEGIN
                                INSERT INTO dbo.IndexMaintenanceLog (TableName, IndexName, FragmentationPercent, PageCount, ActionTaken, DurationSeconds)
                                VALUES (@TableName, @IndexName, @Frag, @PageCount, 'SKIP', 0);
                            END
                        END TRY
                        BEGIN CATCH
                            -- Log the error but continue with other indexes
                            DECLARE @ErrorMsg NVARCHAR(MAX) = ERROR_MESSAGE();
                            INSERT INTO dbo.IndexMaintenanceLog (TableName, IndexName, FragmentationPercent, PageCount, ActionTaken)
                            VALUES (@TableName, @IndexName, @Frag, @PageCount, 'ERROR: ' + LEFT(@ErrorMsg, 200));
                        END CATCH

                        FETCH NEXT FROM frag_cursor INTO @TableName, @IndexName, @Frag, @PageCount;
                    END

                    CLOSE frag_cursor;
                    DEALLOCATE frag_cursor;

                    -- Update statistics for better query plans
                    EXEC sp_updatestats;

                    -- Return summary
                    SELECT @RebuildCount AS RebuildCount, @ReorganizeCount AS ReorganizeCount;

                    -- Cleanup
                    DROP TABLE #frag;
                ";

                await using var command = new SqlCommand(maintenanceSql, connection);
                command.CommandTimeout = 1800; // 30 minutes timeout for large databases

                await using var reader = await command.ExecuteReaderAsync();

                int rebuiltCount = 0;
                int reorganizedCount = 0;

                if (await reader.ReadAsync())
                {
                    rebuiltCount = reader.GetInt32(0);
                    reorganizedCount = reader.GetInt32(1);
                }

                System.Diagnostics.Debug.WriteLine($"Index Maintenance Completed:");
                System.Diagnostics.Debug.WriteLine($"  - Rebuilt: {rebuiltCount} indexes");
                System.Diagnostics.Debug.WriteLine($"  - Reorganized: {reorganizedCount} indexes");
                System.Diagnostics.Debug.WriteLine("===== INDEX MAINTENANCE FINISHED =====");
            }
            catch (SqlException sqlEx)
            {
                throw new InvalidOperationException(
                    $"Index maintenance failed: {sqlEx.Message}\n" +
                    $"Error Number: {sqlEx.Number}",
                    sqlEx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to perform index maintenance: {ex.Message}",
                    ex);
            }
        }

        public async Task<IndexMaintenanceStats> GetMaintenanceStatsAsync()
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'IndexMaintenanceLog')
                    BEGIN
                        SELECT 
                            COUNT(*) AS TotalMaintenance,
                            SUM(CASE WHEN ActionTaken = 'REBUILD' THEN 1 ELSE 0 END) AS TotalRebuilds,
                            SUM(CASE WHEN ActionTaken = 'REORGANIZE' THEN 1 ELSE 0 END) AS TotalReorganizes,
                            MAX(MaintenanceDate) AS LastMaintenanceDate
                        FROM dbo.IndexMaintenanceLog;
                    END
                    ELSE
                    BEGIN
                        SELECT 0 AS TotalMaintenance, 0 AS TotalRebuilds, 0 AS TotalReorganizes, NULL AS LastMaintenanceDate;
                    END
                ";

                await using var command = new SqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new IndexMaintenanceStats
                    {
                        TotalMaintenance = reader.GetInt32(0),
                        TotalRebuilds = reader.GetInt32(1),
                        TotalReorganizes = reader.GetInt32(2),
                        LastMaintenanceDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
                    };
                }

                return new IndexMaintenanceStats();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get maintenance stats: {ex.Message}");
                return new IndexMaintenanceStats();
            }
        }

        #endregion
    }

    #region Supporting Classes

    public class IndexMaintenanceStats
    {
        public int TotalMaintenance { get; set; }
        public int TotalRebuilds { get; set; }
        public int TotalReorganizes { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
    }

    #endregion
}