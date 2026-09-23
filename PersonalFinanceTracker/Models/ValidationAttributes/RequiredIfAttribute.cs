using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models.ValidationAttributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class RequiredIfAttribute : ValidationAttribute
    {
        private readonly string _otherProperty;

        public RequiredIfAttribute(string otherProperty)
        {
            _otherProperty = otherProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var otherProp = validationContext.ObjectType.GetProperty(_otherProperty);
            if (otherProp == null)
                return new ValidationResult($"Unknown property: {_otherProperty}");

            var otherValue = otherProp.GetValue(validationContext.ObjectInstance);

            // Convert otherValue to byte (TransactionAmountMatchComparision type)
            byte otherByte = 0;
            if (otherValue is byte b)
                otherByte = b;
            else if (otherValue is int i)
                otherByte = (byte)i;

            // Condition: required when otherValue == 3
            bool isRequired = (otherByte == 3);

            if (isRequired)
            {
                if (value == null)
                {
                    return new ValidationResult(
                        ErrorMessage ?? $"{validationContext.DisplayName} is required."
                    );
                }
            }

            return ValidationResult.Success;
        }
    }

}
