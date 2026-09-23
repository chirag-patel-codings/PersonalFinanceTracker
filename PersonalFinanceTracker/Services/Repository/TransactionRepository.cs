using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using MySqlX.XDevAPI.Common;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Ocsp;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.TransactionRules;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using TransactionDataModel = PersonalFinanceTracker.Models.Transaction;

namespace PersonalFinanceTracker.Services.Repository
{
    public class TransactionRepository : ITransactionRepository
    {
        
        private readonly IDbConnectionFactory _factory;

        private readonly ITransactionRuleService _transactionRuleService;   // WILL NEED TO DELETE IF NOT WORKING.
        public TransactionRepository(IDbConnectionFactory factory, ITransactionRuleService transactionRuleService)
        {
            _transactionRuleService = transactionRuleService;
            _factory = factory;
        }

        
        // Gets the total number of transaction records. MUST BE CALLED UPON EVERY FILTER CHANGE!!!
        public int GetTotalNoOfRecords(ulong userId, TransactionFilter transactionQueryFilter, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            var parameters = PrepareTransactionQueryParameters(userId, transactionQueryFilter, null, null);

            int totalNoOfRecords =  _conn.ExecuteScalar<int>("sp_pft_get_transactions_or_records_count",
                                          parameters,
                                          commandType: CommandType.StoredProcedure
                                         );

            return totalNoOfRecords;
            
        }

        
        // Gets the records between a sequence of supplied 'recStartNumber' and 'recEndNumber'
        public IEnumerable<TransactionDataModel>? GetTransactions(ulong userId, TransactionFilter transactionQueryFilter, int recStartNumber, int recEndNumber, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = PrepareTransactionQueryParameters(userId, transactionQueryFilter, recStartNumber, recEndNumber);
            
            /******************************************************************
            // To be removed after debug!!!

            parameters.ParameterNames.ToList().ForEach(n =>
                    Debug.WriteLine($"{n} = {parameters.Get<object>(n)}"));

            /******************************************************************/

            return _conn.Query<TransactionDataModel>("sp_pft_get_transactions_or_records_count",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }

        
        // Gets Single Transaction Record based on supplied transactionId
        public TransactionDataModel? GetTransaction(ulong userId, string transactionId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT CAST(t.transaction_id AS CHAR) AS TransactionId, " +
                                "t.transaction_date AS TransactionDate, " +
                                "t.transaction_description AS TransactionDescription, " +
                                "t.transaction_amount AS TransactionAmount, " +
                                "CAST(t.account_id AS CHAR) AS AccountId, " +
                                "CAST(t.category_id AS CHAR) AS CategoryId, " +
                                "CAST(t.goal_id AS CHAR) AS GoalId, " +
                                "CAST(t.tag_id AS CHAR) AS TagId, " +
                                "t.transaction_categorization AS TransactionCategorization, " +
                                "tr.transaction_repeat_id AS TransactionRepeatId, " +
                                "tr.is_transaction_repeat_active AS IsTransactionRepeatActive, " +
                                "tr.transaction_repeat_interval AS TransactionRepeatInterval, " +
                                "tr.transaction_repeat_end_date AS TransactionRepeatEndDate " +
                          "FROM pft_transactions t " +
                          "LEFT JOIN pft_trasactions_repeats tr " +
                          "  ON t.transaction_id = tr.transaction_id " +
                         "WHERE t.user_id = @UserId" +
                         "  AND t.transaction_id = @TransactionId;";

            return _conn.QuerySingle<TransactionDataModel>(sql, new { UserId = userId, TransactionId = ulong.Parse(transactionId) });
        }

        // Saves a single record in the transaction table (if it's a repeat tranasction then: also creates a new entry in the 'repeat' tables
        public bool SaveTransaction(TransactionDataModel transactionRecord, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("transaction_id", transactionRecord.TransactionId == "" ? 0 : ulong.Parse(transactionRecord.TransactionId));
            parameters.Add("user_id", transactionRecord.UserId);
            parameters.Add("transaction_date", transactionRecord.TransactionDate.ToString("yyyy-MM-dd"));
            parameters.Add("transaction_description", transactionRecord.TransactionDescription.Trim());
            parameters.Add("transaction_amount", transactionRecord.TransactionAmount);
            parameters.Add("account_id", ulong.Parse(transactionRecord.AccountId));
            parameters.Add("category_id", ulong.Parse(transactionRecord.CategoryId));
            parameters.Add("goal_id", transactionRecord.GoalId == "" ? null : ulong.Parse(transactionRecord.GoalId));
            parameters.Add("tag_id", transactionRecord.TagId == "" ? null :  ulong.Parse(transactionRecord.TagId));
            parameters.Add("transaction_categorization", transactionRecord.TransactionCategorization);   // 1 - Manual Entry,  2 - CSV uplod, 3 - Auto (Linked Bank Account)
            parameters.Add("transaction_type", transactionRecord.TransactionType);
            parameters.Add("transaction_repeat_id", transactionRecord.TransactionRepeatId);
            parameters.Add("transaction_repeat_interval", transactionRecord.TransactionRepeatInterval); 
            parameters.Add("transaction_repeat_end_date", transactionRecord.TransactionRepeatEndDate == null ? null : transactionRecord.TransactionRepeatEndDate?.ToString("yyyy-MM-dd")); 
            parameters.Add("is_transaction_repeat_active", transactionRecord.IsTransactionRepeatActive); 
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_transaction",
                            parameters,
                            commandType: CommandType.StoredProcedure
                            );
            
            return parameters.Get<int>("total_effected_records") > 0 ? true : false;

        }

        
        // Deletes a Transaction by supplied userId and transactionId
        public bool DeleteTransaction(ulong userId, string transactionId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("transaction_id", ulong.Parse(transactionId));
            parameters.Add("user_id", userId);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_transaction",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;

        }

