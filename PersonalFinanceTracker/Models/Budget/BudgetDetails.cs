using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    // Budget Controller does not have Budget Model as not required!!!
    // This is for the data retrieval from the database!
    public class BudgetDetails
    {
        public ulong? userId {  get; set; }

        [Required(ErrorMessage = "Budget id is required!")]
        public string BudgetId { get; set; }
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string CategoryType { get; set; }

        [Required(ErrorMessage = "Budget date digits is required!")]
        public string BudgetDateDigits { get; set; }  // Dates are retrieved in YYYYMMDD format and as string from database
        public string BudgetMonthYear { get; set; } // Retrieved as string from database  - Must always be the first of the every month
        public double? BudgetAmount { get; set; }   // Amount can be null here!!!

    }
}
