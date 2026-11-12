using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http.Json;
using System.ComponentModel;


namespace PersonalFinanceTracker.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ulong _currentUserId;

        // For Plaid - Start
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        // For Plaid - End

        public AccountController(IHttpContextAccessor httpContextAccessor, IAccountRepository accountRepository, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {

            _accountRepository = accountRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));

            _configuration = configuration;
            _httpClientFactory = httpClientFactory;

        }


        [Authorize]
        [HttpGet("/accounts")]
        // Index page!!!
        public ActionResult Index(string? connString)
        {

            return View("Account");

        }


        [Authorize]
        [HttpPost("/accounts")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...Subsequent after Index page!!1
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {

            Pagination pagination;
            IEnumerable<Account> accounts;
            IEnumerable<AccountType>? accountTypes = null;

            if (payload["firstRequest"]?.ToString().ToLower() == "true")
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _accountRepository.GetTotalNoOfRecords(_currentUserId, connString);

                accountTypes = _accountRepository.GetAccountTypes(connString);
            }
            else
            {
                // Get the inner string from the JSON node, or default to an empty object string
                string jsonString = payload["pagination"]?.ToJsonString() ?? "{}";

                // Deserialize the string into your strongly-typed Pagination object
                pagination = JsonSerializer.Deserialize<Pagination>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            }

            accounts = _accountRepository.GetAccounts(_currentUserId, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);

            return Ok(new { accounts, pagination, accountTypes });

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified account record for display on screen!!!
        public IActionResult GetAccount([FromBody] dynamic getAccountDataFor, string? connString = null)
        {

            string accountId = getAccountDataFor.GetProperty("accountId").GetString();
            var account = _accountRepository.GetAccount(_currentUserId, accountId, connString);

            return Ok(account);

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Account!!!
        public IActionResult SaveAccount([FromBody] Account account, string? connString = null)
        {
            
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!
            if (ModelState.IsValid)
            {
                account.UserId = _currentUserId;
                if (_accountRepository.SaveAccount(account, connString))
                {
                    return Ok(new { success = true });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Database error occurred." });
                }
            }
            else
            {
                return BadRequest(new { success = false, message = ModelState.Where(ms => ms.Value.Errors.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()) });
            }

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAccount([FromBody] dynamic deleteAccountDataFor, string? connString = null)
        {
            
            string accountId = deleteAccountDataFor.GetProperty("accountId").GetString();

            if (_accountRepository.DeleteAccount(_currentUserId, accountId, connString))
            {
                return Ok(new { success = true });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Database error occurred." });  // record may not be found!!!
            }

        }

        // Plaid - Start

        // Creates Temporary Token that remains valid for 30 minutes.
        // PENDING: EVEN IF IT'S ADD MODE, IF THE USER HAS SELECTED OTHER ACCOUNTS FROM THE INSTITUTE, IT'S ACCESS TOKEN TO BE USED...
        // IT SHOULD NOT GENERATE THE NEW ACCESS TOKEN OR ITEM_ID
        [Authorize]
        [HttpPost("/api/plaid/create-link-token")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLinkToken()
        {
            return await CreatePlaidLinkTokenAsync(); // existingAccessToken will be null
        }


        // Call this when use clicks "Edit" or "Delete" icon...
        // plaidAccessToken can be permanent Token
        [HttpPost("/api/plaid/create-update-link-token")]
        public async Task<IActionResult> CreateUpdateLinkToken([FromBody] dynamic createUpdateTokenForAccount, string? connString = null)
        {

            string accountId = createUpdateTokenForAccount.GetProperty("accountId").GetString();

            // PLAID PERMENANT ACCESS TOKEN SHOULD NEVER BE SENT TO THE FRONT-END.
            // Get the existing permanent access token for the account from your database.
            string? plaidAccessToken = _accountRepository.GetPlaidPermanentAccessToken(_currentUserId, accountId, connString);


            // TEMP - MUST BE REMOVED...Start
            // bool accountDeleted = DeletePlaidItemAsync(plaidAccessToken).Result;
            // TEMP - MUST BE REMOVED...End

            if (string.IsNullOrEmpty(plaidAccessToken))
            {
                return NotFound(new { success = false, message = "Linked account not found." });
            }

            return await CreatePlaidLinkTokenAsync(plaidAccessToken);

        }

        // DELETE THE EXISTING BANK (ALL THE ACCOUNTS) FROM PLAID
        // [HttpPost("/api/plaid/delete-linked-institute")]
        public async Task<bool> DeletePlaidItemAsync(string plaidAccessToken)
        {
            
            using var client = new HttpClient();
            client.BaseAddress = new Uri(_configuration["Plaid:BaseURI"]);

            var payload = new
            {
                client_id = _configuration["Plaid:ClientId"],
                secret = _configuration["Plaid:Secret"],
                access_token = plaidAccessToken
            };

            var response = await client.PostAsJsonAsync("/item/remove", payload);
            return response.IsSuccessStatusCode;
        }

        // Creates a new "Temporary Token" in Plaid or if the existing Permanent Access Token is supplied then makes the accounts selectable to user can 
        // check or un-check the accounts in selection to sync / stop syncing data from that account!
        public async Task<IActionResult> CreatePlaidLinkTokenAsync(string? existingAccessToken = null)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(_configuration["Plaid:BaseURI"]);

            // Build the base payload required for ALL link tokens
            var payload = new Dictionary<string, object>
            {
                { "client_id", _configuration["Plaid:ClientId"] },
                { "secret", _configuration["Plaid:Secret"] },
                { "client_name", _configuration["Plaid:ClientName"] },
                { "country_codes", new[] { "US" } },
                { "language", "en" },
                { "user", new { client_user_id = _currentUserId.ToString() } },
                { "products", new[] { "transactions" } }
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
                return Ok(new { link_token = linkToken });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Plaid API Error: {errorContent}");

        }


        // Gets the "account details" + "permenant Plaid token" from the Plaid and saves them to database.
        [Authorize]
        [HttpPost("/api/plaid/get-and-save-exchange-tokens")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetAndSaveExchangeTokens([FromBody] JsonObject payload, string? connString = null)
        {
            // string plaidPublicTokenValue = plaidPublicToken.GetProperty("plaidPublicToken").GetString();

            string plaidPublicTemporaryTokenValue = payload["plaidPublicTemporaryToken"]?.ToString();
            string dataMode = payload["dataMode"]?.ToString().ToLower();

            var result = await SaveAccountDetailsFromPlaid(plaidPublicTemporaryTokenValue, dataMode, connString);

            return result;

        }


        // Save or update the "Account Details" + "Permanent Access Token" from Plaid!
        private async Task<IActionResult> SaveAccountDetailsFromPlaid(string plaidPublicTemporaryTokenValue, string dataMode, string? connString = null)
        {

            try
            {

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(_configuration["Plaid:BaseURI"]); // Switch to production when live

                // Exchange public_token for access_token and item_id
                var exchangePayload = new
                {
                    client_id = _configuration["Plaid:ClientId"],
                    secret = _configuration["Plaid:Secret"],
                    public_token = plaidPublicTemporaryTokenValue
                };

                var exchangeResponse = await client.PostAsJsonAsync("/item/public_token/exchange", exchangePayload);

                if (!exchangeResponse.IsSuccessStatusCode)
                {
                    var errorContent = await exchangeResponse.Content.ReadAsStringAsync();
                    return BadRequest(new { error = errorContent });
                }

                var exchangeJson = await exchangeResponse.Content.ReadFromJsonAsync<JsonElement>();
                string accessToken = exchangeJson.GetProperty("access_token").GetString();  // PERMANENT ACCESS TOKEN
                string itemId = exchangeJson.GetProperty("item_id").GetString();            // SINGLE UNIQUE ITEM ID PER INSTITUTE/BANK

                // Call /item/get to retrieve metadata like Institution ID
                var itemGetPayload = new
                {
                    client_id = _configuration["Plaid:ClientId"],
                    secret = _configuration["Plaid:Secret"],
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
                        client_id = _configuration["Plaid:ClientId"],
                        secret = _configuration["Plaid:Secret"],
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
                    client_id = _configuration["Plaid:ClientId"],
                    secret = _configuration["Plaid:Secret"],
                    access_token = accessToken
                };

                var accountsResponse = await client.PostAsJsonAsync("/accounts/get", accountsPayload);
                var selectedAccounts = new List<object>();

                List<Account> accounts = new List<Account>();

                if (accountsResponse.IsSuccessStatusCode)
                {
                    var accountsJson = await accountsResponse.Content.ReadFromJsonAsync<JsonElement>();

                    if (accountsJson.TryGetProperty("accounts", out var accountsArray))
                    {
                        foreach (var acc in accountsArray.EnumerateArray()) // PENDING: CHECK HERE WHAT IS RETURNED FOR "NAME"
                        {
                            string accountName = acc.GetProperty("name").GetString().Trim();

                            if(accountName.Length > 90)
                            {
                                accountName = accountName.Substring(0, 90);
                            }
                            // Get the account name with masked last four account digits...
                            accountName = accountName + "*" + acc.GetProperty("mask").GetString().Trim();

                            int accountTypeId = 101;
                            string accountTypeText = acc.GetProperty("type").GetString().ToLower();

                            if (accountTypeText.Contains("depository"))
                            {
                                accountTypeId = 101;
                            }
                            else if (accountTypeText.Contains("credit"))
                            {
                                accountTypeId = 501;
                            }
                            else if (accountTypeText.Contains("investment"))
                            {
                                accountTypeId = 102;
                            }
                            else if (accountTypeText.Contains("loan"))
                            {
                                accountTypeId = 502;
                            }

                            int maxAccountNameLengthForDescription = institutionName.Length > 92 ? 92 : institutionName.Length;

                            string accountIdForPlaid = null;
                            if (acc.TryGetProperty("persistent_account_id", out var persistentProp) && persistentProp.ValueKind != JsonValueKind.Null)  // Always unique and not frequently changing
                            {
                                accountIdForPlaid = persistentProp.GetString();
                            }
                            else
                            {
                                // Fallback to standard account_id if persistent_account_id is missing
                                accountIdForPlaid = acc.GetProperty("account_id").GetString();
                            }

                            // Populate Account Model here without validations....
                            Account account = new Account
                            {
                                AccountId = null,
                                UserId = _currentUserId,
                                AccountName = accountName,
                                AccountInstitutionName = institutionName,
                                AccountTypeId = accountTypeId,
                                AccountDescription = "Plaid - " + institutionName.Substring(0, maxAccountNameLengthForDescription),
                                IsALinkedAccount = 1,
                                PlaidAccessToken = accessToken,
                                PlaidItemId = itemId,
                                PlaidInstitutionId = institutionId,
                                PlaidAccountId = accountIdForPlaid
                            };

                            TryValidateModel(account);
                            if (ModelState.IsValid)
                            {
                                accounts.Add(account);
                            }
                            else
                            {
                                // Incase when any of the item fails model validation!
                                return BadRequest(new { success = false, message = ModelState.Where(ms => ms.Value.Errors.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()) });
                            }

                        }
                    }
                }

                // PENDING: Securely save accessToken, itemId, institutionName, and accounts to your database here!
                if (accounts.Count > 0)
                {
                    if (_accountRepository.SaveLinkedAccountsFromExternalInstitute(accounts, dataMode, connString))
                    {
                        return Ok(new { success = true });
                    }
                    else
                    {
                        return StatusCode(500, new { success = false, message = "Database error occurred." });
                    }
                }
                else
                {
                    return Ok(new { success = false, message = "No Accounts selected!   " });
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }


        /*
        
        public async Task<IActionResult> ExchangeToken([FromBody] dynamic plaidPublicToken)
        {

            string plaidPublicTokenValue = plaidPublicToken.GetProperty("plaidPublicToken").GetString();

            // Create HttpClient pointing to Sandbox (or Production URL)
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://sandbox.plaid.com"); // Switch to https://production.plaid.com when live

            // Build the payload matching Plaid's raw JSON schema for public token exchange
            var payload = new
            {
                client_id = _configuration["Plaid:ClientId"],
                secret = _configuration["Plaid:Secret"],
                public_token = plaidPublicTokenValue
            };

            // Send POST request to Plaid's direct endpoint
            var response = await client.PostAsJsonAsync("/item/public_token/exchange", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return BadRequest(new { error = errorContent });
            }

            // Read the JSON response from Plaid
            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();

            // CRITICAL: Save accessToken and itemId to your database securely tied to the user!
            string accessToken = jsonResponse.GetProperty("access_token").GetString();
            string itemId = jsonResponse.GetProperty("item_id").GetString();

            return Ok(new { success = true, accessToken = accessToken, itemId = itemId, message = "Successfully exchanged token via direct Plaid API!" });


        }
        
        */

        // Plaid - End


    }
}
