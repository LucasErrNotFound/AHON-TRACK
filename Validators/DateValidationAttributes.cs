using System;
using System.ComponentModel.DataAnnotations;

namespace AHON_TRACK.Validators;

public sealed class StartDateValidationAttribute(
    string matchProperty,
    string errorMessage = "Start date must happen before the end date") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var endDateProperty = instance.GetType().GetProperty(matchProperty);
        var endDate = endDateProperty?.GetValue(instance) as DateOnly?;

        // Skip validation if the current value is null or end date is null
        if (value is not DateOnly currentDate || !endDate.HasValue) 
            return ValidationResult.Success;

        return currentDate.CompareTo(endDate.Value) < 0
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

public sealed class EndDateValidationAttribute(
    string matchProperty,
    string errorMessage = "End date should happen after the start date") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var startDateProperty = instance.GetType().GetProperty(matchProperty);
        var startDate = startDateProperty?.GetValue(instance) as DateOnly?;

        // Skip validation if the current value is null or start date is null
        if (value is not DateOnly currentDate || !startDate.HasValue) 
            return ValidationResult.Success;

        return currentDate.CompareTo(startDate.Value) > 0
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}