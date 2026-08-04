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
    public class BudgetController : Controller
    {
        private readonly IBudgetDetailsRepository _budgetDetailsRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly ulong _currentUserId;
        private readonly string _userCurrencyCode, _userCurrencySymbol;
        public BudgetController(IHttpContextAccessor httpContextAccessor, IBudgetDetailsRepository budgetDetailsRepository, IBudgetRepository budgetRepository)
        {
            _budgetDetailsRepository = budgetDetailsRepository;
            _budgetRepository = budgetRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _userCurrencyCode = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencyCode");
            _userCurrencySymbol = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencySymbol");
        }


        [Authorize]
        [HttpGet("/budgets")]
        // Index page!!!
        public ActionResult Index(string? connString)
        {
            var budgetDetailsFilter = new BudgetDetailsFilter();
            
            var budgets = _budgetDetailsRepository.GetBudgetDetails(_currentUserId, budgetDetailsFilter.BudgetDetailsStartDate, budgetDetailsFilter.BudgetDetailsEndDate, connString);
            
            return View("Budget", budgets);
        }


        [Authorize]
        [HttpPost("budgets")]
        [ValidateAntiForgeryToken]
        // Returns the records between the BudgetDetailsStartDate and BudgetDetailsEndDate from a 'Post' request...Subsequent after Index page!!
        public IActionResult Index([FromBody] BudgetDetailsFilter budgetDetailsFilter, string? connString)
        {

            var budgetDetails = _budgetDetailsRepository.GetBudgetDetails(_currentUserId, budgetDetailsFilter.BudgetDetailsStartDate, budgetDetailsFilter.BudgetDetailsEndDate, connString);
            var budgetsMonthYear = budgetDetails.Select(b => b.BudgetMonthYear).Distinct();
            var userCurrencyDetails = new { CurrencyCode = _userCurrencyCode, CurrencySymbol = _userCurrencySymbol };

            return Ok(new { budgetDetails, budgetsMonthYear, userCurrencyDetails });
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Budget!!!
        public IActionResult SaveBudget([FromBody] BudgetDetails budgetDetails, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!

            budgetDetails.userId = _currentUserId;

            if (ModelState.IsValid)
            {
                if (_budgetDetailsRepository.SaveBudgetDetails(budgetDetails, connString))
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
        public IActionResult DeleteBudget([FromBody] BudgetDetails budgetDetails, string? connString = null)
        {
            
            if (_budgetDetailsRepository.DeleteBudgetDetails(_currentUserId, budgetDetails.BudgetId, budgetDetails.BudgetDateDigits, connString))
            {
                return Ok(new { success = true });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Database error occurred." });  // record may not be found!!!
            }

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Returns all the budget settings for the current user
        public IActionResult GetBudgetSettings(string? connString)
        {

            var budgetSettings = _budgetRepository.GetBudgetSettings(_currentUserId, connString);
            return Ok(budgetSettings);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Save Budget Settings!!!
        public IActionResult SaveBudgetSettings([FromBody] Budget budget, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!

            budget.UserId = _currentUserId;

            if (ModelState.IsValid)
            {
                if (_budgetRepository.SaveBudgetSettings(budget, connString))
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

    }
}
