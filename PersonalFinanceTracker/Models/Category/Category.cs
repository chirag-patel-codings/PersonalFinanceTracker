using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public class Category
    {
        
        public string? CategoryId { get; set; }  // will be generated in the database
        public ulong? UserId { get; set; }  // to be provided at the time of record insertion


        [Required(ErrorMessage = "Please select standard category!")]
        public string? StandardCategoryId { get; set; }


        [Required(ErrorMessage = "Please provide the type of category!")]
        public sbyte CategoryType { get; set; }  // Income = 1, Expense = -1, Permanent = 0

        [Required(ErrorMessage = "Category name is required!")]
        [MaxLength(100, ErrorMessage = "Category name should not be more than 100 characters long!")]
        public string CategoryName { get; set; }


        [MaxLength(255, ErrorMessage = "Category description should not be more than 255 characters long!")]
        public string? CategoryDescription { get; set; }


        [MaxLength(50, ErrorMessage = "Category color should not be more than 50 characters long!")]
        public string? CategoryColor { get; set; } = "#ffffff";

        public int CategoryDisplayOrder { get; set; }   // to be provided at the time of record insertion -- LINQ 

    }
    
}