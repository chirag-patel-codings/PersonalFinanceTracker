using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class Transaction
    {
        
        public string? TransactionId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion


        [Required(ErrorMessage = "Please provide transaction date!")]
        public DateOnly TransactionDate { get; set; }

        [MaxLength(255, ErrorMessage = "Transaction description should not be more than 255 characters long!")]
        [Required(ErrorMessage = "Please provide transaction description!")]
        public string TransactionDescription { get; set; }

        [Required(ErrorMessage = "Please provide transaction amount!")]
        public double TransactionAmount { get; set; }   // to be provided at the time of record insertion -- LINQ 

        [Required(ErrorMessage = "Please provide account id!")]
        public string AccountId { get; set; }

        [Required(ErrorMessage = "Please provide category id!")]
        public string CategoryId { get; set; }

        public string? GoalId { get; set; }
        public string? TagId { get; set; }
        public byte? IsTransactionManuallyEntered { get; set; }  // 1 = true, 0 = false
        public byte? IsTransactionManuallyCategorized { get; set; }  // 1 = true, 0 = false

    }
    
}