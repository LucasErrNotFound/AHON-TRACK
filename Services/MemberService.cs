using AHON_TRACK.Models;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class MemberService : IMemberService
    {
        private readonly string _connectionString;

        public MemberService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<MemberModel>> GetMemberAsync()
        {
            var members = new List<MemberModel>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT MemberId, 
                               Name, 
                               ContactNumber, 
                               MembershipType, 
                               Status, 
                               Validity,
                               AvatarSource
                        FROM Members";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            members.Add(new MemberModel
                            {
                                ID = reader.GetInt32(reader.GetOrdinal("MemberId")),
                                Name = reader["Name"].ToString() ?? string.Empty,
                                ContactNumber = reader["ContactNumber"].ToString() ?? string.Empty,
                                MembershipType = reader["MembershipType"].ToString() ?? string.Empty,
                                Status = reader["Status"].ToString() ?? string.Empty,
                                Validity = reader["Validity"].ToString() ?? string.Empty,
                                AvatarSource = reader["AvatarSource"] == DBNull.Value
                                    ? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
                                    : reader["AvatarSource"]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO: handle/log error properly
                Console.WriteLine($"[GetMemberAsync] Error: {ex.Message}");
            }

            return members;
        }
    }
}
