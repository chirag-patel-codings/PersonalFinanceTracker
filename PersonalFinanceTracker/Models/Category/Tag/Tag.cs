using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class Tag
    {
        public string? TagId { get; set; }
        public ulong? UserId { get; set; }

        [Required(ErrorMessage = "Tag name is required!")]
        [MaxLength(100, ErrorMessage = "Tag name should not be more than 100 characters long!")]
        public string? TagName {  get; set; }

        [MaxLength(255, ErrorMessage = "Tag description should not be more than 255 characters long!")]
        public string? TagDescription { get; set; }


        [MaxLength(50, ErrorMessage = "Tag color should not be more than 50 characters long!")]
        public string? TagColor { get; set; } = "#ffffff";

        public sbyte? IsTagActive { get; set; } = 1;     // Value & member is provided when element is not present. If member is present with incorrect value, model fails. 

    }
}
