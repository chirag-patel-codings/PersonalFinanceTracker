using Dapper;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;
using System.Text.Json;
using TransactionDataModel = PersonalFinanceTracker.Models.Transaction;

namespace PersonalFinanceTracker.Services.BulkUpload
{
    // This class works with the database does not provide any httpServices with Plaid
    public class BulkUploadService : IBulkUploadService
    {

        private readonly IDbConnectionFactory _factory;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionRuleService _transactionRuleService;

        public BulkUploadService(IDbConnectionFactory factory, ITransactionRepository transactionRepository, ITransactionRuleService transactionRuleService)
        {
            _factory = factory;
            _transactionRepository = transactionRepository;
            _transactionRuleService = transactionRuleService;
        }

        
        // Complete
        // Inserts transaction record(s) from CSV or Linked Bank Account!!!
        // transactionCategorization: 2 = CSV upload , 3 = auto (Bank) 
        public bool BulkInsertTransactions(ulong userId, IEnumerable<TransactionDataModel> transactions, string? importTemplateId, string? connString)
        {

            int rowsInserted = 0;

            connString = connString ?? "pft_con_str";
            IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = PrepareSQLStatementForBulkInsert(userId, transactions, importTemplateId);

            using (var transaction = _conn.BeginTransaction())
            {

                try
                {
                    rowsInserted = _conn.Execute(sql, transaction: transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return false;
                }

            }

            return rowsInserted > 0;

        }

        
        // If the transactions gets the validation error for category, then change the category to "Uncategorized" and "Category Type = 0"
        public bool SaveLinkedAccountData(ulong userId, string plaidItemId, List<JsonElement> plaidAddedList, List<JsonElement> plaidModifiedList, List<JsonElement> plaidRemovedList, string plaidNewCursor, IEnumerable<Account?> linkedAccounts, string? connString)
        {

            connString = connString ?? "pft_con_str";
            IDbConnection _conn = _factory.GetDBConnection(connString);

            List<TransactionDataModel> transactionsList = new List<TransactionDataModel>();
            int rowsAffected = 0;
            string sql = "";

            if (plaidAddedList.Count > 0 || plaidModifiedList.Count > 0)
            {

                var userCategoriesList = _transactionRepository.GetSelectOptionsDataFor(userId, "categories", connString);
                var defaultCategoryIdForUser = _transactionRepository.GetDefaultCateogryId(userId, connString).ToString();

                // plaidAddedList
                // Insert records from 'added'
                if (plaidAddedList.Count > 0)
                {
                    transactionsList = GenerateTransactionRecordsWithRulesAppliedForLinkedAccounts(userId, plaidAddedList, linkedAccounts, userCategoriesList, defaultCategoryIdForUser, connString);
                    sql = PrepareSQLStatementForBulkInsert(userId, transactionsList, null);
                }

                // plaidModifiedList
                // Update records in place from 'modified' using transaction_id
                if (plaidModifiedList.Count > 0)
                {
                    transactionsList = GenerateTransactionRecordsWithRulesAppliedForLinkedAccounts(userId, plaidModifiedList, linkedAccounts, userCategoriesList, defaultCategoryIdForUser, connString);
                    sql = $"{sql} {PrepareSQLStatementForLinkedAccountsBulkUpdate(userId, transactionsList)}";
                }

            }

            // plaidRemovedList
            // Delete (Best Practise for financial transactions: Soft-delete or flag records from 'removed' using transaction_id)
            if (plaidRemovedList.Count > 0)
            {
                sql = $"{sql} {PrepareSQLStatementForLinkedAccountsBulkDelete(userId, plaidRemovedList)}";
            }

            // SQL for next cursor value. Always available on fetch eventhough when there is no new add, update or delete.
            sql = $"{sql} UPDATE pft_accounts SET plaid_next_cursor = '{plaidNewCursor}' WHERE user_id = '{userId}' AND plaid_item_id = '{plaidItemId}' AND plaid_is_account_active = '1';";

            using (var transaction = _conn.BeginTransaction())
            {

                try
                {
                    rowsAffected = _conn.Execute(sql, transaction: transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return false;
                }

            }

            return rowsAffected > 0;

        }


        // Complete
        // Prepares the Values statement for INSERT from CSV or Linked Bank Account data!!!
        private string PrepareSQLStatementForBulkInsert(ulong userId, IEnumerable<TransactionDataModel> transactions, string? importTemplateId)
        {
            string sql = "INSERT INTO pft_transactions (transaction_id, user_id, transaction_date, transaction_description, transaction_amount, account_id, category_id, goal_id, tag_id, transaction_categorization, transaction_type, plaid_transaction_id) " +
                          "VALUES ";
            
            // VALUES statement - Start
            StringBuilder sb = new StringBuilder();

            foreach (var rec in transactions)
            {

                rec.GoalId = rec.GoalId == null ? "NULL" : $"'{rec.GoalId}'";
                rec.TagId = rec.TagId == null ? "NULL" : $"'{rec.TagId}'";

                string currentRecordAsValuesStatement = $"(GeneratePrefixedId(3), {userId}, '{rec.TransactionDate.ToString("yyyy-MM-dd")}', '{rec.TransactionDescription.Replace("\\", "\\\\").Replace("\'", "\\'").Replace("\"", "\\\"")}', '{rec.TransactionAmount}', '{rec.AccountId}', '{rec.CategoryId}', {rec.GoalId}, {rec.TagId}, '{rec.TransactionCategorization}', '{rec.TransactionType}', '{rec.PlaidTransactionId}'),";
                sb.Append(currentRecordAsValuesStatement);

            }

            string result = sb.ToString().Trim();

            sql = string.Concat(sql, result.Substring(0, result.Length - 1), ";");
            // VALUES statement - End

            if (importTemplateId != null)
            {
                sql = string.Concat(sql, $"INSERT INTO pft_trans_import (user_id, import_template_id) VALUES ('{userId}', '{importTemplateId}');");
            }

            return sql;

        }


