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
    
    public class ReportController : Controller
    {
        private readonly IReportRepository _reportRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ulong _currentUserId;
        private readonly string _userCurrencyCode, _userCurrencySymbol;
        public ReportController(IHttpContextAccessor httpContextAccessor, IReportRepository reportRepository, ITransactionRepository transactionRepository)
        {

            _reportRepository = reportRepository;
            _transactionRepository = transactionRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _userCurrencyCode = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencyCode");
            _userCurrencySymbol = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencySymbol");

        }


        // Index page!!!
        [Authorize]
        [HttpGet("reports")]
        public ActionResult Index(string? connString)
        {
            
            return View("Report");
        }

        // Returns the User's Currency Details and Tags lists!!!
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetTagsAndCurrencyDetails([FromBody] string? connString = null)
        {

            var userCurrencyDetails = new { CurrencyCode = _userCurrencyCode, CurrencySymbol = _userCurrencySymbol };

            IEnumerable<ListOptionStringId>? tagsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "tags", connString);

            return Ok(new { tagsList, userCurrencyDetails });

        }

        // Gets the specified report record for display on screen!!!
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetReport([FromBody] ReportFilter reportFilter, string? connString = null)
        {

            IEnumerable<string> reportMonthYear = null;

            var report = _reportRepository.GetReport(_currentUserId, reportFilter.ReportName, reportFilter.ReportStartDate, reportFilter.ReportEndDate, reportFilter.ReportInterval, reportFilter.TagId, connString);

            /*
            if (report.Any(r => !string.IsNullOrEmpty(r.ReportMonthYear)))
            {
                reportMonthYear = report.Select(r => r.ReportMonthYear).Distinct();
            }
            */

            return Ok(new { report });

        }

        
    }
}
