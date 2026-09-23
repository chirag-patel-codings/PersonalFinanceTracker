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
    public class RecurringController : Controller
    {
        private readonly IRecurringRepository _recurringRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ulong _currentUserId;
        private readonly string _userCurrencyCode, _userCurrencySymbol;
        public RecurringController(IHttpContextAccessor httpContextAccessor, IRecurringRepository recurringRepository, ITransactionRepository transactionRepository) 
        { 

            _recurringRepository = recurringRepository;
            _transactionRepository = transactionRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _userCurrencyCode = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencyCode");
            _userCurrencySymbol = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencySymbol");
        
        }


        [Authorize]
        [HttpGet("/transactions/recurring")]
        // Index page!!!
        public IActionResult Index(string? connString)
        {

            return View("~/Views/Transaction/RecurringSchedules/Recurring.cshtml");

        }


        [Authorize]
        [HttpPost("/transactions/recurring")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {

            Pagination pagination;
            IEnumerable<Recurring> recurrings;
            IEnumerable<ListOptionStringId>? categoriesList = null;
            IEnumerable<ListOptionStringId>? accountsList = null;
            IEnumerable<ListOptionStringId>? tagsList = null;
            IEnumerable<ListOptionStringId>? goalsList = null;
            IEnumerable<ListOption>? transactionRepeatIntervalList = null;

            // Get the inner string from the JSON node, or default to an empty object string
            var userCurrencyDetails = new { CurrencyCode = _userCurrencyCode, CurrencySymbol = _userCurrencySymbol };

            bool isFirstRequest = payload["firstRequest"]?.ToString() == "true";

            if (isFirstRequest)
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _recurringRepository.GetTotalNoOfRecords(_currentUserId, connString);

                categoriesList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "categories", connString);
                accountsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "accounts", connString);
                tagsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "tags", connString);
                goalsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "goals", connString);

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

            recurrings = _recurringRepository.GetRecurrings(_currentUserId, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);


            if (isFirstRequest)
            {
                return Ok(new { recurrings, pagination, categoriesList, accountsList, tagsList, goalsList, transactionRepeatIntervalList, userCurrencyDetails });
            }
            else
            {
                return Ok(new { recurrings, pagination });
            }

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified recurring record for display on screen!!!
        public IActionResult GetRecurring([FromBody] dynamic getRecurringDataFor, string? connString = null)
        {

            string recurringId = getRecurringDataFor.GetProperty("recurringId").GetString();
            var recurring = _recurringRepository.GetRecurring(_currentUserId, recurringId, connString);
            
            return Ok(recurring);

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Recurring!!!
        public IActionResult SaveRecurring([FromBody] Recurring recurring, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!
            
            recurring.UserId = _currentUserId;
            
            if (ModelState.IsValid)
            {
                if(_recurringRepository.SaveRecurring(recurring, connString))
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
        public IActionResult DeleteRecurring([FromBody] JsonObject payload, string? connString = null)
        {
            // dynamic deleteRecurringDataFor

            bool deleteSeries = payload["deleteSeries"]?.ToString() == "true";
            string recurringId = payload["recurringId"]?.ToString();

            if (_recurringRepository.DeleteRecurring(_currentUserId, recurringId, deleteSeries, connString))
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
