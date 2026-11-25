using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.BulkUpload;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Numerics;
using System.Security.Principal;
using System.Text.Json;

namespace PersonalFinanceTracker.Services.Plaid
{
    public class PlaidService : IPlaidService
    {

        private readonly IDbConnectionFactory _factory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBulkUploadService _bulkUploadService;
        private readonly IConfiguration _config;

        private readonly IAccountRepository _accountRepository;

        public PlaidService(IDbConnectionFactory factory, IHttpClientFactory httpClientFactory, IAccountRepository accountRepository, IBulkUploadService bulkUploadService, IConfiguration config) 
        {
            _factory = factory;
            _httpClientFactory = httpClientFactory;
            _accountRepository = accountRepository;
            _bulkUploadService = bulkUploadService;
            _config = config;
        }

        // (Acts for the Linked Accounts created at the same time - (they will have same plaid-access-token & plaid-item-id)):
        // dataMode 
        // "Add":       Pre-Linked Same Account will be deleted and will start as fresh new account. Any other new account(s) will be start as new fresh account(s).
        // "Edit":      Selected Pre-Linked Same Account & it's related details will remain as is. Non-Selected Pre-Linked Same Account & it's related details will be deleted.
        //              Selected Non-Prelinked New Account will be created and start as fresh.
        // "Delete":    Selected Pre-Linked Same Account & it's related details will remain as is. Non-Selected Pre-Linked Account & it's related details will be deleted.
        //              Selected Non-Prelinked Account will NOT be created.
        public bool SaveLinkedAccountsFromExternalInstitute(ulong userId, List<Account> accountsListFromPlaid, string? dataMode, string? connString)
        {

            connString = connString ?? "pft_con_str";
            IDbConnection _conn = _factory.GetDBConnection(connString);

            List<Account> unlinkedAccounts = new List<Account>();
            List<Account> newAccounts = new List<Account>();

            string sql = "";
            using (var transaction = _conn.BeginTransaction())
            {

                var userLinkedAccountsForInstitute = GetPlaidActiveAccountsForUser(userId, connString).Where(a => a.PlaidInstitutionId == accountsListFromPlaid[0].PlaidInstitutionId);

                // Confirms the new accounts for the Particular Institution
                foreach (var accountFromPlaid in accountsListFromPlaid) 
                {
                    // u.PlaidAccessToken == accountFromPlaid.PlaidAccessToken && u.PlaidItemId == accountFromPlaid.PlaidItemId (NOT TO BE CONSIDERED HERE)
                    var accountAlreadyExists = userLinkedAccountsForInstitute.FirstOrDefault(u => u.PlaidInstitutionId == accountFromPlaid.PlaidInstitutionId && u.AccountName == accountFromPlaid.AccountName && u.PlaidAccountMask == accountFromPlaid.PlaidAccountMask && u.PlaidAccountSubtype == accountFromPlaid.PlaidAccountSubtype);
                    if (accountAlreadyExists == null)
                    {
                        var accountCreatedWithSamePermanentAccessToken = userLinkedAccountsForInstitute.FirstOrDefault(u => u.PlaidAccessToken == accountFromPlaid.PlaidAccessToken && u.PlaidItemId == accountFromPlaid.PlaidItemId && u.PlaidNextCursor != null);
                        if (accountCreatedWithSamePermanentAccessToken != null)
                        {
                            accountFromPlaid.PlaidNextCursor = accountCreatedWithSamePermanentAccessToken.PlaidNextCursor;
                        }
                        newAccounts.Add(accountFromPlaid);
                    }
                    else
                    {
                        if(dataMode == "add")
                        {
                            unlinkedAccounts.Add(accountAlreadyExists);
                            newAccounts.Add(accountFromPlaid);
                        }
                    }
                }

                
                // Confirms the Unlinked Accounts By User for the Particular Institution
                foreach (var existingLinkedAccount in userLinkedAccountsForInstitute)
                {
                    var accountExists = accountsListFromPlaid.FirstOrDefault(p => p.PlaidInstitutionId == existingLinkedAccount.PlaidInstitutionId && p.AccountName == existingLinkedAccount.AccountName && p.PlaidAccountMask == existingLinkedAccount.PlaidAccountMask && p.PlaidAccountSubtype == existingLinkedAccount.PlaidAccountSubtype);
                    if(accountExists == null && dataMode != "add")
                    {
                        // Account will be retained if they were created at different time. (they will have different plaid-access-token & plaid-item-id).  
                        if (existingLinkedAccount.PlaidAccessToken == accountsListFromPlaid[0].PlaidAccessToken && existingLinkedAccount.PlaidItemId == accountsListFromPlaid[0].PlaidItemId)
                        {
                            unlinkedAccounts.Add(existingLinkedAccount);
                        }
                    }
                }

                // Add new accounts
                if (newAccounts.Count > 0 && dataMode != "delete")
                {

                    sql = "INSERT INTO pft_accounts (account_id, user_id, account_name, account_institution_name, account_type_id, account_description, is_a_linked_account, plaid_access_token, plaid_item_id, plaid_institution_id, plaid_account_id, plaid_is_account_active, plaid_persistent_account_id, plaid_account_mask, plaid_account_subtype, plaid_next_cursor) VALUES ";

                    string valuesText = "";
                    foreach (Account account in newAccounts)
                    {
                        valuesText = $"{valuesText} (GeneratePrefixedId(2), '{account.UserId}', '{account.AccountName}', '{account.AccountInstitutionName}', '{account.AccountTypeId}', '{account.AccountDescription}', '{account.IsALinkedAccount}', '{account.PlaidAccessToken}', '{account.PlaidItemId}', '{account.PlaidInstitutionId}', '{account.PlaidAccountId}', '{(dataMode == "delete" ? 0 : 1)}', {(account.PlaidPersistentAccountId == null ? "null" : "'" + account.PlaidPersistentAccountId + "'")}, {(account.PlaidAccountMask == null ? "null" : "'" + account.PlaidAccountMask + "'")}, {(account.PlaidAccountSubtype == null ? "null" : "'" + account.PlaidAccountSubtype + "'")}, {(account.PlaidNextCursor == null ? "null" : "'" + account.PlaidNextCursor + "'")} ),";
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

                // Remove Account & It's Related Information for Un-Linked Accounts
                foreach (var unlinkedAccount in unlinkedAccounts)
                {
                    try
                    {

                        var parameters = new DynamicParameters();
                        parameters.Add("account_id", ulong.Parse(unlinkedAccount.AccountId));
                        parameters.Add("user_id", userId);
                        parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

                        _conn.Execute("sp_pft_delete_account",
                                       parameters,
                                       commandType: CommandType.StoredProcedure,
                                       transaction: transaction
                                      );
                        
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
            IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT plaid_access_token FROM pft_accounts WHERE user_id = @UserId AND account_id = @AccountId";

            return _conn.QuerySingleOrDefault<string?>(sql, new { UserId = userId, AccountId = ulong.Parse(accountId) });

        }


        // Returns the list of Plaid's active accounts for the user
        public IEnumerable<Account?> GetPlaidActiveAccountsForUser(ulong userId, string? connString)
        {

            connString = connString ?? "pft_con_str";
            IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT account_id AS AccountId, " +
                                "user_id AS UserId, " +
                                "account_name AS AccountName, " +
                                "account_institution_name AS AccountInstitutionName, " +
                                "account_type_id AS AccountTypeId, " +
                                "account_description AS AccountDescription, " +
                                "is_a_linked_account AS IsALinkedAccount, " +
                                "plaid_access_token AS PlaidAccessToken, " +
                                "plaid_item_id AS PlaidItemId, " +
                                "plaid_institution_id AS PlaidInstitutionId, " +
                                "plaid_account_id AS PlaidAccountId, " +
                                "plaid_persistent_account_id AS PlaidPersistentAccountId, " +
                                "plaid_account_mask AS PlaidAccountMask, " +
                                "plaid_account_subtype AS PlaidAccountSubtype, " +
                                "plaid_next_cursor AS PlaidNextCursor " +
                         "FROM pft_accounts WHERE user_id = @UserId AND is_a_linked_account = '1' AND plaid_is_account_active = '1'";

            return _conn.Query<Account?>(sql, new { UserId = userId });
        }


        // Performs the plaid's account details up-to-date in database.
        public void PlaidAccountReconsilation(ulong userId, string plaidItemId, string plaidInstitutionId, string plaidAccountId, string plaidAccountName, string plaidAccountMask, string accountTypeText, string plaidAccountSubtype, string? connString)
        {

            connString = connString ?? "pft_con_str";
            IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("plaid_item_id", plaidItemId, DbType.AnsiString);
            parameters.Add("plaid_institution_id", plaidInstitutionId, DbType.AnsiString);
            parameters.Add("plaid_account_id", plaidAccountId, DbType.AnsiString);
            parameters.Add("plaid_account_name", plaidAccountName, DbType.AnsiString);
            parameters.Add("plaid_account_mask", plaidAccountMask, DbType.AnsiString);
            parameters.Add("account_type_text", accountTypeText, DbType.AnsiString);
            parameters.Add("plaid_account_subtype", plaidAccountSubtype, DbType.AnsiString);

            _conn.Execute("sp_pft_plaid_accounts_reconsilation",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }


        // Returns the Last Stored Plaid's Next Cursor Value. 
        // Next Cursor value will always be the same for the single permanent access token... 
        public string? GetPlaidNextCursorValue(ulong userId, string plaidItemId, string? connString)
        {

            connString = connString ?? "pft_con_str";
            IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = $"SELECT plaid_next_cursor FROM pft_accounts WHERE user_id = '{userId}' AND plaid_item_id = '{plaidItemId}' LIMIT 1;";

            return _conn.ExecuteScalar<string?>(sql);

        }

        
        // Creates a new "Temporary Token" in Plaid or if the existing Permanent Access Token is supplied then makes the accounts selectable to user.
        // User can check or un-check the accounts in selection to sync / stop syncing data from that account!
        public async Task<IActionResult> CreatePlaidLinkTokenAsync(ulong userId, string? existingAccessToken = null)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(_config["Plaid:BaseURI"]);

            // Build the base payload required for ALL link tokens
            var payload = new Dictionary<string, object>
            {
                { "client_id", _config["Plaid:ClientId"] },
                { "secret", _config["Plaid:Secret"] },
                { "client_name", _config["Plaid:ClientName"] },
                { "country_codes", new[] { "US" } },
                { "language", "en" },
                { "user", new { client_user_id = userId.ToString() } },
                { "products", new[] { "transactions" } },
                { "redirect_uri", _config["Plaid:RedirectUri"] }
            };

            // If an existing access token is provided, add the Update Mode properties!
            if (!string.IsNullOrEmpty(existingAccessToken))
            {
                payload.Add("access_token", existingAccessToken);
                payload.Add("update", new { account_selection_enabled = true });
            }

            // Send request to Plaid
            var response = await client.PostAsJsonAsync("/link/token/create", payload);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
                string linkToken = jsonResponse.GetProperty("link_token").GetString();
                return new OkObjectResult(new { link_token = linkToken });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Plaid API Error: {errorContent}");

        }


        // DELETE THE EXISTING BANK (ALL THE ACCOUNTS) FROM PLAID DIRECTLY (NOT USED BUT KEPT FOR THE FUTURE ENDEAVORS)
        public async Task<bool> DeletePlaidItemAsync(string plaidAccessToken)
        {

            using var client = new HttpClient();
            client.BaseAddress = new Uri(_config["Plaid:BaseURI"]);

            var payload = new
            {
                client_id = _config["Plaid:ClientId"],
                secret = _config["Plaid:Secret"],
                access_token = plaidAccessToken
            };

            var response = await client.PostAsJsonAsync("/item/remove", payload);
            return response.IsSuccessStatusCode;
        }


        // Save or update the "Account Details" + "Permanent Access Token" from Plaid!
        public async Task<IActionResult> SaveAccountDetailsFromPlaid(ulong userId, string plaidPublicTemporaryTokenValue, string dataMode, string? connString = null)
        {

            var modelValidationResults = new List<ValidationResult>();

            try
            {

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(_config["Plaid:BaseURI"]); // Switch to production when live

                // Exchange public_token for access_token and item_id
                var exchangePayload = new
                {
                    client_id = _config["Plaid:ClientId"],
                    secret = _config["Plaid:Secret"],
                    public_token = plaidPublicTemporaryTokenValue
                };

                var exchangeResponse = await client.PostAsJsonAsync("/item/public_token/exchange", exchangePayload);

                if (!exchangeResponse.IsSuccessStatusCode)
                {
                    var errorContent = await exchangeResponse.Content.ReadAsStringAsync();
                    return new BadRequestObjectResult(new { error = errorContent });
                }

                var exchangeJson = await exchangeResponse.Content.ReadFromJsonAsync<JsonElement>();
                string accessToken = exchangeJson.GetProperty("access_token").GetString();  // PERMANENT ACCESS TOKEN
                string itemId = exchangeJson.GetProperty("item_id").GetString();            // SINGLE UNIQUE ITEM ID PER INSTITUTE/BANK

                // Call /item/get to retrieve metadata like Institution ID
                var itemGetPayload = new
                {
                    client_id = _config["Plaid:ClientId"],
                    secret = _config["Plaid:Secret"],
                    access_token = accessToken
                };

                var itemGetResponse = await client.PostAsJsonAsync("/item/get", itemGetPayload);
                string institutionId = null;

                if (itemGetResponse.IsSuccessStatusCode)
                {
                    var itemGetJson = await itemGetResponse.Content.ReadFromJsonAsync<JsonElement>();
                    // Plaid returns an 'item' object which contains 'institution_id'
                    if (itemGetJson.TryGetProperty("item", out var itemObj) &&
                        itemObj.TryGetProperty("institution_id", out var instIdProp))
                    {
                        institutionId = instIdProp.GetString();
                    }
                }

                // (Optional) Call /institutions/get_by_id to get the human-readable Institution Name (e.g., "Chase")
                string institutionName = "Unknown Institution";
                if (!string.IsNullOrEmpty(institutionId))
                {
                    var instPayload = new
                    {
                        client_id = _config["Plaid:ClientId"],
                        secret = _config["Plaid:Secret"],
                        institution_id = institutionId,
                        country_codes = new[] { "US" }
                    };

                    var instResponse = await client.PostAsJsonAsync("/institutions/get_by_id", instPayload);
                    if (instResponse.IsSuccessStatusCode)
                    {
                        var instJson = await instResponse.Content.ReadFromJsonAsync<JsonElement>();
                        if (instJson.TryGetProperty("institution", out var instObj) &&
                            instObj.TryGetProperty("name", out var nameProp))
                        {
                            institutionName = nameProp.GetString().Trim();
                        }
                    }
                }

                // (Optional) Call /accounts/get to see which accounts the user selected (Checking, Savings, etc.)
                var accountsPayload = new
                {
                    client_id = _config["Plaid:ClientId"],
                    secret = _config["Plaid:Secret"],
                    access_token = accessToken
                };

                var accountsResponse = await client.PostAsJsonAsync("/accounts/get", accountsPayload);
                // var selectedAccounts = new List<object>();

                List<Account> accounts = new List<Account>();

                if (accountsResponse.IsSuccessStatusCode)
                {
                    var accountsJson = await accountsResponse.Content.ReadFromJsonAsync<JsonElement>();

                    if (accountsJson.TryGetProperty("accounts", out var accountsArray))
                    {
                        foreach (var acc in accountsArray.EnumerateArray()) // PENDING: CHECK HERE WHAT IS RETURNED FOR "NAME"
                        {
                            string accountName = acc.GetProperty("name").GetString().Trim();

                            if (accountName.Length > 90)
                            {
                                accountName = accountName.Substring(0, 90);
                            }

                            // Get Persistent Account Id
                            string? persistentAccountId = acc.TryGetProperty("persistent_account_id", out var persistentProp) && persistentProp.ValueKind != JsonValueKind.Null
                                                            ? persistentProp.GetString().Trim()
                                                            : null;

                            // Get the account name with masked last four account digits...
                            string? accountMask = acc.TryGetProperty("mask", out var maskProp) && maskProp.ValueKind != JsonValueKind.Null
                                            ? maskProp.GetString().Trim()
                                            : null;

                            string? accountSubType = acc.TryGetProperty("subtype", out var subtypeProp) && subtypeProp.ValueKind != JsonValueKind.Null
                                            ? subtypeProp.GetString().Trim()
                                            : null;

                            string accountTypeText = acc.GetProperty("type").GetString().ToLower();
                            int accountTypeId = GetAccountTypeId(accountTypeText, connString);

                            int maxAccountNameLengthForDescription = institutionName.Length > 92 ? 92 : institutionName.Length;

                            string accountIdForPlaid = acc.GetProperty("account_id").GetString();

                            // Populate Account Model here without validations....
                            Account account = new Account
                            {
                                AccountId = null,
                                UserId = userId,
                                AccountName = accountName,
                                AccountInstitutionName = institutionName,
                                AccountTypeId = accountTypeId,
                                AccountDescription = "Plaid - " + institutionName.Substring(0, maxAccountNameLengthForDescription),
                                IsALinkedAccount = 1,
                                PlaidAccessToken = accessToken,
                                PlaidItemId = itemId,
                                PlaidInstitutionId = institutionId,
                                PlaidAccountId = accountIdForPlaid,
                                PlaidPersistentAccountId = persistentAccountId,
                                PlaidAccountMask = accountMask,
                                PlaidAccountSubtype = accountSubType
                            };

                            // Validate model!
                            var context = new ValidationContext(account, serviceProvider: null, items: null);

                            // validateAllProperties: true ensures every property is checked against its attributes
                            bool isValid = Validator.TryValidateObject(account, context, modelValidationResults, validateAllProperties: true);

                            if (isValid)
                            {
                                accounts.Add(account);
                            }
                            else
                            {
                                // Incase when any of the item fails model validation!
                                return new BadRequestObjectResult(new { success = false, message = modelValidationResults.SelectMany(r => r.MemberNames.Select(member => new { Member = member, r.ErrorMessage })).GroupBy(x => x.Member).ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage!).ToList()) });
                            }

                        }
                    }
                }

                // Securely save accessToken, itemId, institutionName, and accounts to your database here!
                if (accounts.Count > 0)
                {
                    if (SaveLinkedAccountsFromExternalInstitute(userId, accounts, dataMode, connString))
                    {
                        return new OkObjectResult(new { success = true });
                    }
                    else
                    {
                        return new StatusCodeResult(500);
                    }
                }
                else
                {
                    return new OkObjectResult(new { success = false, message = "No Accounts selected!" });
                }

            }
            catch (Exception ex)
            {
                return new StatusCodeResult(500);
            }
        }


        // Gets the account type id
        public int GetAccountTypeId(string accountTypeName, string? connString)
        {
            connString = connString ?? "pft_con_str";
            IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT account_type_id FROM pft_account_types WHERE account_type_name = @AccountTypeName;";

            return _conn.ExecuteScalar<int>(sql, new { AccountTypeName = accountTypeName });

        }


        // Performs the Accounts Reconsilations In Database...Matches with the exact values in the plaid.
        // Useful when Plaid changes the accounts details...like account name!!!
        public async Task PerformPlaidAccountReconsilation(ulong userId, IEnumerable<Account?> linkedAccounts,  string? connString)
        {

            string plaidItemId = "", plaidInstituteId = "";

            if (linkedAccounts.Count() > 0)
            {
                IEnumerable<string?> distinctAccessTokens = linkedAccounts.Select(a => a?.PlaidAccessToken).Distinct();

                foreach (var accessToken in distinctAccessTokens)
                {

                    JsonElement accountsJson = await GetPlaidAccountsDetails(accessToken) ?? new JsonElement();

                    if (accountsJson.TryGetProperty("item", out var plaidItemDetails))
                    {
                        plaidItemId = plaidItemDetails.GetProperty("item_id").GetString();
                        plaidInstituteId = plaidItemDetails.GetProperty("institution_id").GetString();

                        if (accountsJson.TryGetProperty("accounts", out var accountsArray))
                        {
                            foreach (var acc in accountsArray.EnumerateArray())
                            {
                                string plaidAccountId = "", plaidAccountName = "", plaidAccountMask = "", plaidAccountType = "", plaidAccountSubType = "";

                                plaidAccountId = acc.GetProperty("account_id").GetString();
                                plaidAccountName = acc.GetProperty("name").GetString();
                                plaidAccountMask = acc.GetProperty("mask").GetString();
                                plaidAccountType = acc.GetProperty("type").GetString();
                                plaidAccountSubType = acc.GetProperty("subtype").GetString();

                                PlaidAccountReconsilation(userId, plaidItemId, plaidInstituteId, plaidAccountId, plaidAccountName, plaidAccountMask, plaidAccountType, plaidAccountSubType, connString);
                            }
                        }

                    }
                }

            }

        }


        // Gets details from Plaid for the accounts under the same Plaid's PermanentExchangeToken 
        private async Task<JsonElement?> GetPlaidAccountsDetails(string plaidPermanentExchangeToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_config["Plaid:BaseURI"]); // Switch to production when live

            // (Optional) Call /accounts/get to see which accounts the user selected (Checking, Savings, etc.)
            var accountsPayload = new
            {
                client_id = _config["Plaid:ClientId"],
                secret = _config["Plaid:Secret"],
                access_token = plaidPermanentExchangeToken
            };

            var accountsResponse = await client.PostAsJsonAsync("/accounts/get", accountsPayload);

            if (accountsResponse.IsSuccessStatusCode)
            {
                var accountsJson = await accountsResponse.Content.ReadFromJsonAsync<JsonElement>();
                return accountsJson;
            }
            else
            {
                return null;
            }
        }


        // Gets the data from all the external institutes
        public async Task ImportAllLinkedAccountsDataForUser(ulong userId, IEnumerable<Account?> linkedAccounts, string? connString)
        {

            if (linkedAccounts.Count() > 0)
            {
                // Check here if the values are correct here!!!
                var distinctAccounts = linkedAccounts.Select(a => new { PlaidAccessToken = a?.PlaidAccessToken, PlaidItemId = a?.PlaidItemId }).Distinct();

                foreach (var item in distinctAccounts)
                {
                    await SyncTransactionsForItemAsync(userId, item.PlaidAccessToken, item.PlaidItemId, linkedAccounts, connString);
                }
            }

        }


        // All the data are being retrieved from using "Plaid_Item_Id" for all the accounts from a single institute's all the accounts...
        // Plaid Item Id refers to the All the accounts of an institution!
        private async Task SyncTransactionsForItemAsync(ulong userId, string accessToken, string plaidItemId, IEnumerable<Account?> linkedAccounts, string? connString = null)
        {

            HttpClient httpClient = _httpClientFactory.CreateClient();
            string plaidDataFetchUri = string.Join("/", _config["Plaid:BaseURI"], "transactions/sync");

            // Load the last saved cursor for this Item from your MySQL database
            string? currentCursor = GetPlaidNextCursorValue(userId, plaidItemId, connString);

            bool hasMore = true;
            string? latestCursor = currentCursor;

            // Temporary collections to batch changes before saving
            var allAdded = new List<JsonElement>();
            var allModified = new List<JsonElement>();
            var allRemoved = new List<JsonElement>();

            try
            {
                // Pagination Loop
                while (hasMore)
                {
                    var requestBody = new
                    {
                        client_id = _config["Plaid:ClientId"],
                        secret = _config["Plaid:Secret"],
                        access_token = accessToken,
                        cursor = latestCursor // null on first sync, next_cursor on subsequent syncs
                    };

                    HttpResponseMessage response = await httpClient.PostAsJsonAsync(plaidDataFetchUri, requestBody);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Plaid sync failed: {response.StatusCode} - {errorContent}");
                    }

                    using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    JsonElement root = doc.RootElement;

                    // Extract arrays from current page
                    if (root.TryGetProperty("added", out var addedProp) && addedProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in addedProp.EnumerateArray())
                        {
                            allAdded.Add(item.Clone());
                        }
                    }
                    if (root.TryGetProperty("modified", out var modProp) && modProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in modProp.EnumerateArray())
                        {
                            allModified.Add(item.Clone());
                        }
                    }
                    if (root.TryGetProperty("removed", out var remProp) && remProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in remProp.EnumerateArray())
                        {
                            allRemoved.Add(item.Clone());
                        }
                    }

                    // Update pagination flags
                    hasMore = root.GetProperty("has_more").GetBoolean();
                    latestCursor = root.GetProperty("next_cursor").GetString();
                }

                // atomically commit changes and the new cursor to MySQL
                _bulkUploadService.SaveLinkedAccountData(userId, plaidItemId, allAdded, allModified, allRemoved, latestCursor!, linkedAccounts, connString);
            }
            catch (JsonException ex)
            {
                // Handle JSON parsing errors safely
                Console.WriteLine($"JSON parsing error during sync: {ex.Message}");
            }
        }


    }
}
