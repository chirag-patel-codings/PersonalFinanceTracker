using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using System.ComponentModel.DataAnnotations;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface ITransactionRepository
    {
        public int GetTotalNoOfRecords(ulong userId, TransactionFilter transactionQueryFilter, string? connString);
        public Transaction? GetTransaction(ulong userId, string transactionId, string? connString);
        public IEnumerable<Transaction>? GetTransactions(ulong userId, TransactionFilter transactionQueryFilter, int recStartNumber, int recEndNumber, string? connString);
        public bool SaveTransaction(Transaction transaction, string? connString);
        public bool DeleteTransaction(ulong userId, string transactionId, string? connString);

        public IEnumerable<ListOptionStringId> GetSelectOptionsDataFor(ulong userId, string selectFor, string? connString);

        // Bulk Import
        public (List<ValidationResult>? modelValidationResults, List<Transaction>? transactions) GenerateTransactionRecordsWithRulesApplied(ulong userId, BulkImportTemplate bulkImportTemplate, List<string[]> transactionDataList, string defaultCategoryId, byte transactionCategorization, string? connString);
        public ulong GetDefaultCateogryId(ulong userId, string? connString);    // When bulk upload or linked account
        public bool BulkUploadTransactions(ulong userId, IEnumerable<Transaction> transaction, string? connString);
        
        // Apply Rule
        public IEnumerable<Transaction> GetCSVAndBankLinkTransactionsForUser(ulong userID, string? connString);
        public void UpdateTransactionRecordFromRule(ulong userID, Transaction transaction, string? connString);

        
    }
}
