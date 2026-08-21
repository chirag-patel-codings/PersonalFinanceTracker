using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PersonalFinanceTracker.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ulong _currentUserId;
        private readonly string _userCurrencyCode, _userCurrencySymbol;
        public TransactionController(IHttpContextAccessor httpContextAccessor, ITransactionRepository transactionRepository) 
        { 
            _transactionRepository = transactionRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _userCurrencyCode = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencyCode");
            _userCurrencySymbol = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencySymbol");
        }


        [Authorize]
        [HttpGet("/transactions")]
        // Index page!!!
        public IActionResult Index(string? connString)
        {
            

            return View("Transaction");
        }


        [Authorize]
        [HttpPost("transactions")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {
            Pagination pagination;
            IEnumerable<Transaction> transactions;
            IEnumerable<ListOptionStringId>? categoriesList = null;
            IEnumerable<ListOptionStringId>? accountsList = null;
            IEnumerable<ListOptionStringId>? tagsList = null;
            IEnumerable<ListOptionStringId>? goalsList = null;
            IEnumerable<ListOption>? transactionCategorizationList = null;
            IEnumerable<ListOption>? transactionTypeList = null;
            IEnumerable<ListOption>? transactionRepeatIntervalList = null;

            // Get the inner string from the JSON node, or default to an empty object string
            string transactionFilterString = payload["transactionFilter"]?.ToJsonString() ?? "{}";
            TransactionFilter transactionFilter = JsonSerializer.Deserialize<TransactionFilter>(transactionFilterString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var userCurrencyDetails = new { CurrencyCode = _userCurrencyCode, CurrencySymbol = _userCurrencySymbol };

            bool isFirstRequest = payload["firstRequest"]?.ToString() == "true";

            if (isFirstRequest)
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _transactionRepository.GetTotalNoOfRecords(_currentUserId, transactionFilter, connString);

                categoriesList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "categories", connString);
                accountsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "accounts", connString);
                tagsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "tags", connString);
                goalsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "goals", connString);

                transactionCategorizationList = new List<ListOption>()
                                                {
                                                    new ListOption(0, "All"),
                                                    new ListOption(1, "Manual"),    // 1 = manual, 2 = CSV upload, 3 = auto (Bank)
                                                    new ListOption(3, "Auto"),
                                                };
                transactionTypeList = new List<ListOption>()
                                                {
                                                    new ListOption(0, "All"),
                                                    new ListOption(2, "Recurring")
                                                };
                transactionRepeatIntervalList = new List<ListOption>()
                                                {
                                                    new ListOption(1, "Weekly"),
                                                    new ListOption(2, "BiWeekly"),
                                                    new ListOption(3, "Monthly"),
                                                    new ListOption(4, "BiMonthly"),
                                                    new ListOption(5, "Quarterly"),
                                                    new ListOption(6, "Half-Yearly"),
                                                    new ListOption(7, "Yearly")
                                                };

            }
            else
            {

                // Get the inner string from the JSON node, or default to an empty object string
                string jsonString = payload["pagination"]?.ToJsonString() ?? "{}";

                // Deserialize the string into your strongly-typed Pagination object
                pagination = JsonSerializer.Deserialize<Pagination>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            }

            transactions = _transactionRepository.GetTransactions(_currentUserId, transactionFilter, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);


            if (isFirstRequest)
            {
                return Ok(new { transactions, pagination, categoriesList, accountsList, tagsList, goalsList, transactionCategorizationList, transactionTypeList, transactionRepeatIntervalList, userCurrencyDetails });
            }
            else
            {
                return Ok(new { transactions, pagination });
            }

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified transaction record for display on screen!!!
        public IActionResult GetTransaction([FromBody] dynamic getTransactionDataFor, string? connString = null)
        {

            string transactionId = getTransactionDataFor.GetProperty("transactionId").GetString();
            var transaction = _transactionRepository.GetTransaction(_currentUserId, transactionId, connString);
            
            return Ok(transaction);
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Transaction!!!
        public IActionResult SaveTransaction([FromBody] Transaction transaction, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!
            
            transaction.UserId = _currentUserId;
            
            if (ModelState.IsValid)
            {
                if(_transactionRepository.SaveTransaction(transaction, connString))
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
        public IActionResult DeleteTransaction([FromBody] dynamic deleteTransactionDataFor, string? connString = null)
        {
            string transactionId = deleteTransactionDataFor.GetProperty("transactionId").GetString();

            if (_transactionRepository.DeleteTransaction(_currentUserId, transactionId, connString))
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
