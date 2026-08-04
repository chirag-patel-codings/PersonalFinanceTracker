using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IBudgetDetailsRepository
    {
        public IEnumerable<BudgetDetails>? GetBudgetDetails(ulong userId, DateOnly budgetDetailsStartDate, DateOnly budgetDetailsEndDate, string? connString);
        public bool SaveBudgetDetails(BudgetDetails budget, string? connString);
        public bool DeleteBudgetDetails(ulong userId, string budgetId, string budgetDateDigits, string? connString);
        
    }
}
