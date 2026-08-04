using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IAccountRepository
    {
        public int GetTotalNoOfRecords(ulong userId, string? connString);
        public Account? GetAccount(ulong userId, string accountId, string? connString);
        public IEnumerable<Account>? GetAccounts(ulong userId, int recStartNumber, int recEndNumber, string? connString);
        public bool SaveAccount(Account account, string? connString);
        public bool DeleteAccount(ulong userId, string accountId, string? connString);
        public IEnumerable<AccountType> GetAccountTypes(string? connString);
    }
}
