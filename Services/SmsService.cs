using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AHON_TRACK.Services.Interface;

namespace AHON_TRACK.Services;

public class SmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _fromPhoneNumber;

    public SmsService(string apiKey, string fromPhoneNumber)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _fromPhoneNumber = fromPhoneNumber ?? throw new ArgumentNullException(nameof(fromPhoneNumber));
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    /// <summary>
    /// Sends an SMS message using HttpSMS API
    /// </summary>
    public async Task<(bool Success, string Message)> SendSmsAsync(string toPhoneNumber, string message)
    {
        try
        {
            // Format the phone number to international format
            string formattedNumber = FormatPhilippineNumber(toPhoneNumber);
            
            if (string.IsNullOrEmpty(formattedNumber))
            {
                return (false, "Invalid phone number format.");
            }

            Debug.WriteLine($"[SendSmsAsync] Sending SMS to {formattedNumber}");
            Debug.WriteLine($"[SendSmsAsync] Message: {message}");

            var payload = new
            {
                from = _fromPhoneNumber,
                to = formattedNumber,
                content = message
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.httpsms.com/v1/messages/send",
                httpContent
            );

            var responseContent = await response.Content.ReadAsStringAsync();
            
            Debug.WriteLine($"[SendSmsAsync] Response Status: {response.StatusCode}");
            Debug.WriteLine($"[SendSmsAsync] Response Content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                return (true, "SMS sent successfully.");
            }

            return (false, $"Failed to send SMS. Status: {response.StatusCode}, Response: {responseContent}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[SendSmsAsync] HTTP Error: {ex.Message}");
            return (false, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SendSmsAsync] Error: {ex.Message}");
            return (false, $"Error sending SMS: {ex.Message}");
        }
    }

    /// <summary>
    /// Formats Philippine phone numbers to international format (+63XXXXXXXXXX)
    /// Handles formats: 09XXXXXXXXX, 9XXXXXXXXX, +639XXXXXXXXX, 639XXXXXXXXX
    /// </summary>
    public string FormatPhilippineNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        // Remove all non-digit characters except leading +
        string cleaned = Regex.Replace(phoneNumber, @"[^\d+]", "");

        // Remove leading + temporarily for processing
        cleaned = cleaned.TrimStart('+');

        // Handle different formats
        if (cleaned.StartsWith("639") && cleaned.Length == 12)
        {
            // Already in format 639XXXXXXXXX
            return $"+{cleaned}";
        }
        else if (cleaned.StartsWith("09") && cleaned.Length == 11)
        {
            // Format: 09XXXXXXXXX -> +639XXXXXXXXX
            return $"+63{cleaned.Substring(1)}";
        }
        else if (cleaned.StartsWith("9") && cleaned.Length == 10)
        {
            // Format: 9XXXXXXXXX -> +639XXXXXXXXX
            return $"+63{cleaned}";
        }
        else if (cleaned.Length == 10)
        {
            // Assume it's missing the leading 9
            return $"+639{cleaned}";
        }

        Debug.WriteLine($"[FormatPhilippineNumber] Invalid phone number format: {phoneNumber}");
        return string.Empty;
    }

    /// <summary>
    /// Generates a notification message for members with near expiry status
    /// </summary>
    public string GenerateNearExpiryMessage(string memberName, string validUntil, int daysRemaining)
    {
        Debug.WriteLine($"[GenerateNearExpiryMessage] Member: {memberName}, Days: {daysRemaining}, ValidUntil: {validUntil}");
    
        string urgencyText = daysRemaining switch
        {
            0 => "URGENT: Your membership expires TODAY",
            1 => "URGENT: Your membership expires TOMORROW",
            2 => "IMPORTANT: Your membership expires in 2 days",
            3 => "IMPORTANT: Your membership expires in 3 days",
            <= 5 => $"REMINDER: Your membership expires in {daysRemaining} days",
            <= 7 => $"Friendly reminder: Your membership expires in {daysRemaining} days",
            _ => $"Notice: Your membership expires in {daysRemaining} days"
        };

        return $@"Hi {memberName},

{urgencyText} ({validUntil}).

To continue enjoying our gym facilities without interruption, please renew your membership at your earliest convenience.

Visit us today or contact our staff for renewal assistance.

Thank you for being a valued member!

- AHON Victory Gym Management";
    }

    /// <summary>
    /// Generates a notification message for members with expired status
    /// </summary>
    public string GenerateExpiredMessage(string memberName, string validUntil)
    {
        return $@"Hi {memberName},

Your gym membership has expired as of {validUntil}.

We miss seeing you at the gym! To resume your workouts and regain access to our facilities, please renew your membership.

Our team is ready to assist you with the renewal process. Visit us or give us a call.

We look forward to having you back!

- AHON Victory Gym Management";
    }
}