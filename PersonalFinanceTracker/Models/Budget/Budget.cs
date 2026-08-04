namespace PersonalFinanceTracker.Models
{
    // This class mainly used to configure budget settings.
    // As the table-name in the database is 'pft_budgets', the name of this class has not been set to 'BudgetSettings'.
    public class Budget
    {
        public ulong? UserId { get; set; }
        public string CategoryId {  get; set; }
        public string? CategoryName { get; set; }
        public byte BudgetToBeRolledOver { get; set; } = 0;
        public byte? BudgetIfUnderSettings { get; set; } = null;
        public byte? BudgetIfOverSettings { get; set; } = null;
        
    }
}
