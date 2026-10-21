using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using DataValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace PersonalFinanceTrackerTests
{
    // To get the Attribute Validation Errors for the Model Classes
    public static class ModelValidationHelper
    {
        public static IList<DataValidationResult> ValidateModel(object model)
        {
            var results = new List<DataValidationResult>();
            var ctx = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, ctx, results, true);
            return results;
        }
    }

}
