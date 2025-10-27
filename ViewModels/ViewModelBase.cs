using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

public abstract class ViewModelBase : ObservableValidator, INotifyDataErrorInfo, IAsyncDisposable
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public new bool HasErrors => _errors.Count != 0;

    public new event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return Array.Empty<string>();
        }

        return _errors.TryGetValue(propertyName, out var errors)
            ? errors
            : Array.Empty<string>();
    }

    protected new void SetProperty<T>(ref T field, T value, bool validate = false,
        [CallerMemberName] string propertyName = null!)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;

        field = value;
        OnPropertyChanged(propertyName);
        if (validate) ValidateProperty(value, propertyName);
    }

    protected void ValidateProperty<T>(T value, string propertyName)
    {
        ClearErrors(propertyName);

        var validationContext = new ValidationContext(this)
        {
            MemberName = propertyName
        };
        var validationResults = new List<ValidationResult>();

        object valueToValidate = value;
        if (value == null && typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            valueToValidate = null;
        }

        if (Validator.TryValidateProperty(value, validationContext, validationResults)) return;

        foreach (var validationResult in validationResults)
            AddError(propertyName, validationResult.ErrorMessage ?? string.Empty);
    }

    protected void AddError(string propertyName, string error)
    {
        if (!_errors.TryGetValue(propertyName, out var value))
        {
            value = ([]);
            _errors[propertyName] = value;
        }

        if (value.Contains(error)) return;
        value.Add(error);
        OnErrorsChanged(propertyName);
    }

    protected new void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName)) OnErrorsChanged(propertyName);
    }

    protected void ClearAllErrors()
    {
        var properties = _errors.Keys.ToList();
        _errors.Clear();
        foreach (var property in properties) OnErrorsChanged(property);
    }

    private void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    protected new void ValidateAllProperties()
    {
        var properties = GetType().GetProperties()
            .Where(prop => prop.GetCustomAttributes(typeof(ValidationAttribute), true).Length != 0);

        foreach (var property in properties)
        {
            var value = property.GetValue(this);
            ValidateProperty(value, property.Name);
        }
    }
    
    private CancellationTokenSource? _lifecycleCts;
    private bool _disposed;

    /// <summary>
    /// Lifecycle cancellation token - cancelled when ViewModel is disposed.
    /// Use this for long-running operations that should stop on cleanup.
    /// </summary>
    protected CancellationToken LifecycleToken => 
        (_lifecycleCts ??= new CancellationTokenSource()).Token;

    /// <summary>
    /// Override to perform async cleanup (unsubscribe events, dispose services, etc.)
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    /// <summary>
    /// Override to perform sync cleanup (dispose managed resources)
    /// </summary>
    protected virtual void Dispose(bool disposing) { }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Cancel any ongoing operations
        _lifecycleCts?.Cancel();

        // Async cleanup
        await DisposeAsyncCore().ConfigureAwait(false);

        // Sync cleanup
        Dispose(disposing: true);
        
        // Cleanup CTS
        _lifecycleCts?.Dispose();
        _lifecycleCts = null;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Helper to ensure ViewModel is not disposed
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}
