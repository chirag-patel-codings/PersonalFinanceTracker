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

        [Required(ErrorMessage = "Please provide the type of account!")]
        public int AccountTypeId { get; set; }

        [MaxLength(255, ErrorMessage = "Account description should not be more than 255 characters long!")]
        public string? AccountDescription { get; set; }

        [Required(ErrorMessage = "Is A Linked Account Flag is required!")]  // For Model Validation purpose only.
        public byte IsALinkedAccount { get; set; } = 0; // Will be programattically set
    }
}
