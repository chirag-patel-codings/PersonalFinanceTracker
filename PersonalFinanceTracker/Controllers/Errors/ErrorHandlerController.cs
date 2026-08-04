using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Utilities.Zlib;
using PersonalFinanceTracker.Controllers.Authentication;
using PersonalFinanceTracker.Models;
using System.Diagnostics;

namespace PersonalFinanceTracker.Controllers.Errors
{
    public class ErrorHandlerController : Controller
    {
        // 1. Declare the private field for logging.
        private readonly ILogger<ErrorHandlerController> _logger;

        // 2. Inject it through the constructor
        public ErrorHandlerController(ILogger<ErrorHandlerController> logger)
        {
            // 3. Assign it to the field
            _logger = logger;
        }
        

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [Route("/Error/{statusCode?}")]
        public IActionResult Error(int? statusCode = null)
        {
            // 1. Initialize the view model with the request tracking ID
            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode ?? 500 // Default to 500 if no code is passed
            };

            // 2. Customize the message depending on the HTTP status code
            if (model.StatusCode == 404)
            {
                model.ErrorMessage = "The page or resource you requested could not be found.";

                var missingPath = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath;
                _logger.LogWarning("404 Not Found - Invalid URI: {Path}", missingPath);

                // Return the standard error view passing the compiled model
                return View("Error", model);
            }

            if (model.StatusCode == 403)
            {
                model.ErrorMessage = "You do not have permission to access this resource.";
                return View("AccessDenied", model);
            }

            // 3. Handle general 500 system/database crashes
            var exceptionDetails = HttpContext.Features.Get<IExceptionHandlerFeature>();

            if (exceptionDetails != null)
            {
                _logger.LogError(exceptionDetails.Error, "Global Crash Caught: {Message}", exceptionDetails.Error.Message);
            }

            model.ErrorMessage = "An unexpected server error occurred while processing your request. Please try again later.";

            return View("Error", model);
        }
    }
}
