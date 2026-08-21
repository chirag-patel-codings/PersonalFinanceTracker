using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface ITransactionRepository
    {
        public int GetTotalNoOfRecords(ulong userId, TransactionFilter transactionQueryFilter, string? connString);
        public Transaction? GetTransaction(ulong userId, string transactionId, string? connString);
        public IEnumerable<Transaction>? GetTransactions(ulong userId, TransactionFilter transactionQueryFilter, int recStartNumber, int recEndNumber, string? connString);
        public bool SaveTransaction(Transaction transaction, string? connString);
        public bool DeleteTransaction(ulong userId, string transactionId, string? connString);
        public bool BulkUploadTransactions(ulong userId, IEnumerable<Transaction> transactions, string? connString);
        public ulong GetDefaultCateogryId(ulong userId, string? connString);    // When bulk upload or linked account
        public IEnumerable<ListOptionStringId> GetSelectOptionsDataFor(ulong userId, string selectFor, string? connString);


    }
}
