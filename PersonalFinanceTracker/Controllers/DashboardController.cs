using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IConfiguration _config;
        private readonly IReportRepository _reportRepository;
        IUserRegistrationRepository _userRegistrationRepository;
        private readonly ulong _currentUserId;
        private readonly string _userCurrencyCode, _userCurrencySymbol;

        public DashboardController(IHttpContextAccessor httpContextAccessor, IConfiguration config, IUserRegistrationRepository userRegistrationRepository, IReportRepository reportRepository)
        {

            _userRegistrationRepository = userRegistrationRepository;
            _reportRepository = reportRepository;
            
            _config = config;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _userCurrencyCode = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencyCode");
            _userCurrencySymbol = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencySymbol");

        }
        public IActionResult Index()
        {

            return View();

        }

        // Returns the SyncFusion License Key & User's Currency Details
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetUserCurrencyDetailsAndLicenseKey()
        {

            string licenseKey = _config["LicenseKeys:SyncFusion"];

            var userCurrencyDetails = new { CurrencyCode = _userCurrencyCode, CurrencySymbol = _userCurrencySymbol };

            return Ok(new { userCurrencyDetails, licenseKey });

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetReportsData([FromBody] DashboardFilter dashboardFilter, string? connString)
        {

            var categoryWiseSummaryChartData = GetCategoryWiseSummaryData(dashboardFilter, connString);
            var monthlyIncomeExpenseChartData = GetMonthlyIncomeExpenseData(dashboardFilter, connString);
            var monthlyNetIncomeChartData = GetNetIncomeData(dashboardFilter, connString);
            var monthlyNetWorthChartData = GetNetWorthData(dashboardFilter, connString);
            

            return Ok(new { categoryWiseSummaryChartData, monthlyIncomeExpenseChartData, monthlyNetIncomeChartData, monthlyNetWorthChartData });

        }

        // Gets the Monthly Income-Expense data
        private List<Report> GetCategoryWiseSummaryData(DashboardFilter dashboardFilter, string? connString)
        {

            var categoryWiseSummary = _reportRepository.GetReport(_currentUserId, "CategoryWiseSummary", dashboardFilter.DashboardParametersStartDate, dashboardFilter.DashboardParametersEndDate, 1, null, connString).ToList(); // Monthly Reports

            return categoryWiseSummary;

        }


        // Gets the Monthly Income-Expense data
        private List<DashboardBarAndLineCharts> GetMonthlyIncomeExpenseData(DashboardFilter dashboardFilter, string? connString) 
        {
            List<DashboardBarAndLineCharts> monthlyIncomeExpenses = new List<DashboardBarAndLineCharts>();
            var monthlyIncomeExpensesData = _reportRepository.GetReport(_currentUserId, "IncomeExpense", dashboardFilter.DashboardParametersStartDate, dashboardFilter.DashboardParametersEndDate, 1, null, connString); // Monthly Reports

            var distinctMonthYear = monthlyIncomeExpensesData.Select(m => m.ReportMonthYear).Distinct();
            
            // Check if the data retains the sort order
            foreach (var item in distinctMonthYear)
            {
                var dashboardBarAndLineCharts =  monthlyIncomeExpensesData
                                                        .Where(m => m.ReportMonthYear == item)
                                                        .GroupBy(m => m.ReportClassification )
                                                        .Select( s => new {
                                                            MonthYear = item,
                                                            Category = s.Key == 1 ? "Income" : "Expense",
                                                            Amount = s.Sum(x => x.ReportAmount2)
                                                        });
                foreach(var listItem in dashboardBarAndLineCharts)
                {
                    DashboardBarAndLineCharts dashboardBarAndLineChart = new DashboardBarAndLineCharts(listItem.MonthYear, listItem.Category, Math.Abs(listItem.Amount ?? 0));
                    monthlyIncomeExpenses.Add(dashboardBarAndLineChart);
                }
            }

            monthlyIncomeExpenses.Reverse();

            return monthlyIncomeExpenses;

        }

        // Gets the Monthly Net Income-Expense data
        private List<DashboardBarAndLineCharts> GetNetIncomeData(DashboardFilter dashboardFilter, string? connString)
        {
            List<DashboardBarAndLineCharts> monthlyNetIncome = new List<DashboardBarAndLineCharts>();
            var monthlyNetIncomeData = _reportRepository.GetReport(_currentUserId, "IncomeExpense", dashboardFilter.DashboardParametersStartDate, dashboardFilter.DashboardParametersEndDate, 1, null, connString); // Monthly Reports

            var distinctMonthYear = monthlyNetIncomeData.Select(m => m.ReportMonthYear).Distinct();

            // Check if the data retains the sort order
            foreach (var item in distinctMonthYear)
            {
                var dashboardBarAndLineCharts = monthlyNetIncomeData
                                                        .Where(m => m.ReportMonthYear == item)
                                                        .GroupBy(m => m.ReportMonthYear)
                                                        .Select(s => new {
                                                            MonthYear = s.Key,
                                                            Amount = s.Sum(x => x.ReportAmount2)
                                                        });
                foreach (var listItem in dashboardBarAndLineCharts)
                {
                    DashboardBarAndLineCharts dashboardBarAndLineChart = new DashboardBarAndLineCharts(listItem.MonthYear, null, Math.Round(listItem.Amount ?? 0, 2));
                    monthlyNetIncome.Add(dashboardBarAndLineChart);
                }
            }

            monthlyNetIncome.Reverse();

            return monthlyNetIncome;

        }

        // Gets the Monthly Net Income-Expense data
        private List<DashboardBarAndLineCharts> GetNetWorthData(DashboardFilter dashboardFilter, string? connString)
        {
            List<DashboardBarAndLineCharts> monthlyNetWorth = new List<DashboardBarAndLineCharts>();
            var monthlyNetWorthData = _reportRepository.GetReport(_currentUserId, "NetWorth", dashboardFilter.DashboardParametersStartDate, dashboardFilter.DashboardParametersEndDate, 1, null, connString); // Monthly Reports

            var distinctMonthYear = monthlyNetWorthData.Select(m => m.ReportMonthYear).Distinct();

            // Check if the data retains the sort order
            foreach (var item in distinctMonthYear)
            {
                var dashboardBarAndLineCharts = monthlyNetWorthData
                                                        .Where(m => m.ReportMonthYear == item)
                                                        .GroupBy(m => m.ReportMonthYear)
                                                        .Select(s => new {
                                                            MonthYear = s.Key,
                                                            Amount = s.Sum(x => x.ReportAmount2)    // All accounts combined!!!
                                                        });
                foreach (var listItem in dashboardBarAndLineCharts)
                {
                    DashboardBarAndLineCharts dashboardBarAndLineChart = new DashboardBarAndLineCharts(listItem.MonthYear, null, Math.Round(listItem.Amount ?? 0, 2));
                    monthlyNetWorth.Add(dashboardBarAndLineChart);
                }
            }
            monthlyNetWorth.Reverse();

            return monthlyNetWorth;

        }

        [AllowAnonymous]
        [Route("VerifyEmail")]
        public IActionResult VerifyEmail([FromQuery] string? token)
        {

            if(token != null)
            {
                int totalRecordsUpdated = _userRegistrationRepository.UpdateUserEmailVarification(token, null);
                if(totalRecordsUpdated > 0)
                {
                    return RedirectToAction("Index");
                }
            }

            return RedirectToAction("Error/" + HttpStatusCode.InternalServerError, "Error");

        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {

            return View();

        }


        
    }
}
