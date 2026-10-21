using System.ComponentModel.DataAnnotations;
using PersonalFinanceTracker.Models.ValidationAttributes;

namespace PersonalFinanceTracker.Models
{

    // For the data storage to be retrieved from database!
    public class DashboardFilter
    {
        
        [LessThan("DashboardParametersEndDate", ErrorMessage = "Start date must be earlier than end date!")]
        [Required(ErrorMessage = "Start date is required!")]
        public DateOnly DashboardParametersStartDate { get; set; } 

        [Required(ErrorMessage = "End date is required!")]   
        public DateOnly DashboardParametersEndDate { get; set; }

    }
}
