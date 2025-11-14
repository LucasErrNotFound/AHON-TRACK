using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface;

public interface ISmsService
{
    Task<(bool Success, string Message)> SendSmsAsync(string toPhoneNumber, string message);
    string FormatPhilippineNumber(string phoneNumber);
    string GenerateNearExpiryMessage(string memberName, string validUntil, int daysRemaining);
    string GenerateExpiredMessage(string memberName, string validUntil);
}