using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    // To provide the criteria to retrieve the budget details data
    public class BudgetDetailsFilter
    {
        [LessThan("BudgetDetailsEndDate", ErrorMessage = "Start date must be earlier than end date!")]
        [Required(ErrorMessage = "Start date is required!")]
        public DateOnly BudgetDetailsStartDate { get; set; } = new DateOnly(DateTime.Now.Year, 1, 1);

        [Required(ErrorMessage = "End date is required!")]
        public DateOnly BudgetDetailsEndDate { get; set; } = new DateOnly(DateTime.Now.Year, 12, 31);

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class LessThanAttribute : ValidationAttribute, IClientModelValidator
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

        // Client-Side Validation Logic
        public void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            // Use ALL LOWERCASE for data-val-lessthan to avoid HTML rendering bugs
            MergeAttribute(context.Attributes, "data-val-lessthan", ErrorMessage ?? "Invalid date");
            MergeAttribute(context.Attributes, "data-val-lessthan-other", _otherProperty);
        }

        private void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
        {
            if (!attributes.ContainsKey(key))
            {
                attributes.Add(key, value);
            }
        }
    }

}
