using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using System.Diagnostics;
using System.Net;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        IUserRegistrationRepository _userRegistrationRepository;
        public DashboardController(IUserRegistrationRepository userRegistrationRepository)
        {
            _userRegistrationRepository = userRegistrationRepository;
        }
        public IActionResult Index()
        {
            return View();
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
