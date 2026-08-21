using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using PersonalFinanceTracker.Models.ValidationAttributes;

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

}
