using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Converters
{
    public static class ImageHelper
    {
        public static Bitmap GetDefaultAvatar()
        {
            var uri = new Uri("avares://AHON_TRACK/Assets/MainWindowView/user.png");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }

        public static Bitmap? GetDefaultAvatarSafe()
        {
            try
            {
                return GetDefaultAvatar();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading default avatar: {ex.Message}");
                return null;
            }
        }

        // Bitmap to byte array conversion for database storage
        public static byte[]? BitmapToBytes(Bitmap? bitmap)
        {
            if (bitmap == null)
            {
                System.Diagnostics.Debug.WriteLine("BitmapToBytes: Input bitmap is null");
                return null;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"BitmapToBytes: Converting bitmap {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");

                using (var ms = new MemoryStream())
                {
                    // Avalonia saves as PNG by default
                    bitmap.Save(ms);
                    var result = ms.ToArray();

                    System.Diagnostics.Debug.WriteLine($"BitmapToBytes: Success! Generated {result.Length} bytes");
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BitmapToBytes: Error converting bitmap to bytes: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"BitmapToBytes: Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // Byte array to Bitmap conversion for UI display
        public static Bitmap BytesToBitmap(byte[]? bytes)
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }

        // Base64 string to Bitmap conversion
        public static Bitmap? Base64ToBitmap(string? base64String)
        {
            if (string.IsNullOrWhiteSpace(base64String)) return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(base64String);
                return BytesToBitmap(bytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error converting base64 to bitmap: {ex.Message}");
                return null;
            }
        }

        // Bitmap to Base64 string conversion
        public static string BitmapToBase64(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static string BytesToBase64(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        // File path to byte array conversion for database storage
        public static async Task<byte[]?> FilePathToBytesAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                return await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading file to bytes: {ex.Message}");
                return null;
            }
        }

        // File path to Bitmap conversion for UI display
        public static Bitmap? FilePathToBitmap(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                return new Bitmap(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading bitmap from file: {ex.Message}");
                return null;
            }
        }

        // Validate if the file is a valid image format
        public static bool IsValidImageFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return validExtensions.Contains(extension);
        }

        // Get file size in a human-readable format
        public static string GetFileSizeString(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return "Unknown";

            try
            {
                var fileInfo = new FileInfo(filePath);
                var bytes = fileInfo.Length;

                string[] sizes = { "B", "KB", "MB", "GB" };
                int order = 0;
                double len = bytes;

                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }

                return $"{len:0.##} {sizes[order]}";
            }
            catch
            {
                return "Unknown";
            }
        }

        // Comprehensive method to get avatar with fallback to default
        public static Bitmap GetAvatarOrDefault(object? profilePicture)
        {
            Bitmap? avatar = null;

            // Handle different types of profile picture data
            if (profilePicture != null)
            {
                switch (profilePicture)
                {
                    case Bitmap bitmap:
                        avatar = bitmap;
                        break;
                    case byte[] bytes:
                        avatar = BytesToBitmap(bytes);
                        break;
                    case string base64String:
                        avatar = Base64ToBitmap(base64String);
                        break;
                }
            }

            // Return avatar if valid, otherwise return default
            return avatar ?? GetDefaultAvatarSafe() ?? CreateFallbackBitmap();
        }

        // Create a simple fallback bitmap if default avatar fails to load
        private static Bitmap CreateFallbackBitmap()
        {
            try
            {
                // Create a simple 100x100 colored bitmap as absolute fallback
                using var ms = new MemoryStream();

                // Create a minimal PNG manually (this is a very basic approach)
                // In practice, you might want to create a simple colored rectangle
                // For now, return a 1x1 transparent bitmap
                var fallbackData = new byte[] {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                    0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                    0x42, 0x60, 0x82
                };

                ms.Write(fallbackData);
                ms.Position = 0;
                return new Bitmap(ms);
            }
            catch
            {
                // If all else fails, this will throw, but at least we tried
                throw new InvalidOperationException("Could not create any fallback bitmap");
            }
        }

        // Utility method for resizing bitmaps (useful for profile pictures)
        public static Bitmap? ResizeBitmap(Bitmap? source, int width, int height)
        {
            if (source == null) return null;

            try
            {
                return source.CreateScaledBitmap(new Avalonia.PixelSize(width, height));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resizing bitmap: {ex.Message}");
                return source; // Return original if resize fails
            }
        }

        // Method to handle image selection workflow for your ViewModels
        public static async Task<(Bitmap? uiBitmap, byte[]? dbBytes, bool success)> ProcessImageSelectionAsync(string? filePath)
        {
            if (!IsValidImageFile(filePath))
            {
                return (null, null, false);
            }

            try
            {
                // Load for UI
                var uiBitmap = FilePathToBitmap(filePath);

                // Convert for database
                var dbBytes = await FilePathToBytesAsync(filePath);

                var success = uiBitmap != null && dbBytes != null;
                return (uiBitmap, dbBytes, success);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing image selection: {ex.Message}");
                return (null, null, false);
            }
        }
    }
}
