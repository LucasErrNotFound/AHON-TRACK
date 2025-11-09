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
            if (!Regex.IsMatch(input, @"^[A-Z0-9]{12,13}$"))
            {
                return new ValidationResult("Reference number must be 12–13 characters, uppercase letters and digits only.");
            }

            // Explicit uppercase check (safety)
            if (input.Any(char.IsLetter) && input != input.ToUpper())
            {
                return new ValidationResult("Reference number must use uppercase letters only.");
            }

            return ValidationResult.Success;
        }
    }
}