        // Prepares the 'sp_pft_get_transactions_or_records_count' stored procedure parameters to get the transaction data or transaction data count for the current filter...
        private DynamicParameters PrepareTransactionQueryParameters(ulong userId, TransactionFilter transactionQueryFilter, int? recStartNumber, int? recEndNumber)
        {

            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("start_date", transactionQueryFilter.TransactionFilterStartDate.ToString("yyyy-MM-dd"));
            parameters.Add("end_date", transactionQueryFilter.TransactionFilterEndDate.ToString("yyyy-MM-dd"));
            parameters.Add("category_id", transactionQueryFilter.TransactionFilterCategoryId == null ? null : ulong.Parse(transactionQueryFilter.TransactionFilterCategoryId));
            parameters.Add("account_id", transactionQueryFilter.TransactionFilterAccountId == null ? null : ulong.Parse(transactionQueryFilter.TransactionFilterAccountId));
            parameters.Add("transaction_description", string.IsNullOrEmpty(transactionQueryFilter.TransactionFilterDescription) ? transactionQueryFilter.TransactionFilterDescription : transactionQueryFilter.TransactionFilterDescription.Trim());
            parameters.Add("tag_id", transactionQueryFilter.TransactionFilterTagId == null ? null : ulong.Parse(transactionQueryFilter.TransactionFilterTagId));
            parameters.Add("goal_id", transactionQueryFilter.TransactionFilterGoalId == null ? null : ulong.Parse(transactionQueryFilter.TransactionFilterGoalId));
            parameters.Add("transaction_categorization", transactionQueryFilter.TransactionFilterCategorization);
            parameters.Add("transaction_type", transactionQueryFilter.TransactionFilterType);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);
            // parameters.Add("total_records_count", direction: ParameterDirection.Output);

            return parameters;
        }

        // Gets the list of Categories, tags, accounts & goals
        public IEnumerable<ListOptionStringId> GetSelectOptionsDataFor(ulong userId, string selectFor, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string idColumn = "";
            string nameColumn = "";
            string tableName = "";
            string typeColumn = "";

            switch (selectFor)
            {

                case "accounts":
                    idColumn = "account_id";
                    nameColumn = "account_name";
                    tableName = "pft_accounts";
                    break;
                case "tags":
                    idColumn = "tag_id";
                    nameColumn = "tag_name";
                    tableName = "pft_tags";
                    break;
                case "goals":
                    idColumn = "goal_id";
                    nameColumn = "goal_name";
                    tableName = "pft_goals";
                    break;
                case "categories":
                    idColumn = "category_id";
                    nameColumn = "category_name";
                    typeColumn = "category_type";
                    tableName = "pft_categories";
                    break;

            }

            string sql = $"SELECT CAST(t.{idColumn} AS CHAR) AS listOptionId, t.{nameColumn} AS listOptionName{ (typeColumn != "" ? ", " + typeColumn + " AS listOptionType" : "" ) } FROM {tableName} t WHERE t.user_id = @UserId;";

            return _conn.Query<ListOptionStringId>(sql, new { UserId = userId });
        }


