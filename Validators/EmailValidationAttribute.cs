using System.ComponentModel.DataAnnotations;

namespace AHON_TRACK.Validators;

public sealed class EmailValidationAttribute() : ValidationAttribute(DefaultErrorMessage)
{
    private static readonly string DefaultErrorMessage = "Please enter a valid email address";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string email || string.IsNullOrWhiteSpace(email))
        {
            return new ValidationResult(ErrorMessage);
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1)
        {
            return new ValidationResult(ErrorMessage);
        }

        var dotIndex = email.IndexOf('.', atIndex);
        if (dotIndex == -1 || dotIndex == atIndex + 1 || dotIndex == email.Length - 1)
        {
            return new ValidationResult(ErrorMessage);
        }

        for (var i = 0; i < email.Length - 1; i++)
        {
            if (email[i] == '.' && email[i + 1] == '.')
            {
                return new ValidationResult(ErrorMessage);
            }
        }
        return ValidationResult.Success;
    }
}