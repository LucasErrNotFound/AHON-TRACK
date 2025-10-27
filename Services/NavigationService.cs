using AHON_TRACK.Services.Interface;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AHON_TRACK.Services;

public sealed class NavigationService : INavigationService, IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private INavigable? _currentPage;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    
    public INavigable? CurrentPage => _currentPage;
    public string CurrentRoute { get; private set; } = "dashboard";
    
    public event Action<INavigable, string>? NavigationCompleted;

    public NavigationService(ServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async ValueTask NavigateAsync<T>(
        Dictionary<string, object>? parameters = null, 
        CancellationToken cancellationToken = default) 
        where T : INavigable
    {
        var attr = typeof(T).GetCustomAttribute<PageAttribute>();
        if (attr is null)
        {
            _logger.LogError("Type {Type} is missing PageAttribute", typeof(T).Name);
            throw new InvalidOperationException($"Not a valid page type: {typeof(T).Name}");
        }

        var page = _serviceProvider.GetService<T>();
        if (page is null)
        {
            _logger.LogError("Failed to resolve page {Type}", typeof(T).Name);
            throw new InvalidOperationException($"Page not found: {typeof(T).Name}");
        }

        // Set parameters before navigation
        if (page is INavigableWithParameters navigableWithParams && parameters != null)
        {
            navigableWithParams.SetNavigationParameters(parameters);
        }

        await NavigateAsync(page, attr.Route, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask NavigateAsync(
        INavigable page, 
        string route, 
        CancellationToken cancellationToken = default)
    {
        await _navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Don't navigate to same page
            if (ReferenceEquals(_currentPage, page))
            {
                _logger.LogDebug("Already on page {Route}", route);
                return;
            }

            // Cleanup old page
            if (_currentPage != null)
            {
                _logger.LogDebug("Disposing previous page: {Route}", CurrentRoute);
                
                try
                {
                    await _currentPage.OnNavigatingFromAsync(cancellationToken).ConfigureAwait(false);
                    await _currentPage.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing page {Route}", CurrentRoute);
                }
            }

            // Set new page
            _currentPage = page;
            CurrentRoute = route;

            // Initialize new page
            await page.InitializeAsync(cancellationToken).ConfigureAwait(false);
            
            _logger.LogInformation("Navigated to {Route}", route);
            NavigationCompleted?.Invoke(page, route);
        }
        finally
        {
            _navigationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentPage != null)
        {
            try
            {
                await _currentPage.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing current page during navigation service cleanup");
            }
            _currentPage = null;
        }

        _navigationLock?.Dispose();
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PageAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}