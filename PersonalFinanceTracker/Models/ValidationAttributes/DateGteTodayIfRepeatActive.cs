using System;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models.ValidationAttributes
{
    
    public class DateGteTodayIfRepeatActiveAttribute : ValidationAttribute
    {
        public string RepeatFlagProperty { get; }

        public DateGteTodayIfRepeatActiveAttribute(string repeatFlagProperty)
        {
            RepeatFlagProperty = repeatFlagProperty;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            // Get the repeat flag property (IsTransactionRepeatActive)
            var flagProp = validationContext.ObjectType.GetProperty(RepeatFlagProperty);
            if (flagProp == null)
                return new ValidationResult($"Unknown property: {RepeatFlagProperty}");

            var flagValue = flagProp.GetValue(validationContext.ObjectInstance);

            bool isRepeatActive = false;

            // Support int, bool, byte
            if (flagValue is bool b)
                isRepeatActive = b;
            else if (flagValue is int i)
                isRepeatActive = (i == 1);
            else if (flagValue is byte bt)
                isRepeatActive = (bt == 1);

            // If repeat is NOT active → skip validation
            if (!isRepeatActive)
                return ValidationResult.Success;

            // If repeat IS active → date must be >= today
            if (value == null)
                return ValidationResult.Success; // client-side handles required

            if (DateTime.TryParse(value.ToString(), out DateTime inputDate))
            {
                if (inputDate >= DateTime.Today)
                    return ValidationResult.Success;
            }

            return new ValidationResult(ErrorMessage);
        }
    }

}
