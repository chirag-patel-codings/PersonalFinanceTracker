using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models.ValidationAttributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class IsValidForCategoryTypeAttribute : ValidationAttribute
    {
        private readonly string _otherProperty;

        public IsValidForCategoryTypeAttribute(string otherProperty)
        {
            _otherProperty = otherProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            bool isValid = true;
            var otherProp = validationContext.ObjectType.GetProperty(_otherProperty);

            if (otherProp == null)
                return new ValidationResult($"Unknown property: {_otherProperty}");

            var otherValue = otherProp.GetValue(validationContext.ObjectInstance).ToString();

            // If either value is null, pass validation (leave that to [Required])
            if (value == null || otherValue == null || otherValue == "" )      
            {
                return ValidationResult.Success;
            }

            if (double.TryParse(value.ToString(), out double amountValue))
            {
                if( (otherValue == "-1" && amountValue > 0) || (otherValue == "1" && amountValue < 0) )
                {
                    isValid = false;
                }
            }
            else
            {
                return new ValidationResult($"Invalid amount value!");
            }
            
            return isValid ? ValidationResult.Success : new ValidationResult(ErrorMessage);

        }
    }

}
