using System;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models.ValidationAttributes
{

    [AttributeUsage(AttributeTargets.Property)]
    public class DateGteTodayAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // If empty → let [Required] handle it
            if (value == null)
                return ValidationResult.Success;

            // Try to parse the date
            if (!DateTime.TryParse(value.ToString(), out DateTime inputDate))
                return ValidationResult.Success; // Let other validators handle bad formats

            DateTime today = DateTime.Today;

            // Validation rule: date must be >= today
            if (inputDate < today)
            {
                return new ValidationResult(
                    ErrorMessage ?? $"{validationContext.DisplayName} must be today or later."
                );
            }

            return ValidationResult.Success;
        }
    }

}
