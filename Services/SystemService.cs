using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class SystemService : ISystemService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public SystemService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        // Package Service Methods

        // Simple method with minimal parameters (2 parameters)
        public async Task AddPackageAsync(string packageName, decimal price)
        {
            const string query = @"
                INSERT INTO Package (packageName, price, description, duration, features1, features2, features3, features4, features5, discount, discountType, discountedPrice, validFrom, validTo)
                VALUES (@packageName, @price, @description, @duration, @features1, @features2, @features3, @features4, @features5, @discount, @discountType, @discountedPrice, @validFrom, @validTo)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        // Required parameters
                        command.Parameters.AddWithValue("@packageName", packageName);
                        command.Parameters.AddWithValue("@price", price);

                        // Optional/default parameters
                        command.Parameters.AddWithValue("@description", DBNull.Value);
                        command.Parameters.AddWithValue("@duration", 30); // Default 30 days
                        command.Parameters.AddWithValue("@features1", DBNull.Value);
                        command.Parameters.AddWithValue("@features2", DBNull.Value);
                        command.Parameters.AddWithValue("@features3", DBNull.Value);
                        command.Parameters.AddWithValue("@features4", DBNull.Value);
                        command.Parameters.AddWithValue("@features5", DBNull.Value);
                        command.Parameters.AddWithValue("@discount", 0);
                        command.Parameters.AddWithValue("@discountType", DBNull.Value);
                        command.Parameters.AddWithValue("@discountedPrice", price); // Same as price if no discount
                        command.Parameters.AddWithValue("@validFrom", DateTime.Now);
                        command.Parameters.AddWithValue("@validTo", DateTime.Now.AddDays(365)); // Valid for 1 year

                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Show success toast
                _toastManager.CreateToast($"Package '{packageName}' added successfully!");
            }
            catch (SqlException ex)
            {
                // Handle SQL-specific errors
                _toastManager.CreateToast($"Database error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // Handle general errors
                _toastManager.CreateToast($"Error adding package: {ex.Message}");
                throw;
            }
        }

        // Full method with all parameters (13 parameters)
        public async Task AddPackageAsync(string packageName, decimal price, string description,
            int duration, string features1, string features2, string features3,
            string features4, string features5, decimal discount, string discountType,
            DateTime validFrom, DateTime validTo)
        {
            const string query = @"
                INSERT INTO Package (packageName, price, description, duration, features1, features2, features3, features4, features5, discount, discountType, discountedPrice, validFrom, validTo)
                VALUES (@packageName, @price, @description, @duration, @features1, @features2, @features3, @features4, @features5, @discount, @discountType, @discountedPrice, @validFrom, @validTo)";

            try
            {
                // Calculate discounted price
                decimal discountedPrice = price;
                if (discount > 0)
                {
                    if (discountType?.ToLower() == "percentage")
                    {
                        discountedPrice = price - (price * discount / 100);
                    }
                    else if (discountType?.ToLower() == "fixed")
                    {
                        discountedPrice = price - discount;
                        // Make sure discounted price doesn't go below 0
                        if (discountedPrice < 0) discountedPrice = 0;
                    }
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@packageName", packageName);
                        command.Parameters.AddWithValue("@price", price);
                        command.Parameters.AddWithValue("@description", (object)description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@duration", duration);
                        command.Parameters.AddWithValue("@features1", (object)features1 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features2", (object)features2 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features3", (object)features3 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features4", (object)features4 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features5", (object)features5 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discount", discount);
                        command.Parameters.AddWithValue("@discountType", (object)discountType ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discountedPrice", discountedPrice);
                        command.Parameters.AddWithValue("@validFrom", validFrom);
                        command.Parameters.AddWithValue("@validTo", validTo);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                _toastManager.CreateToast($"Package '{packageName}' added successfully!");
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast($"Database error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast($"Error adding package: {ex.Message}");
                throw;
            }
        }

        // Method to add package using a model (1 parameter)
        public async Task AddPackageAsync(PackageModel package)
        {
            await AddPackageAsync(
                package.packageName,
                package.price,
                package.description,
                package.duration,
                package.features1,
                package.features2,
                package.features3,
                package.features4,
                package.features5,
                package.discount,
                package.discountType,
                package.validFrom,
                package.validTo
            );
        }

        // Other service methods can be added here


    }
}
