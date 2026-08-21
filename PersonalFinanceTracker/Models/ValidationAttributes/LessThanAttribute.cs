using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models.ValidationAttributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class LessThanAttribute : ValidationAttribute
    {
        private readonly string _otherProperty;

        public LessThanAttribute(string otherProperty)
        {
            _otherProperty = otherProperty;
        }

        // Server-Side Validation Logic
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Fetch the property info for the other field (e.g., BudgetDetailsEndDate)
            var otherPropertyInfo = validationContext.ObjectType.GetProperty(_otherProperty);

            if (otherPropertyInfo == null)
            {
                return new ValidationResult($"Unknown property: {_otherProperty}");
            }

            var otherValue = otherPropertyInfo.GetValue(validationContext.ObjectInstance, null);

            // If either value is null, pass validation (leave that to [Required])
            if (value == null || otherValue == null)
            {
                return ValidationResult.Success;
            }

            // Compare values (works for DateOnly, DateTime, int, double, etc.)
            if (value is IComparable comparableValue)
            {
                if (comparableValue.CompareTo(otherValue) > 0)
                {
                    return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} must be earlier than {_otherProperty}.");
                }
            }

            return ValidationResult.Success;
        }

        
    }
}
