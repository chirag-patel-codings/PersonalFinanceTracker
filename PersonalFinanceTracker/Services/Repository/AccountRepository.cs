using Dapper;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace PersonalFinanceTracker.Services.Repository
{
    public class AccountRepository : IAccountRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public AccountRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        // Gets the total number of account records
        public int GetTotalNoOfRecords(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT COUNT(account_id) FROM pft_accounts WHERE user_id = @UserId";

            return _conn.ExecuteScalar<int>(sql, new { UserId = userId });
        }


        // Gets Single Account Record based on supplied accountId
        public Account? GetAccount(ulong userId, string accountId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT CAST(a.account_id AS CHAR) AS AccountId, " +
                                "a.account_name AS AccountName, " +
                                "a.account_institution_name AS AccountInstitutionName, " +
                                "a.account_type_id AS AccountTypeId, " +
                                "a.account_description AS AccountDescription, " +
                                "a.is_a_linked_account AS IsALinkedAccount " +
                          "FROM pft_accounts a " +
                         "WHERE a.user_id = @UserId" +
                         "  AND a.account_id = @AccountId;";

            return _conn.QuerySingle<Account>(sql, new { UserId = userId, AccountId = ulong.Parse(accountId) });
        }


        // Gets the records between a sequence of supplied 'recStartNumber' and 'recEndNumber'
        public IEnumerable<Account>? GetAccounts(ulong userId, int recStartNumber, int  recEndNumber, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);

            return _conn.Query<Account>("sp_pft_get_accounts",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }

        
        // Saves a single account record
        public bool SaveAccount(Account account, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("account_id", account.AccountId == "" ? 0 : ulong.Parse(account.AccountId));
            parameters.Add("user_id", account.UserId);
            parameters.Add("account_name", account.AccountName);
            parameters.Add("account_institution_name", account.AccountInstitutionName);
            parameters.Add("account_type_id", account.AccountTypeId);
            parameters.Add("account_description", account.AccountDescription);
            parameters.Add("is_a_linked_account", account.IsALinkedAccount);
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_account",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_effected_records") > 0 ? true : false;

        }

        // Deletes a Account by supplied userId and accountId
        public bool DeleteAccount(ulong userId, string accountId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("account_id", ulong.Parse(accountId));
            parameters.Add("user_id", userId);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_account",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;

        }

        // Returns a list of all standardize Categories
        public IEnumerable<AccountType> GetAccountTypes(string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT account_type_id AS AccountTypeId, " +
                            "account_type_classification AS AccountTypeClassification, " +
                            "account_type_name AS AccountTypeName, " +
                            "account_type_description AS AccountTypeDescription " +
                            "FROM pft_account_types";

            return _conn.Query<AccountType>(sql);

        }

    }
}
