using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Client.Services;

internal static class AccountFormValidation
{
    internal static bool TryValidate(object value, out string message)
    {
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            value,
            new ValidationContext(value),
            results,
            validateAllProperties: true);
        message = string.Join("；", results.Select(result => result.ErrorMessage));
        return valid;
    }
}
