using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface ITransactionRuleRepository
    {
        public int GetTotalNoOfRecords(ulong userId, string? connString);
        public TransactionRule? GetTransactionRule(ulong userId, string transactionRuleId, string? connString);
        public IEnumerable<TransactionRule>? GetTransactionRules(ulong userId, int recStartNumber, int recEndNumber, string? connString);
        public bool SaveTransactionRule(TransactionRule transactionRule, string? connString);
        public bool DeleteTransactionRule(ulong userId, string transactionRuleId, string? connString);
        

    }
}
