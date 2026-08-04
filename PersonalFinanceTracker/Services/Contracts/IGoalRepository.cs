using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IGoalRepository
    {
        public int GetTotalNoOfRecords(ulong userId, string? connString);
        public IEnumerable<Goal>? GetGoals(ulong userId, int recStartNumber, int recEndNumber, string? connString);
        public Goal? GetGoalWithDetails(ulong userId, string goalId, string? connString);
        public bool SaveGoalWithDetails(Goal goal, string? connString);
        public bool DeleteGoal(ulong userId, string goalId, string? connString);
    }
}
