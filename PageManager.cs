using System;
using System.Collections.Generic;
using System.Reflection;

namespace AHON_TRACK;

public sealed class PageManager(ServiceProvider serviceProvider)
{
    private INavigable? _currentPage;

    public void Navigate<T>() where T : INavigable
    {
        Navigate<T>(null);
    }

    public void Navigate<T>(Dictionary<string, object>? parameters) where T : INavigable
    {
        var attr = typeof(T).GetCustomAttribute<PageAttribute>();
        if (attr is null) throw new InvalidOperationException("Not a valid page type, missing PageAttribute");

        // ✅ Dispose old page if it implements IDisposable
        if (_currentPage is IDisposable disposable)
        {
            disposable.Dispose();
        }

        var page = serviceProvider.GetService<T>();
        if (page is null) throw new InvalidOperationException("Page not found");

        // Pass parameters if the page supports them
        if (page is INavigableWithParameters navigableWithParams && parameters != null)
        {
            navigableWithParams.SetNavigationParameters(parameters);
        }

        _currentPage = page;
        OnNavigate?.Invoke(page, attr.Route);
    }

    private Action<INavigable, string>? _onNavigate;

    public Action<INavigable, string>? OnNavigate
    {
        private get => _onNavigate;
        set
        {
            if (_onNavigate is not null)
            {
                throw new InvalidOperationException("OnNavigate is already set");
            }

            _onNavigate = value;
        }
    }
}

public interface INavigable
{
    void Initialize()
    {
    }
}

public interface INavigableWithParameters : INavigable
{
    void SetNavigationParameters(Dictionary<string, object> parameters);
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PageAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}
