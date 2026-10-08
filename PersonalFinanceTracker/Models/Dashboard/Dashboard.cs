using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{

    // For the data storage to be retrieved from database!
    public class DashboardBarAndLineCharts
    {
        public DashboardBarAndLineCharts(string? monthYear, string? category, double? amount)
        {

            MonthYear = monthYear;
            Category = category;
            Amount = amount;
        
        }

        public string? MonthYear { get; set; }        // ReportMonthYear
        public string? Category { get; set; }   // ReportClassification
        public double? Amount { get; set; }      // ReportAmount2

    }
}
