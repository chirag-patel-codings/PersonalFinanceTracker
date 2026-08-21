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

namespace PersonalFinanceTracker.Services.Repository
{
    public class RecurringRepository : IRecurringRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public RecurringRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        
        // Gets the total number of (transaction) recurring records.
        public int GetTotalNoOfRecords(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT COUNT(trs.transaction_recurring_id) " +
                           "FROM pft_recurring_schedules trs " +
                           "JOIN pft_trasactions_repeats tr " +
                             "ON trs.transaction_repeat_id = tr.transaction_repeat_id " +
                          "WHERE tr.user_id = @UserId";

            return _conn.ExecuteScalar<int>(sql, new { UserId = userId });

        }

        
        // Gets the records between a sequence of supplied 'recStartNumber' and 'recEndNumber'
        public IEnumerable<Recurring>? GetRecurrings(ulong userId, int recStartNumber, int recEndNumber, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);

            return _conn.Query<Recurring>("sp_pft_get_transaction_recurrings",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }

        
        // Gets Single Recurring Record based on supplied transactionId
        public Recurring? GetRecurring(ulong userId, string transactionRecurringId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT CAST(trs.transaction_recurring_id AS CHAR) AS RecurringId, " +
                                "trs.recurring_next_date AS RecurringNextDate, " +
                                "trs.recurring_description AS RecurringDescription, " +
                                "trs.recurring_amount AS RecurringAmount, " +
                                "CAST(trs.account_id AS CHAR) AS AccountId, " +
                                "CAST(trs.category_id AS CHAR) AS CategoryId, " +
                                "CAST(trs.goal_id AS CHAR) AS GoalId, " +
                                "CAST(trs.tag_id AS CHAR) AS TagId, " +
                                "trs.transaction_categorization AS TransactionCategorization, " +
                                "tr.transaction_repeat_id AS TransactionRepeatId, " +
                                "tr.is_transaction_repeat_active AS IsTransactionRepeatActive, " +
                                "tr.transaction_repeat_interval AS TransactionRepeatInterval, " +
                                "tr.transaction_repeat_end_date AS TransactionRepeatEndDate " +
                          "FROM pft_recurring_schedules trs " +
                          "JOIN pft_trasactions_repeats tr " +
                          "  ON trs.transaction_repeat_id = tr.transaction_repeat_id " +
                         "WHERE tr.user_id = @UserId" +
                         "  AND trs.transaction_recurring_id = @RecurringId;";

            return _conn.QuerySingle<Recurring>(sql, new { UserId = userId, RecurringId = ulong.Parse(transactionRecurringId) });
        }

        // Saves a single record in the Transaction Repeats & Recurring Schedules table
        public bool SaveRecurring(Recurring transactionRecurringRecord, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("transaction_recurring_id", transactionRecurringRecord.RecurringId == "" ? 0 : ulong.Parse(transactionRecurringRecord.RecurringId));
            parameters.Add("user_id", transactionRecurringRecord.UserId);
            parameters.Add("recurring_next_date", transactionRecurringRecord.RecurringNextDate?.ToString("yyyy-MM-dd"));
            parameters.Add("recurring_description", transactionRecurringRecord.RecurringDescription.Trim());
            parameters.Add("recurring_amount", transactionRecurringRecord.RecurringAmount);
            parameters.Add("account_id", ulong.Parse(transactionRecurringRecord.AccountId));
            parameters.Add("category_id", ulong.Parse(transactionRecurringRecord.CategoryId));
            parameters.Add("goal_id", transactionRecurringRecord.GoalId == "" ? null : ulong.Parse(transactionRecurringRecord.GoalId));
            parameters.Add("tag_id", transactionRecurringRecord.TagId == "" ? null :  ulong.Parse(transactionRecurringRecord.TagId));
            parameters.Add("transaction_categorization", transactionRecurringRecord.TransactionCategorization);   // 1 - Manual Entry,  2 - CSV uplod, 3 - Auto (Linked Bank Account)
            parameters.Add("transaction_repeat_id", transactionRecurringRecord.TransactionRepeatId);
            parameters.Add("transaction_repeat_interval", transactionRecurringRecord.TransactionRepeatInterval); 
            parameters.Add("transaction_repeat_end_date", transactionRecurringRecord.TransactionRepeatEndDate == null ? null : transactionRecurringRecord.TransactionRepeatEndDate?.ToString("yyyy-MM-dd")); 
            parameters.Add("is_transaction_repeat_active", transactionRecurringRecord.IsTransactionRepeatActive); 
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            

            _conn.Execute("sp_pft_save_transaction_recurring",
                            parameters,
                            commandType: CommandType.StoredProcedure
                            );
            
            return parameters.Get<int>("total_effected_records") > 0 ? true : false;

        }

        
        // Deletes a Recurring by supplied userId and transactionId
        public bool DeleteRecurring(ulong userId, string transactionRecurringId, bool deleteSeries, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("transaction_recurring_id", ulong.Parse(transactionRecurringId));
            parameters.Add("user_id", userId);
            parameters.Add("delete_series", deleteSeries);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_recurring",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;

        }


    }
}
