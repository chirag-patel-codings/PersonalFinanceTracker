using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class Goal
    {
        public string? GoalId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion

        [Required(ErrorMessage = "Please provide the type of goal!")]
        public sbyte? GoalType { get; set; }  // Savings = 1, Reduce Debt = 2, Investing = 3, Tax Planning = 4, Other = 5

        [Required(ErrorMessage = "Goal name is required!")]
        [MaxLength(100, ErrorMessage = "Goal name should not be more than 100 characters long!")]
        public string GoalName { get; set; }

        [MaxLength(255, ErrorMessage = "Goal description should not be more than 255 characters long!")]
        public string? GoalDescription { get; set; }

        public IEnumerable<GoalDetails>? GoalDetails { get; set; }

    }
}
