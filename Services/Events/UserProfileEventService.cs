using System;

namespace AHON_TRACK.Services.Events;

public class UserProfileEventService
{
    private static UserProfileEventService? _instance;
    public static UserProfileEventService Instance => _instance ??= new UserProfileEventService();

    public event Action? ProfilePictureUpdated;

    public void NotifyProfilePictureUpdated()
    {
        ProfilePictureUpdated?.Invoke();
    }
}