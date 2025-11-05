using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Helpers
{
    /// <summary>
    /// Helper class for password hashing and verification using BCrypt
    /// </summary>
    public static class PasswordHelper
    {
        /// <summary>
        /// Hashes a plain text password using BCrypt
        /// </summary>
        /// <param name="password">The plain text password to hash</param>
        /// <returns>The hashed password</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }

            // Generate a salt and hash the password
            // WorkFactor 12 is a good balance between security and performance
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        /// <summary>
        /// Verifies if a plain text password matches a hashed password
        /// </summary>
        /// <param name="password">The plain text password to verify</param>
        /// <param name="hashedPassword">The hashed password to compare against</param>
        /// <returns>True if the password matches, false otherwise</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(hashedPassword))
            {
                return false;
            }

            try
            {
                // Verify the password against the hash
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch (Exception)
            {
                // If there's any error during verification, return false
                return false;
            }
        }

        /// <summary>
        /// Checks if a password needs to be rehashed (e.g., if security standards have changed)
        /// </summary>
        /// <param name="hashedPassword">The hashed password to check</param>
        /// <param name="newWorkFactor">The new work factor to use (default: 12)</param>
        /// <returns>True if the password needs rehashing, false otherwise</returns>
        public static bool NeedsRehash(string hashedPassword, int newWorkFactor = 12)
        {
            if (string.IsNullOrWhiteSpace(hashedPassword))
            {
                return true;
            }

            try
            {
                // Check if the hash needs to be upgraded
                return BCrypt.Net.BCrypt.PasswordNeedsRehash(hashedPassword, newWorkFactor);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Generates a random password of specified length
        /// </summary>
        /// <param name="length">The length of the password (minimum 8)</param>
        /// <returns>A randomly generated password</returns>
        public static string GenerateRandomPassword(int length = 12)
        {
            if (length < 8)
            {
                throw new ArgumentException("Password length must be at least 8 characters", nameof(length));
            }

            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string special = "!@#$%^&*()_-+=";
            const string allChars = lowercase + uppercase + digits + special;

            var random = new Random();
            var password = new char[length];

            // Ensure at least one of each character type
            password[0] = lowercase[random.Next(lowercase.Length)];
            password[1] = uppercase[random.Next(uppercase.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = special[random.Next(special.Length)];

            // Fill the rest randomly
            for (int i = 4; i < length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Shuffle the password
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = password[i];
                password[i] = password[j];
                password[j] = temp;
            }

            return new string(password);
        }
    }
}