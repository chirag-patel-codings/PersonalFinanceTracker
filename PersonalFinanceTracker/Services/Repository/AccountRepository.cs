using Dapper;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using System.Text.RegularExpressions;
using ZstdSharp.Unsafe;

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

            string sql = "SELECT COUNT(account_id) FROM pft_accounts WHERE user_id = @UserId AND plaid_is_account_active = '1'";

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
            parameters.Add("account_id", string.IsNullOrEmpty(account.AccountId) ? 0 : ulong.Parse(account.AccountId));
            parameters.Add("user_id", account.UserId);
            parameters.Add("account_name", account.AccountName.Trim());
            parameters.Add("account_institution_name", account.AccountInstitutionName);
            parameters.Add("account_type_id", account.AccountTypeId);
            parameters.Add("account_description", string.IsNullOrEmpty(account.AccountDescription) ? account.AccountDescription : account.AccountDescription.Trim());
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


        // dataMode = "Add": Saves all the account(s), selected by the user when selecting institution from Plaid at once.
        // dataMode = "Update" + "Delete": Deletes all the exsiting data for institute first and then adds the new data.
        public bool SaveLinkedAccountsFromExternalInstitute (List<Account> accounts, string? dataMode, string? connString)
        {

            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            List<Account> existingAccounts = new List<Account>();
            List<Account> newAccounts = new List<Account>();

            // Set the is-active flag to inactive...This way the existing data would remain in related tables...
            string sql = "";
            using (var transaction = _conn.BeginTransaction())
            {

                sql = $"{sql} UPDATE pft_accounts SET plaid_is_account_active = '0' WHERE user_id = '{accounts[0].UserId}' AND plaid_access_token = '{accounts[0].PlaidAccessToken}' AND plaid_item_id = '{accounts[0].PlaidItemId}' AND is_a_linked_account = '1';";   // ALL THE LINKED ACCOUNTS: AND plaid_access_token = '{accounts[0].PlaidAccessToken}' AND plaid_item_id = '{accounts[0].PlaidItemId}'

                try
                {
                    _conn.Execute(sql, transaction: transaction);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return false;
                }
                
                // Re-Active the existing accounts if the user has kept:
                foreach (Account account in accounts)
                {
                    // Plaid returns different access_token and item_id for the account from same institute...So re-activate only if all the details are same...otherwise create a new account!!!
                    sql = $"SELECT 1 FROM pft_accounts WHERE user_id = '{account.UserId}' AND plaid_institution_id = '{account.PlaidInstitutionId}' AND account_institution_name = '{account.AccountInstitutionName}' AND account_name = '{account.AccountName}' AND account_type_id = '{account.AccountTypeId}'; ";
                    int? accountExists = _conn.ExecuteScalar<int?>(sql);

                    if (accountExists == null)
                    {
                        newAccounts.Add(account);
                    }
                    else
                    {
                        existingAccounts.Add(account);
                    }
                }


                // Update: plaid_is_account_active, plaid_access_token, plaid_item_id, plaid_account_id
                if (existingAccounts.Count > 0)
                {

                    sql = "";
                    foreach (Account account in existingAccounts)
                    {
                        sql = $"{sql} UPDATE pft_accounts SET plaid_is_account_active = '{(dataMode == "delete" ? 0 : 1)}', plaid_access_token = '{account.PlaidAccessToken}', plaid_item_id = '{account.PlaidItemId}', plaid_account_id = '{account.PlaidAccountId}' WHERE user_id = '{account.UserId}' AND plaid_institution_id = '{account.PlaidInstitutionId}' AND account_institution_name = '{account.AccountInstitutionName}' AND account_name = '{account.AccountName}' AND account_type_id = '{account.AccountTypeId}';";
                    }

                    try
                    {
                        _conn.Execute(sql, transaction: transaction);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return false;
                    }

                }

                // Add with plaid_is_account_active = 1 for "Update" + "Add" and plaid_is_account_active = 0 for "Delete"
                if (newAccounts.Count > 0)
                {

                    sql = "INSERT INTO pft_accounts (account_id, user_id, account_name, account_institution_name, account_type_id, account_description, is_a_linked_account, plaid_access_token, plaid_item_id, plaid_institution_id, plaid_account_id, plaid_is_account_active) VALUES ";

                    string valuesText = "";
                    foreach (Account account in newAccounts)
                    {
                        valuesText = $"{valuesText} (GeneratePrefixedId(2), '{account.UserId}', '{account.AccountName}', '{account.AccountInstitutionName}', '{account.AccountTypeId}', '{account.AccountDescription}', '{account.IsALinkedAccount}', '{account.PlaidAccessToken}', '{account.PlaidItemId}', '{account.PlaidInstitutionId}', '{account.PlaidAccountId}', '{(dataMode == "delete" ? 0 : 1)}'),";
                    }

                    valuesText = valuesText.Trim();
                    valuesText = valuesText.Substring(0, valuesText.Length - 1);

                    sql = sql + valuesText;

                    try
                    {
                        _conn.Execute(sql, transaction: transaction);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return false;
                    }

                }

                transaction.Commit();
            }

            return true;

        }


        // Retruns the Plaid Permanent Access Token for the given userId and accountId
        public string? GetPlaidPermanentAccessToken(ulong userId, string? accountId, string? connString)
        {

            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT plaid_access_token FROM pft_accounts WHERE user_id = @UserId AND account_id = @AccountId";

            return _conn.QuerySingleOrDefault<string?>(sql, new { UserId = userId, AccountId = ulong.Parse(accountId) });

        }

    }
}
