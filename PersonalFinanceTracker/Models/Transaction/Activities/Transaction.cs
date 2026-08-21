using PersonalFinanceTracker.Models.ValidationAttributes;
using System.ComponentModel.DataAnnotations;
using PersonalFinanceTracker.Models.ValidationAttributes;

namespace PersonalFinanceTracker.Models
{
    public class Transaction
    {
        
        public string? TransactionId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion

        [DateGteTodayIfRepeatActive("IsTransactionRepeatActive", ErrorMessage = "The date must be today or greater to generate a transaction recurrings!")]
        [LessThan("TransactionRepeatEndDate", ErrorMessage = "The date must be earlier than recurring end date!")]
        [Required(ErrorMessage = "Please provide transaction date!")]
        public DateOnly TransactionDate { get; set; }

        [MaxLength(255, ErrorMessage = "Transaction description should not be more than 255 characters long!")]
        [Required(ErrorMessage = "Please provide transaction description!")]
        public string TransactionDescription { get; set; }

        [Required(ErrorMessage = "Please provide transaction amount!")]
        public double TransactionAmount { get; set; }    

        [Required(ErrorMessage = "Please provide account id!")]
        public string AccountId { get; set; }

        [Required(ErrorMessage = "Please provide category id!")]
        public string CategoryId { get; set; }

        public string? GoalId { get; set; }
        public string? TagId { get; set; }
        public byte TransactionCategorization { get; set; } = 1; // 1 = manual, 2 = CSV upload , 3 = auto (Bank); TO BE SET FOR MANUAL TRANSACTION ENTRY!!!
        public byte TransactionType { get; set; } = 1; // 1 = Regular, 2 - Recurring
        
        // FOR TRANSACTION REPEAT:
        public int? TransactionRepeatId { get; set; }
        public byte? IsTransactionRepeatActive { get; set; } = 0;    // 1 = true, 0 = false.
        public byte? TransactionRepeatInterval { get; set; }    //  Weekly - 1, BiWeekly - 2, Monthly – 3,  BiMonthly – 4,  Quarterly – 5,  Half-Yearly – 6 , Yearly - 7
        public DateOnly? TransactionRepeatEndDate { get; set; }


    }
    
}