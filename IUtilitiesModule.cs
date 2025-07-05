using Jab;
using ShadUI;

namespace AHON_TRACK;

/// <summary>
/// This module defines shared utility services that should be registered
/// across the application. DialogManager is singleton (shared), but ToastManager
/// is transient (each window gets its own instance).
/// 
/// Note: This module is currently not imported in ServiceProvider to avoid
/// duplicate registrations. The registrations are done directly in ServiceProvider.
/// </summary>
[ServiceProviderModule]
[Singleton<DialogManager>]  // Registers DialogManager as a singleton
[Transient<ToastManager>]   // Registers ToastManager as transient (each window gets its own)
public interface IUtilitiesModule
{
    // This interface is used by Jab for code generation
    // No implementation needed - Jab generates the registration code
}