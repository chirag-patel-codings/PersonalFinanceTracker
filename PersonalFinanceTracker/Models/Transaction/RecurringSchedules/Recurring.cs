using PersonalFinanceTracker.Models.ValidationAttributes;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class Recurring
    {
        
        public string? RecurringId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion for the "REPEATS" Settings table

        [DateGteToday(ErrorMessage = "First date must be today or later!")]
        [Required(ErrorMessage = "Please provide recurring date!")]
        public DateOnly? RecurringNextDate { get; set; }     // Transaction Date

        [MaxLength(255, ErrorMessage = "Recurring description should not be more than 255 characters long!")]
        [Required(ErrorMessage = "Please provide recurring description!")]
        public string RecurringDescription { get; set; }

        [Required(ErrorMessage = "Please provide recurring amount!")]
        public double RecurringAmount { get; set; }    

        [Required(ErrorMessage = "Please provide account id!")]
        public string AccountId { get; set; }

        [Required(ErrorMessage = "Please provide category id!")]
        public string CategoryId { get; set; }

        public string? GoalId { get; set; }
        public string? TagId { get; set; }
        public byte TransactionCategorization { get; set; } = 1; // 1 = manual, 2 = CSV upload , 3 = auto (Bank); TO BE SET FOR MANUAL TRANSACTION ENTRY!!!
        // public byte RecurringType { get; set; } = 2; // 1 = Regular, 2 - Recurring (May be fixed value assigned at the time of record move to the transaction main table)
        
        // FOR TRANSACTION REPEAT:
        public int? TransactionRepeatId { get; set; }
        public byte IsTransactionRepeatActive { get; set; } = 0;    // 1 = true, 0 = false.
        public byte TransactionRepeatInterval { get; set; } = 0;   //  Weekly - 1, BiWeekly - 2, Monthly – 3,  BiMonthly – 4,  Quarterly – 5,  Half-Yearly – 6 , Yearly - 7
        public DateOnly? TransactionRepeatEndDate { get; set; }


    }
    
}