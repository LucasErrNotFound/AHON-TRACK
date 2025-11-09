using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace AHON_TRACK.Validators
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class UppercaseReferenceNumberValidator : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success; // Let [Required] handle nulls

            string input = value.ToString() ?? string.Empty;

            // Must be alphanumeric, uppercase only, 12–13 characters
            if (!Regex.IsMatch(input, @"^(\d{13}|\d{6})$"))
            {
                return new ValidationResult("Reference number must be exactly 13 digits for GCash or 6 digits for Maya");
            }

            return ValidationResult.Success;
        }
    }
}