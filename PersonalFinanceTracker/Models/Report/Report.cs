using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{

    // For the data storage to be retrieved from database!
    public class Report
    {
        public int ReportRowNo { get; set; }            // ROW IDENTIFIER...TO DISPLAY IN TABLE 
        public string? ReportMonthYear { get; set; }    // month_year
        public int? ReportClassification { get; set; }  // account_type_id, category_type
        public string? ReportName { get; set; }         // account_name, goal_name, category
        public string? ReportInterval { get; set; }     // goal_interval
        public double? ReportAmount1 { get; set; }      // SECONDARY VALUES: transaction_amount, goal_defined_amount_for_period, goal_defined_amount, total_prorated_budget (SECOND COLUMN)
        public double? ReportAmount2 { get; set; }      // DISPLAY VALUES: account_net_worth, contribution, actual (FIRST COLUMN)

    }
}
