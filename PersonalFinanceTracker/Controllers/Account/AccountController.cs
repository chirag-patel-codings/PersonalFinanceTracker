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
        private readonly IPlaidService _plaidService;
        private readonly ulong _currentUserId;

        public AccountController(IHttpContextAccessor httpContextAccessor, IAccountRepository accountRepository, IPlaidService plaidService)
        {

            _accountRepository = accountRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _plaidService = plaidService;

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
        [Authorize]
        [HttpPost("/api/plaid/create-link-token")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLinkToken()
        {
            return await  _plaidService.CreatePlaidLinkTokenAsync(_currentUserId); // existingAccessToken will be null
        }


        // Call this when use clicks "Edit" or "Delete" icon...
        // plaidAccessToken can be permanent Token
        [Authorize]
        [HttpPost("/api/plaid/create-update-link-token")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUpdateLinkToken([FromBody] dynamic createUpdateTokenForAccount, string? connString = null)
        {

            string accountId = createUpdateTokenForAccount.GetProperty("accountId").GetString();

            // PLAID PERMENANT ACCESS TOKEN SHOULD NEVER BE SENT TO THE FRONT-END.
            // Get the existing permanent access token for the account from database.
            string? plaidAccessToken = _plaidService.GetPlaidPermanentAccessToken(_currentUserId, accountId, connString);

            if (string.IsNullOrEmpty(plaidAccessToken))
            {
                return NotFound(new { success = false, message = "Linked account not found." });
            }

            return await _plaidService.CreatePlaidLinkTokenAsync(_currentUserId, plaidAccessToken);

        }


        // Gets the "account details" + "permenant Plaid token" from the Plaid and saves them to database.
        [Authorize]
        [HttpPost("/api/plaid/get-and-save-exchange-tokens")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetAndSaveExchangeTokens([FromBody] JsonObject payload, string? connString = null)
        {
            
            string plaidPublicTemporaryTokenValue = payload["plaidPublicTemporaryToken"]?.ToString()!;
            string dataMode = payload["dataMode"]?.ToString().ToLower()!;

            var result = await _plaidService.SaveAccountDetailsFromPlaid(_currentUserId, plaidPublicTemporaryTokenValue!, dataMode!, connString);

            if (result is StatusCodeResult statusCodeResult)    // to check statuscode '500'
            {
                if(statusCodeResult.StatusCode == 500)
                {
                    result = StatusCode(500, new { success = false, message = "Database or other error occurred." });
                }
            }

            return result;

        }

        // Plaid - End


    }
}
