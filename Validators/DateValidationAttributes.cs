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

public sealed class TodayValidationAttribute(
    string errorMessage = "The date you selected is invalid. Past dates are not allowed") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var today = DateTime.Today;
        
        if (value is not DateTime selectedDate)
            return ValidationResult.Success;
        
        return selectedDate.CompareTo(today) >= 0
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

public sealed class PurchasedDateValidationAttribute(
    string matchProperty,
    string errorMessage = "Purchase date must be before warranty expiry") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var warrantyProperty = instance.GetType().GetProperty(matchProperty);
        var warrantyDate = warrantyProperty?.GetValue(instance) as DateTime?;

        // Skip validation if either date is null
        if (value is not DateTime purchaseDate || !warrantyDate.HasValue) 
            return ValidationResult.Success;

        // Purchase date should be before warranty expiry
        return purchaseDate < warrantyDate.Value
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

public sealed class WarrantyDateValidationAttribute(
    string matchProperty,
    string errorMessage = "Warranty expiry must be after purchase date") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var purchaseProperty = instance.GetType().GetProperty(matchProperty);
        var purchaseDate = purchaseProperty?.GetValue(instance) as DateTime?;

        // Skip validation if either date is null
        if (value is not DateTime warrantyDate || !purchaseDate.HasValue) 
            return ValidationResult.Success;

        // Warranty expiry should be after purchase date
        return warrantyDate > purchaseDate.Value
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

public sealed class NotFutureDateAttribute(
    string errorMessage = "Date cannot be in the future") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Skip validation if date is null
        if (value is not DateTime date) 
            return ValidationResult.Success;

        // Compare only the date portion, ignoring time
        return date.Date <= DateTime.Today
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

public sealed class LastMaintenanceDateValidationAttribute(
    string matchProperty,
    string errorMessage = "Last maintenance must be before next maintenance") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var nextMaintenanceProperty = instance.GetType().GetProperty(matchProperty);
        var nextMaintenanceDate = nextMaintenanceProperty?.GetValue(instance) as DateTime?;

        // Skip validation if either date is null
        if (value is not DateTime lastMaintenanceDate || !nextMaintenanceDate.HasValue) 
            return ValidationResult.Success;

        // Last maintenance should be before next maintenance
        return lastMaintenanceDate < nextMaintenanceDate.Value
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

public sealed class NextMaintenanceDateValidationAttribute(
    string matchProperty,
    string errorMessage = "Next maintenance must be after last maintenance") : ValidationAttribute(errorMessage)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var lastMaintenanceProperty = instance.GetType().GetProperty(matchProperty);
        var lastMaintenanceDate = lastMaintenanceProperty?.GetValue(instance) as DateTime?;

        // Skip validation if either date is null
        if (value is not DateTime nextMaintenanceDate || !lastMaintenanceDate.HasValue) 
            return ValidationResult.Success;

        // Next maintenance should be after last maintenance
        return nextMaintenanceDate > lastMaintenanceDate.Value
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}