using PersonalFinanceTracker.Models.ValidationAttributes;
using System.ComponentModel.DataAnnotations;
using PersonalFinanceTracker.Models.ValidationAttributes;

namespace PersonalFinanceTracker.Models
{
    public class BulkImportTemplate
    {
        
        public string? ImportTemplateId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion

        public string? AccountId { get; set; }  // MAY NOT BE REQUIRED!

        [MaxLength(100, ErrorMessage = "Import template name should not be more than 100 characters long!")]
        [Required(ErrorMessage = "Please provide import template name!")]
        public string ImportTemplateName { get; set; }

        public byte ImportType { get; set; } = 1;    // CSV = 1 or Bank Linked: 2

        public byte HeaderRowIndex { get; set; } = 0;

        [Required(ErrorMessage = "Please provide date field column index!")]
        public sbyte DateFieldIndex { get; set; } = (sbyte)-1;

        [Required(ErrorMessage = "Please provide description field column index!")]
        public sbyte DescriptionFieldIndex { get; set; } = (sbyte)-1;

        public sbyte AmountFieldIndex { get; set; } = (sbyte)-1;

        public sbyte DebitFieldIndex { get; set; } = (sbyte)-1;

        public sbyte CreditFieldIndex { get; set; } = (sbyte)-1;

    }
    
}