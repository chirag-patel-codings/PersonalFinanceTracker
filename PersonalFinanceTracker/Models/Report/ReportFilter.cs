using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{

    // For the data storage to be retrieved from database!
    public class ReportFilter
    {
        
        public DateOnly ReportStartDate { get; set; }    
        public DateOnly ReportEndDate { get; set; }
        
        public string ReportName { get; set; }  // IncomeExpense, NetWorth, Goal
        public byte ReportInterval { get; set; } // 1 - Monthly, 2 - Duration

        public string? TagId { get; set; } // To get the details about the tag_id

        

    }
}
