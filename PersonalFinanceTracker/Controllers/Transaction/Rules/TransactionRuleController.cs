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
    public class TransactionRuleController : Controller
    {
        private readonly ITransactionRuleRepository _transactionRuleRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionRuleService _transactionRuleService;
        private readonly ulong _currentUserId;
        private readonly string _userCurrencyCode, _userCurrencySymbol;
        public TransactionRuleController(IHttpContextAccessor httpContextAccessor, 
                                            ITransactionRuleRepository transactionRuleRepository, 
                                            ITransactionRepository transactionRepository,
                                            ITransactionRuleService transactionRuleService) 
        { 

            _transactionRuleRepository = transactionRuleRepository;
            _transactionRepository = transactionRepository;
            _transactionRuleService = transactionRuleService;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _userCurrencyCode = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencyCode");
            _userCurrencySymbol = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencySymbol");

        }


        [Authorize]
        [HttpGet("/transactions/rules")]
        // Index page!!!
        public IActionResult Index(string? connString)
        {

            return View("~/Views/Transaction/Rules/TransactionRule.cshtml");

        }


        [Authorize]
        [HttpPost("/transactions/rules")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {

            Pagination pagination;
            IEnumerable<TransactionRule> transactionRules;
            IEnumerable<ListOptionStringId>? accountsList = null;
            IEnumerable<ListOptionStringId>? categoriesList = null;
            IEnumerable<ListOptionStringId>? goalsList = null;
            IEnumerable<ListOption>? transactionTextMatchComparisionList = null;
            IEnumerable<ListOption>? transactionAmountMatchComparisionList = null;

            // Get the inner string from the JSON node, or default to an empty object string
            var userCurrencyDetails = new { CurrencyCode = _userCurrencyCode, CurrencySymbol = _userCurrencySymbol };

            bool isFirstRequest = payload["firstRequest"]?.ToString() == "true";

            if (isFirstRequest)
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _transactionRuleRepository.GetTotalNoOfRecords(_currentUserId, connString);

                accountsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "accounts", connString);
                categoriesList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "categories", connString);
                goalsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "goals", connString);

                transactionTextMatchComparisionList = new List<ListOption>()
                                                {
                                                    new ListOption(0, "Contains"),
                                                    new ListOption(1, "Starts With"),
                                                    new ListOption(2, "Exact Match"),
                                                };

                transactionAmountMatchComparisionList = new List<ListOption>()
                                                {
                                                    new ListOption(0, "Any Amount"),
                                                    new ListOption(1, "Money In"),
                                                    new ListOption(2, "Money Out"),
                                                    new ListOption(3, "Exact Amount")
                                                };

            }
            else
            {

                // Get the inner string from the JSON node, or default to an empty object string
                string jsonString = payload["pagination"]?.ToJsonString() ?? "{}";

                // Deserialize the string into your strongly-typed Pagination object
                pagination = JsonSerializer.Deserialize<Pagination>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            }

            transactionRules = _transactionRuleRepository.GetTransactionRules(_currentUserId, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);


            if (isFirstRequest)
            {
                return Ok(new { transactionRules, pagination, categoriesList, accountsList, goalsList, transactionTextMatchComparisionList, transactionAmountMatchComparisionList, userCurrencyDetails });
            }
            else
            {
                return Ok(new { transactionRules, pagination });
            }

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified transactionRule record for display on screen!!!
        public IActionResult GetTransactionRule([FromBody] dynamic getTransactionRuleDataFor, string? connString = null)
        {

            string transactionRuleId = getTransactionRuleDataFor.GetProperty("transactionRuleId").GetString();
            var transactionRule = _transactionRuleRepository.GetTransactionRule(_currentUserId, transactionRuleId, connString);
            
            return Ok(transactionRule);

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New TransactionRule!!!
        public IActionResult SaveTransactionRule([FromBody] JsonObject payload, string? connString = null)
        {

            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!
            
            TransactionRule transactionRule = JsonSerializer.Deserialize<TransactionRule>(payload["data"]?.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            transactionRule.UserId = _currentUserId;

            bool applyRule = payload["applyRule"]?.ToString() == "1";

            // Perform model validations (as cannot be validated by framework because it's supplied as JsonObject)!!!
            TryValidateModel(transactionRule);

            if (ModelState.IsValid)
            {
                if(_transactionRuleRepository.SaveTransactionRule(transactionRule, connString))
                {
                    if (applyRule)
                    {

                        var allTransactions = _transactionRepository.GetCSVAndBankLinkTransactionsForUser(_currentUserId, connString);

                        foreach(var transaction in allTransactions)
                        {

                            var transactionWithRuleApplied = _transactionRuleService.ApplyRuleToTransaction(_currentUserId, transaction, transactionRule, connString);

                            // Update only if the transaction record has been changed with the rule values applied!
                            if (transactionWithRuleApplied.isRuleApplied)
                            {
                                _transactionRepository.UpdateTransactionRecordFromRule(_currentUserId, transactionWithRuleApplied.transaction, connString);
                            }

                        }

                    }
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
        public IActionResult DeleteTransactionRule([FromBody] dynamic getTransactionRuleDataFor, string? connString = null)
        {
            
            string transactionRuleId = getTransactionRuleDataFor.GetProperty("transactionRuleId").GetString();

            if (_transactionRuleRepository.DeleteTransactionRule(_currentUserId, transactionRuleId, connString))
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
