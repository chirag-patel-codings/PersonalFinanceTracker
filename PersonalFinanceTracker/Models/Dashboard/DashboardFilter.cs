using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{

    // For the data storage to be retrieved from database!
    public class DashboardFilter
    {
        
        public DateOnly DashboardParametersStartDate { get; set; }    
        public DateOnly DashboardParametersEndDate { get; set; }

    }
}