        // Gets the 'Uncategorized' category id for the current user
        public ulong GetDefaultCateogryId(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT c.category_id AS CategoryId " +
                           "FROM pft_categories c " +
                          "WHERE c.user_id = @UserId" +
                          "  AND c.category_name = 'Uncategorized';";

            return _conn.ExecuteScalar<ulong>(sql, new { UserId = userId });
        }


        // Returns ONLY CSV and Bank Transactions (for Rules Assignment). Manual entries are eliminiated!
        public IEnumerable<TransactionDataModel> GetCSVAndBankLinkTransactionsForUser(ulong userID, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT t.transaction_id AS TransactionId, " +
                                "t.transaction_date AS TransactionDate, " +
                                "t.transaction_description AS TransactionDescription, " +
                                "t.transaction_amount AS TransactionAmount, " +
                                "t.account_id AS AccountId, " +
                                "t.category_id AS CategoryId, " +
                                "t.goal_id AS GoalId, " +
                                "t.tag_id AS TagId, " +
                                "t.transaction_categorization AS TransactionCategorization, " +
                                "t.transaction_type AS TransactionType " +
                          "FROM pft_transactions t " +
                         "WHERE t.user_id = @UserId " +
                           "AND t.transaction_categorization <> 1";   // Eliminate manual entries (Banks and CSV only are updated!)

            return _conn.Query<TransactionDataModel>(sql, new { @UserId = userID });

        }

        // Updates the single transaction record that has been changed by rule
        public void UpdateTransactionRecordFromRule(ulong userID, TransactionDataModel transaction, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "UPDATE pft_transactions t " +
                            "SET t.category_id = @CategoryID, " +
                                "t.goal_id = @GoalID, " +
                                "t.transaction_description = @TransactionDescription " +
                         "WHERE t.user_id = @UserId " +
                           "AND t.transaction_id = @TransactionID";

            int totalNumberOfRecordsUpdated = _conn.Execute(sql, new { UserId = userID, CategoryID = transaction.CategoryId, GoalID = transaction.GoalId, TransactionDescription = transaction.TransactionDescription, TransactionID = transaction.TransactionId });
        }


