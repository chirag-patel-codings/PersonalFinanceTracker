using Dapper;
using MySqlX.XDevAPI.Common;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Ocsp;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;

using TransactionDataModel = PersonalFinanceTracker.Models.Transaction;

namespace PersonalFinanceTracker.Services.Repository
{
    public class TransactionRepository : ITransactionRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public TransactionRepository(IDbConnectionFactory factory)
        {
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
            parameters.Add("transaction_description", transactionQueryFilter.TransactionFilterDescription);
            parameters.Add("tag_id", transactionQueryFilter.TransactionFilterTagId == null ? null : ulong.Parse(transactionQueryFilter.TransactionFilterTagId));
            parameters.Add("goal_id", transactionQueryFilter.TransactionFilterGoalId == null ? null : ulong.Parse(transactionQueryFilter.TransactionFilterGoalId));
            parameters.Add("transaction_categorization", transactionQueryFilter.TransactionFilterCategorization);
            parameters.Add("transaction_type", transactionQueryFilter.TransactionFilterType);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);
            // parameters.Add("total_records_count", direction: ParameterDirection.Output);

            return parameters;
        }


        // Inserts transaction record(s) from CSV or Linked Bank Account!!!
        public bool BulkUploadTransactions(ulong userId, IEnumerable<TransactionDataModel> transaction, string? connString)
        {
            int rowsInserted = 0;

            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);


            string sql = "INSERT INTO pft_transactions (transaction_id, user_id, transaction_date, transaction_description, transaction_amount, account_id, category_id, goal_id, tag_id, transaction_categorization, transaction_type) " +
                          "VALUES ";

            sql = string.Concat(sql, PrepareValuesStatementForInsert(userId, transaction));

            rowsInserted = _conn.Execute(sql);

            return rowsInserted > 0 ? true : false;

        }

        // Prepares the Values statement for INSERT from CSV or Linked Bank Account data!!!
        private string PrepareValuesStatementForInsert(ulong userId, IEnumerable<TransactionDataModel> transactionData)
        {
            
            StringBuilder sb = new StringBuilder();

            foreach (var rec in transactionData)
            {
                string currentRecordAsValuesStatement = $"(GeneratePrefixedId(3), {userId}, '{rec.TransactionDate:yyyy-MM-dd}', QUOTE({rec.TransactionDescription}), {rec.TransactionAmount}, {rec.AccountId}, {rec.CategoryId}, {rec.GoalId}, {rec.TagId}, {rec.TransactionCategorization}, {rec.TransactionType}),";
                sb.Append(currentRecordAsValuesStatement);
            }
            
            string result = sb.ToString().Trim();

            result = result.Substring(0, result.Length - 1);

            return result;
        }


        // Gets the list of Categories
        public IEnumerable<ListOptionStringId> GetSelectOptionsDataFor(ulong userId, string selectFor, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string idColumn = "";
            string nameColumn = "";
            string tableName = "";

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
                default:
                    idColumn = "category_id";
                    nameColumn = "category_name";
                    tableName = "pft_categories";
                    break;

            }

            string sql = $"SELECT CAST(t.{idColumn} AS CHAR) AS listOptionId, t.{nameColumn} AS listOptionName FROM {tableName} t WHERE t.user_id = @UserId;";
            
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
    }
}
