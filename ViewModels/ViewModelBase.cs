﻿using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AHON_TRACK.ViewModels;

public abstract class ViewModelBase : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Count != 0;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

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

    protected void SetProperty<T>(ref T field, T value, bool validate = false,
        [CallerMemberName] string propertyName = null!)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;

        field = value;
        OnPropertyChanged(propertyName);
        if (validate) ValidateProperty(value, propertyName);
    }

    private void ValidateProperty<T>(T value, string propertyName)
    {
        ClearErrors(propertyName);

        var validationContext = new ValidationContext(this)
        {
            MemberName = propertyName
        };
        var validationResults = new List<ValidationResult>();

        if (Validator.TryValidateProperty(value, validationContext, validationResults)) return;

        foreach (var validationResult in validationResults)
            AddError(propertyName, validationResult.ErrorMessage ?? string.Empty);
    }

    private void AddError(string propertyName, string error)
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

    private void ClearErrors(string propertyName)
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

    protected void ValidateAllProperties()
    {
        var properties = GetType().GetProperties()
            .Where(prop => prop.GetCustomAttributes(typeof(ValidationAttribute), true).Length != 0);

        foreach (var property in properties)
        {
            var value = property.GetValue(this);
            ValidateProperty(value, property.Name);
        }
    }
}
