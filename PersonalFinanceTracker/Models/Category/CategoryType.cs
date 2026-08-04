namespace PersonalFinanceTracker.Models
{
    // This class is generically defines the type of categories: Income = 1, Expense = -1, Permanent = 0
    public class CategoryType
    {
        public CategoryType(sbyte categoryTypeId, string categoryTypeName)
        {
            CategoryTypeId = categoryTypeId;
            CategoryTypeName = categoryTypeName;
        }

        public sbyte CategoryTypeId { get; set; }
        public string CategoryTypeName { get; set; }
    }

}
