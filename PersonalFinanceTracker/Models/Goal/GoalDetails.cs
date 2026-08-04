using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class GoalDetails
    {
        // public uint RecordId { get; set; }  // To set the unique form-id at client side!!! NOT POSSIBLE FOR NEW RECORD!!!

        public ulong? GoalID { get; set; }
        
        [Required(ErrorMessage = "Please provide the goal interval!")]
        public byte GoalInterval { get; set; }   // Weekly - 1, BiWeekly - 2, Monthly – 3, Quarterly – 4,  Half-Yearly – 5 , Yearly - 6

        [Required(ErrorMessage = "Please provide the goal amount!")]
        public double GoalAmount { get; set; }

        [Required(ErrorMessage = "Please provide the goal start date!")]
        public DateOnly GoalPeriodStartDate { get; set; }

        [Required(ErrorMessage = "Please provide the goal end date!")]
        public DateOnly GoalPeriodEndDate { get; set; }

    }
}
