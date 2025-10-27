using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface;

/// <summary>
/// Navigation service with proper disposal lifecycle.
/// Ensures previous ViewModels are disposed when navigating away.
/// </summary>
public interface INavigationService
{
    /// <summary>Current page being displayed</summary>
    INavigable? CurrentPage { get; }
    
    /// <summary>Current route</summary>
    string CurrentRoute { get; }
    
    /// <summary>Navigation event - fired after navigation completes</summary>
    event Action<INavigable, string>? NavigationCompleted;
    
    /// <summary>Navigate to a page with optional parameters and cancellation support</summary>
    ValueTask NavigateAsync<T>(
        Dictionary<string, object>? parameters = null, 
        CancellationToken cancellationToken = default) 
        where T : INavigable;
    
    /// <summary>Navigate to a specific page instance</summary>
    ValueTask NavigateAsync(
        INavigable page, 
        string route, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for all navigable pages.
/// Implements IAsyncDisposable for proper cleanup.
/// </summary>
public interface INavigable : IAsyncDisposable
{
    /// <summary>Initialize the page - called after navigation</summary>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>Cleanup before navigation away - called before disposal</summary>
    ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// For pages that accept navigation parameters
/// </summary>
public interface INavigableWithParameters : INavigable
{
    void SetNavigationParameters(Dictionary<string, object> parameters);
}