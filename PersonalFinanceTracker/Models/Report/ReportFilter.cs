using System.ComponentModel.DataAnnotations;
using PersonalFinanceTracker.Models.ValidationAttributes;

namespace PersonalFinanceTracker.Models
{

    // For the data storage to be retrieved from database!
    public class ReportFilter
    {

        [LessThan("ReportEndDate", ErrorMessage = "Start date must be earlier than end date!")]
        [Required(ErrorMessage = "Start date is required!")]
        public DateOnly ReportStartDate { get; set; }    
        
        [Required(ErrorMessage = "End date is required!")]
        public DateOnly ReportEndDate { get; set; }
        
        public string ReportName { get; set; }  // IncomeExpense, NetWorth, Goal
        public byte ReportInterval { get; set; } // 1 - Monthly, 2 - Duration

        public string? TagId { get; set; } // To get the details about the tag_id

    }
    
}
