using BCrypt.Net;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using System.Buffers.Text;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PersonalFinanceTracker.Controllers.Authentication
{

    [Route("auth")]
    public class AuthenticationController : Controller
    {
        // IDbConnectionFactory _factory;
        IUserRegistrationRepository _userRegistrationRepository;
        IUserRepository _userRepository;
        IEmailService _emailService;

        public AuthenticationController(IUserRepository userRepository, IUserRegistrationRepository userRegistrationRepository, IEmailService emailService)
        {

            //_factory = factory;
            _userRepository = userRepository;
            _userRegistrationRepository = userRegistrationRepository;
            _emailService = emailService;

        }

        [HttpGet("login")]
        [HttpGet("/")]  // Making this default page.
        public IActionResult Login()
        {

            return View();

        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(SessionUser userLogInDetails, string? connString)
        {

            if (ModelState.IsValid)
            {
                SessionUser? requestedUserDetails = _userRepository.GetUserDetailsForLogIn(userLogInDetails.UserNameOrEmail, connString);

                if (requestedUserDetails != null)
                {
                    // Verify password against the database hash
                    if (BCrypt.Net.BCrypt.EnhancedVerify(userLogInDetails.Password, requestedUserDetails.Password))
                    {
                        // Setup identity claims (This generates the secure token context)
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, requestedUserDetails.UserId.ToString()),
                            new Claim(ClaimTypes.Name, requestedUserDetails.UserNameOrEmail),
                            new Claim("CurrencyCode", requestedUserDetails.CurrencyCode),
                            new Claim("CurrencySymbol", requestedUserDetails.CurrencySymbol),
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        // Issue the encrypted tracking cookie to the browser
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                                                        new ClaimsPrincipal(claimsIdentity));

                        return RedirectToAction("Index", "Dashboard");
                    }
                }
            }

            ModelState.AddModelError("", "Invalid username or password!");

            return View(userLogInDetails);

        }


        [HttpGet("register")]
        public IActionResult Register()
        {

            return View("UserRegistration", (new UserRegistration()));

        }

        
        [HttpPost("register")]
        public IActionResult Register(UserRegistration user, string? connString)
        {

            if (ModelState.IsValid)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(user.Password);
                var secureTokenActual = _emailService.GenerateTokenAndHash();
                user.userEmailTokenHash = secureTokenActual.HashToStore;

                if (_userRegistrationRepository.CreateNewUser(user, connString))
                {
                    string emailVerificationLink = $"{Request.Scheme}://{Request.Host}" + "/verifyemail?token=" + secureTokenActual.OriginalBase64;
                    _emailService.SendEmail(user.Email, user.UserName, 1, emailVerificationLink);
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, $"The registration can't be performed now.\nPlease try again sometimes later!!!");
                }

            }

            return View("UserRegistration", user);

        }

        // Not implemented yet...
        [HttpGet("accessdenied")]
        public IActionResult AccessDenied()
        {

            return View();

        }

        /// <summary>
        /// Function for remote validation attribute
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        /// <remarks>HttpGet("Authentication/UserWithEmailAlreadyExists") has to be like this! So end user can not guess it with 'auth' or trying to access by url</remarks>
        [HttpGet("Authentication/UserWithEmailAlreadyExists")]
        public IActionResult UserWithEmailAlreadyExists(string email, string? connString)
        {
            
            if (Regex.IsMatch(email, APP_CONSTANTS.EMAIL_ADDRESS_VALIDATION_REGEX_PATTERN))
            {
                bool emailAlreadyExists = _userRegistrationRepository.UserWithEmailAlreadyExists(email, connString);

                if (emailAlreadyExists)
                {
                    return Json("User with this Email already exists!");
                }
            }
            else
            {
                return Json(false);
            }

            return Json(true);

        }

        /// <summary>
        /// Function for remote validation attribute
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        [HttpGet("Authentication/UserWithSameUserNameExists")]
        public IActionResult UserWithSameUserNameExists(string userName, string? connString)
        {

            if(userName.Length > 11)
            {
                bool userNameAlreadyExists = _userRegistrationRepository.UserWithSameUserNameExists(userName, connString);

                if (userNameAlreadyExists)
                {
                    return Json("User with this Username already exists!");
                }
            }
            else
            {
                return Json(false);
            }

            return Json(true);

        }


        // on logout button click!!!
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            
            await PerformSignOutAsync();
            return RedirectToAction("Login", "Authentication");

        }

        // On browser close (Works on Edge but NOT on Chrome. NOT PERFECT BUT WORKS!!!)
        [HttpPost("/Authentication/WindowClosedLogout")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> WindowClosedLogout()
        {

            await PerformSignOutAsync();
            return Ok(); // Clean 200 OK response for the beacon

        }


        private async Task PerformSignOutAsync()
        {

            // Remove the tracking cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        }

    }
}
