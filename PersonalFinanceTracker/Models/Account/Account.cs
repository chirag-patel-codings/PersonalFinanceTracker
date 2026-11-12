using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class Account
    {
        public string? AccountId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion

        [Required(ErrorMessage = "Account name is required!")]
        [MaxLength(100, ErrorMessage = "Account name should not be more than 100 characters long!")]
        public string AccountName { get; set; }

        [Required(ErrorMessage = "Account institution name is required!")]
        [MaxLength(100, ErrorMessage = "Account institution name should not be more than 100 characters long!")]
        public string AccountInstitutionName { get; set; }

        [Required(ErrorMessage = "Please provide the type of account!")]    // Kept for the Consistency from front end. Not required by the model as it will fail if not provided.
        public int AccountTypeId { get; set; }

        [MaxLength(255, ErrorMessage = "Account description should not be more than 255 characters long!")]
        public string? AccountDescription { get; set; }

        [Required(ErrorMessage = "Is A Linked Account Flag is required!")]  // Kept for the Consistency from front end. Not required by the model as it will fail if not provided.
        public byte IsALinkedAccount { get; set; } = 0; // Will be programattically set

        // For Plaid Integration - Start
        // These fields will be set in the backend after successful linking of the account with Plaid
        public string? PlaidAccessToken { get; set; } = null; 
        public string? PlaidItemId { get; set; } = null;  
        public string? PlaidInstitutionId { get; set; } = null;  
        public string? PlaidAccountId { get; set; } = null; // May not be useful but keeping it for future use if needed.
        
        // For Plaid Integration - End

    }
}
