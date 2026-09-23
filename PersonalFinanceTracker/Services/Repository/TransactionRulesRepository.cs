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
    public class TransactionRuleRepository : ITransactionRuleRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public TransactionRuleRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        // Completed
        // Gets the total number of (transaction) recurring records.
        public int GetTotalNoOfRecords(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT COUNT(tr.transctions_rule_id) " +
                           "FROM pft_transactions_rules tr " +
                          "WHERE tr.user_id = @UserId";

            return _conn.ExecuteScalar<int>(sql, new { UserId = userId });

        }

        // Completed
        // Gets the records between a sequence of supplied 'recStartNumber' and 'recEndNumber'
        public IEnumerable<TransactionRule>? GetTransactionRules(ulong userId, int recStartNumber, int recEndNumber, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);

            return _conn.Query<TransactionRule>("sp_pft_get_transaction_rules",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }

        // Completed
        // Gets Single TransactionRules Record based on supplied transactionId
        public TransactionRule? GetTransactionRule(ulong userId, string transactionRuleId, string? connString)
        {

            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT CAST(tr.transctions_rule_id AS CHAR) AS TransactionRuleId, " +
                                "CAST(tr.account_id AS CHAR) AS AccountId, " +
                                "CAST(tr.category_id AS CHAR) AS CategoryId, " +
                                "CAST(tr.goal_id AS CHAR) AS GoalId, " +
                                "tr.tr_text_to_match AS TransactionTextToMatch, " +
                                "tr.tr_text_match_comparison AS TransactionTextMatchComparision, " +
                                "tr. tr_amount_match_comparison AS TransactionAmountMatchComparision, " + 
                                "tr.tr_exact_amount AS TransactionExactAmount, " +
                                "tr.tr_description_override AS TransactionDescriptionOverride " +
                          "FROM pft_transactions_rules tr " +
                         "WHERE tr.user_id = @UserId" +
                         "  AND tr.transctions_rule_id = @TransactionRulesId;";

            return _conn.QuerySingle<TransactionRule>(sql, new { UserId = userId, TransactionRulesId = ulong.Parse(transactionRuleId) });

        }

        // Completed
        // Saves a single record in the Transaction Repeats & TransactionRules Schedules table
        public bool SaveTransactionRule(TransactionRule transactionRulesRecord, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("transctions_rule_id", transactionRulesRecord.TransactionRuleId == "" ? 0 : ulong.Parse(transactionRulesRecord.TransactionRuleId));
            parameters.Add("user_id", transactionRulesRecord.UserId);
            parameters.Add("account_id", transactionRulesRecord.AccountId == "" ? null : ulong.Parse(transactionRulesRecord.AccountId));
            parameters.Add("category_id", ulong.Parse(transactionRulesRecord.CategoryId));
            parameters.Add("goal_id", transactionRulesRecord.GoalId == "" ? null : ulong.Parse(transactionRulesRecord.GoalId));
            parameters.Add("tr_text_to_match", transactionRulesRecord.TransactionTextToMatch.Trim());   
            parameters.Add("tr_text_match_comparison", transactionRulesRecord.TransactionTextMatchComparision);
            parameters.Add("tr_amount_match_comparison", transactionRulesRecord.TransactionAmountMatchComparision); 
            parameters.Add("tr_exact_amount", transactionRulesRecord.TransactionExactAmount); 
            parameters.Add("tr_description_override", string.IsNullOrEmpty(transactionRulesRecord.TransactionDescriptionOverride) ? transactionRulesRecord.TransactionDescriptionOverride : transactionRulesRecord.TransactionDescriptionOverride.Trim()); 
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_transctions_rules",
                            parameters,
                            commandType: CommandType.StoredProcedure
                            );
            
            return parameters.Get<int>("total_effected_records") > 0 ? true : false;

        }

        // completed
        // Deletes a TransactionRules by supplied userId and transactionId
        public bool DeleteTransactionRule(ulong userId, string transactionRulesId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("transctions_rule_id", ulong.Parse(transactionRulesId));
            parameters.Add("user_id", userId);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_transactions_rule",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;

        }


    }
}
