using PersonalFinanceTracker.Models;
using System.Text.Json;
using TransactionDataModel = PersonalFinanceTracker.Models.Transaction;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IBulkUploadService
    {
        public bool SaveLinkedAccountData(ulong userId, string plaidItemId, List<JsonElement> plaidAddedList, List<JsonElement> plaidModifiedList, List<JsonElement> plaidRemovedList, string plaidNewCursor, IEnumerable<Account?> linkedAccounts, string? connString);
        public bool BulkInsertTransactions(ulong userId, IEnumerable<TransactionDataModel> transactions, string? importTemplateId, string? connString);

    }
}
