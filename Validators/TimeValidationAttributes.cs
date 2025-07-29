using System;
using System.ComponentModel.DataAnnotations;

namespace AHON_TRACK.Validators;

public sealed class StartTimeValidationAttribute(
    string matchProperty,
    string errorMessage = "Start time must be less than end time"
) : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var endTime = instance.GetType().GetProperty(matchProperty)?.GetValue(instance);

        if (endTime == null)
        {
            return ValidationResult.Success;
        }

        return ((TimeOnly)value!).CompareTo(endTime) < 0
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

public sealed class EndTimeValidationAttribute(
    string matchProperty,
    string errorMessage = "End time must be greater than start time"
) : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var startTime = instance.GetType().GetProperty(matchProperty)?.GetValue(instance);

        if (startTime == null)
        {
            return ValidationResult.Success;
        }

        return ((TimeOnly)value!).CompareTo(startTime) > 0
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}