        // return type of 'void' is fine for this function, but kept 'bool' for similarity with other functions!!!
        private string PrepareSQLStatementForLinkedAccountsBulkUpdate(ulong userId, IEnumerable<TransactionDataModel> transactions)
        {

            StringBuilder sb = new StringBuilder();

            foreach (var rec in transactions)
            {
                rec.GoalId = rec.GoalId == null ? "NULL" : $"'{rec.GoalId}'";
                rec.TransactionDescription = rec.TransactionDescription.Replace("\\", "\\\\").Replace("\'", "\\'").Replace("\"", "\\\"");

                // Fields to update: transaction_date, transaction_description, transaction_amount, category_id, goal_id
                sb.Append($"UPDATE pft_transactions SET transaction_date = '{rec.TransactionDate.ToString("yyyy-MM-dd")}', transaction_description = '{rec.TransactionDescription}',  transaction_amount = '{rec.TransactionAmount}', category_id = '{rec.CategoryId}', goal_id = {rec.GoalId} WHERE user_id = '{userId}' AND plaid_transaction_id = '{rec.PlaidTransactionId}';");
            }

            string sql = sb.ToString();

            return sql;   

        }


        // return type of 'void' is fine for this function, but kept 'bool' for similarity with other functions!!!
        private string PrepareSQLStatementForLinkedAccountsBulkDelete(ulong userId, List<JsonElement> plaidRemovedList)
        {

            StringBuilder sb = new StringBuilder();

            foreach (var rec in plaidRemovedList)
            {
                // Fields to update: transaction_date, transaction_description, transaction_amount, category_id, goal_id
                sb.Append($"'{rec.GetProperty("transaction_id").GetString()!}', ");
            }

            string deletePlaidTransactionIds = sb.ToString().Trim();
            deletePlaidTransactionIds = deletePlaidTransactionIds.Substring(0, deletePlaidTransactionIds.Length - 1);

            string sql = $"DELETE FROM pft_transactions WHERE user_id = '{userId}' AND plaid_transaction_id IN ({deletePlaidTransactionIds});";

            return sql;   

        }


        // "Bulk Import Template" can NOT be applied for the transactions from Plaid, Direct value extraction using Field Name would be correct way!
        private List<TransactionDataModel> GenerateTransactionRecordsWithRulesAppliedForLinkedAccounts(ulong userId, List<JsonElement> jsonElements, IEnumerable<Account?> linkedAccounts, IEnumerable<ListOptionStringId>? userCategoriesList, string defaultCategoryId, string? connString)
        {

            List<TransactionDataModel> transactionsAsList = new List<TransactionDataModel>();
            var modelValidationResults = new List<ValidationResult>();

            foreach (var tx in jsonElements)
            {
                string plaidAccountId = tx.GetProperty("account_id").GetString()!;
               
                // Safe extraction for nullable merchant_name
                string? merchantName = tx.TryGetProperty("merchant_name", out var mProp) && mProp.ValueKind != JsonValueKind.Null
                    ? mProp.GetString()
                    : null;
                // Fallback to general transaction 'name' if merchant_name is null
                string fallbackName = tx.GetProperty("name").GetString()!;
                string description = merchantName ?? fallbackName;

                TransactionDataModel transactionForRuleApplication = new TransactionDataModel
                {
                    TransactionDate = DateOnly.ParseExact(tx.GetProperty("date").GetString()!, "yyyy-MM-dd"),    // YYYY-MM-DD format
                    TransactionAmount = tx.GetProperty("amount").GetDouble() * (-1),    // Transactions are having opposite signs
                    TransactionDescription = description,
                    PlaidTransactionId = tx.GetProperty("transaction_id").GetString()!,
                    AccountId = linkedAccounts?.FirstOrDefault(a => a?.PlaidAccountId == plaidAccountId && a?.UserId == userId)?.AccountId ?? string.Empty, // Must be required!
                    CategoryId = defaultCategoryId,
                    GoalId = null,
                    TagId = null,
                    TransactionCategorization = 3,  // Bank / External Linked Account
                    TransactionType = 1             // Regular

                };

                // Apply Existing Rules (Get Category and (optional)Goal)
                transactionForRuleApplication = _transactionRuleService.ApplyAllRulesToTransaction(userId, transactionForRuleApplication, connString).transaction;
                transactionForRuleApplication.CategoryType = userCategoriesList?.First(c => c.ListOptionId == transactionForRuleApplication.CategoryId).ListOptionType.ToString();

                // Validate model!
                var context = new ValidationContext(transactionForRuleApplication, serviceProvider: null, items: null);

                // validateAllProperties: true ensures every property is checked against its attributes
                bool isValid = Validator.TryValidateObject(transactionForRuleApplication, context, modelValidationResults, validateAllProperties: true);

                if (!isValid)
                {
                    transactionForRuleApplication.CategoryId = defaultCategoryId;
                    transactionForRuleApplication.CategoryType = "0";
                }

                // Even if any rule is not applied, it should/will be inserted into the database with default category_id.
                transactionsAsList.Add(transactionForRuleApplication);

            }

            return transactionsAsList;

        }

    }
}