        // Complete
        // For the each record (as list item of string array), it would create a transaction record, applies the rules and validates against Attributes.
        // If validation fails for any record, none of the record will be inserted and error will be retruned to client with details.
        // If rule matches then it would be applied, otherwise record will have default category!
        public (List<ValidationResult>? modelValidationResults, List<TransactionDataModel>? transactions) GenerateTransactionRecordsWithRulesApplied(ulong userId, BulkImportTemplate bulkImportTemplate, List<string[]> transactionDataList, string defaultCategoryId, byte transactionCategorization, string? connString)
        {

            var transactionsAsList = new List<TransactionDataModel>();

            var modelValidationResults = new List<ValidationResult>();

            // To get the category type
            var categoriesList = GetSelectOptionsDataFor(userId, "categories", connString);

            for (int i = bulkImportTemplate.HeaderRowIndex + 1; i < transactionDataList.Count; i++)
            {
                
                // Import Values from String Array

                string originalDescription = transactionDataList[i][bulkImportTemplate.DescriptionFieldIndex].Trim();
                string originalDate = transactionDataList[i][bulkImportTemplate.DateFieldIndex];

                (bool isRuleApplied, TransactionDataModel transaction) transactionForRuleApplication = ( false, new TransactionDataModel() );
                transactionForRuleApplication.transaction.TransactionDate = DateOnly.ParseExact( originalDate, ["MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "M/d/yy"], CultureInfo.InvariantCulture);
                transactionForRuleApplication.transaction.TransactionDescription = originalDescription;

                transactionForRuleApplication.transaction.TransactionAmount = double.Parse((transactionDataList[i][bulkImportTemplate.AmountFieldIndex].ToString().Trim()) != "" ? transactionDataList[i][bulkImportTemplate.AmountFieldIndex].Trim() :
                     (transactionDataList[i][bulkImportTemplate.DebitFieldIndex].Trim() != "" ? transactionDataList[i][bulkImportTemplate.DebitFieldIndex].Trim() : transactionDataList[i][bulkImportTemplate.CreditFieldIndex].Trim()));

                transactionForRuleApplication.transaction.AccountId = bulkImportTemplate.AccountId;
                transactionForRuleApplication.transaction.CategoryId = defaultCategoryId;
                transactionForRuleApplication.transaction.GoalId = null;
                transactionForRuleApplication.transaction.TagId = null;
                transactionForRuleApplication.transaction.TransactionCategorization = transactionCategorization;  // Can be CSV or Bank!
                transactionForRuleApplication.transaction.TransactionType = 1;    // Regular

                // Apply Existing Rules (Get Category and (optional)Goal)
                transactionForRuleApplication = _transactionRuleService.ApplyAllRulesToTransaction(userId, transactionForRuleApplication.transaction, connString);

                transactionForRuleApplication.transaction.CategoryType = categoriesList.First(c => c.ListOptionId == transactionForRuleApplication.transaction.CategoryId).ListOptionType.ToString();

                // Validate model!
                var context = new ValidationContext(transactionForRuleApplication.transaction, serviceProvider: null, items: null);

                // validateAllProperties: true ensures every property is checked against its attributes
                bool isValid = Validator.TryValidateObject(transactionForRuleApplication.transaction, context, modelValidationResults, validateAllProperties: true);

                if (!isValid)
                {
                    // Add the record detail at the end
                    modelValidationResults.Add(new ValidationResult($"ERROR OCCURED FOR THE RECORD: {originalDate}, {originalDescription}...when '{categoriesList.First(c => c.ListOptionId == transactionForRuleApplication.transaction.CategoryId).ListOptionName}' category applied from rule!"));
                    return (modelValidationResults, null);
                }

                // Even if any rule is not applied, it should/will be inserted into the database with default category_id.
                transactionsAsList.Add(transactionForRuleApplication.transaction);
            }

            return (null, transactionsAsList);

        }

        // Complete
        // Inserts transaction record(s) from CSV or Linked Bank Account!!!
        // transactionCategorization: 2 = CSV upload , 3 = auto (Bank) 
        public bool BulkUploadTransactions(ulong userId, IEnumerable<TransactionDataModel> transactions, string? connString)
        {

            int rowsInserted = 0;

            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "INSERT INTO pft_transactions (transaction_id, user_id, transaction_date, transaction_description, transaction_amount, account_id, category_id, goal_id, tag_id, transaction_categorization, transaction_type) " +
                          "VALUES ";

            sql = string.Concat(sql, PrepareValuesStatementForBulkInsert(userId, transactions, connString));

            rowsInserted = _conn.Execute(sql);

            return rowsInserted > 0 ? true : false;

        }

        // Complete
        // Prepares the Values statement for INSERT from CSV or Linked Bank Account data!!!
        private string PrepareValuesStatementForBulkInsert(ulong userId, IEnumerable<TransactionDataModel> transactionData, string? connString)
        {
                       
            StringBuilder sb = new StringBuilder();
            
            foreach (var rec in transactionData)
            {
                
                rec.GoalId = rec.GoalId == null ? "NULL" : $"'{rec.GoalId}'";
                rec.TagId = rec.TagId == null ? "NULL" : $"'{rec.TagId}'";

                string currentRecordAsValuesStatement = $"(GeneratePrefixedId(3), {userId}, '{rec.TransactionDate.ToString("yyyy-MM-dd")}', '{rec.TransactionDescription.Replace("\\", "\\\\").Replace("\'", "\\'").Replace("\"", "\\\"")}', '{rec.TransactionAmount}', '{rec.AccountId}', '{rec.CategoryId}', {rec.GoalId}, {rec.TagId}, '{rec.TransactionCategorization}', '{rec.TransactionType}'),";
                sb.Append(currentRecordAsValuesStatement);

            }
            
            string result = sb.ToString().Trim();

            return result.Substring(0, result.Length - 1);

        }


        
    }
}
