using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using PersonalFinanceTracker.Models.ValidationAttributes;

namespace PersonalFinanceTracker.Models
{
    public class TransactionFilter
    {
        [LessThan("TransactionEndDate", ErrorMessage = "Start date must be earlier than end date!")]
        [Required(ErrorMessage = "Start date is required!")]
        public DateOnly TransactionFilterStartDate { get; set; } = new DateOnly(DateTime.Now.Year, 1, 1);

        [Required(ErrorMessage = "End date is required!")]
        public DateOnly TransactionFilterEndDate { get; set; } = new DateOnly(DateTime.Now.Year, 12, 31);

        public string? TransactionFilterCategoryId { get; set; }
        public string? TransactionFilterAccountId { get; set; }
        public string? TransactionFilterDescription { get; set; }
        public string? TransactionFilterTagId { get; set; }
        public string? TransactionFilterGoalId { get; set; }
        public byte TransactionFilterCategorization { get; set; }  // 0 = all, 1 = manual, 2 = auto;
        public byte TransactionFilterType { get; set; }   // 0 = all, 1 = Regular, 2 - Recurring

    }

}
