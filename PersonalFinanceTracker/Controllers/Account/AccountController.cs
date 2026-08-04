using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PersonalFinanceTracker.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ulong _currentUserId;
        public AccountController(IHttpContextAccessor httpContextAccessor, IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
        }


        [Authorize]
        [HttpGet("/accounts")]
        // Index page!!!
        public ActionResult Index(string? connString)
        {
            return View("Account");
        }


        [Authorize]
        [HttpPost("accounts")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...Subsequent after Index page!!1
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {
            Pagination pagination;
            IEnumerable<Account> accounts;
            IEnumerable<AccountType>? accountTypes = null;

            if (payload["firstRequest"]?.ToString() == "true")
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

            account.UserId = _currentUserId;

            if (ModelState.IsValid)
            {
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
                return BadRequest(new { success = false, message = "Data Validation Error." });
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
    }
}
