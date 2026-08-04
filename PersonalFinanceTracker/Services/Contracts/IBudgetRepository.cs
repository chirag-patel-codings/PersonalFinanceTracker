using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IBudgetRepository
    {
        public IEnumerable<Budget>? GetBudgetSettings(ulong userId, string? connString);
        public bool SaveBudgetSettings(Budget budget, string? connString);
    }
}
