using PersonalFinanceTracker.Models.ValidationAttributes;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class TransactionRule
    {
        
        public string? TransactionRuleId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion for the "REPEATS" Settings table

        public string AccountId { get; set; }

        [Required(ErrorMessage = "Please provide category!!")]
        public string CategoryId { get; set; }  // If no rules, then assign a default category!!!

        public string? GoalId { get; set; }

        [MaxLength(255, ErrorMessage = "Transaction description match text should not be more than 255 characters long!")]
        [Required(ErrorMessage = "Please provide match text!")]
        public string TransactionTextToMatch { get; set; }

        public byte TransactionTextMatchComparision { get; set; } = 0;    // contains (Default) = 0, starts with = 1, exact match = 2

        public byte TransactionAmountMatchComparision { get; set; } = 0;    // Any Amount = 0, Money In = 1, Money Out = 2, Exact Amount = 3.

        [RequiredIf("TransactionAmountMatchComparision", ErrorMessage = "Exact amount is required!")]
        public double? TransactionExactAmount { get; set; } // Required when TransactionAmountMatchComparision == 3

        [MaxLength(255, ErrorMessage = "Transaction description should not be more than 255 characters long!")]
        public string? TransactionDescriptionOverride { get; set; } = null; // To exclude override, keep the description blank!!!

    }
    
}