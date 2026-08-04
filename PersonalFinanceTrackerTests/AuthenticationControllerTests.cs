using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Platform.Extensions;
using Moq;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using NUnit.Framework;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Controllers.Authentication;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using DataValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace PersonalFinanceTrackerTests
{

    // To get the Attribute Validation Errors for the Model Classes
    public static class ModelValidationHelper
    {
        public static IList<DataValidationResult> ValidateModel(object model)
        {
            var results = new List<DataValidationResult>();
            var ctx = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, ctx, results, true);
            return results;
        }
    }

    [TestFixture]
    public class AuthenticationControllerTests
    {

        // DECLARE FIELDS ONCE HERE (added null-forgiving ! to prevent null warnings)
        private AuthenticationController _controller = null!;
        private IServiceScope _testScope = null!;
        // private IDbConnection _connection = null!;
        private IDbConnectionFactory _factory = null!;
        private IUserRegistrationRepository _userRegistrationRepository = null!;
        private IUserRepository _userRepository = null!;
        private IEmailService _emailService = null!;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // Configure Serilog for test environment
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("Logs/test-log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Load test configuration
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(TestContext.CurrentContext.TestDirectory)
                .AddJsonFile("appsettings.test.json", optional: false)
                .Build();

            // Register DI services
            var services = new ServiceCollection();
            services.AddSingleton(configuration);

            // ⭐⭐ THIS IS THE FIX ⭐⭐
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog();
            });

            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserRegistrationRepository, UserRegistrationRepository>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddTransient<AuthenticationController>();

            // Build provider
            var rootProvider = services.BuildServiceProvider();

            // Create scope
            _testScope = rootProvider.CreateScope();
            var provider = _testScope.ServiceProvider;

            // Resolve dependencies
            _factory = provider.GetRequiredService<IDbConnectionFactory>();
            _userRepository = provider.GetRequiredService<IUserRepository>();
            _userRegistrationRepository = provider.GetRequiredService<IUserRegistrationRepository>();
            _emailService = provider.GetRequiredService<IEmailService>();
            _controller = provider.GetRequiredService<AuthenticationController>();
            
        }

        // Runs in-between every single test
        [SetUp]
        public void Setup()
        {
            // Fresh ModelState
            _controller.ModelState.Clear();

            // Fresh HttpContext
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // Runs once at the END of all the tests
        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            _controller.Dispose();
            _testScope.Dispose();

        }

        [Test]
        // spaces are ignored by attributes as values...these tests passes...
        [TestCase("   ", "   ", "   ", "   ", "   ", null, true, "First Name is required!", "UserRegistration", 6)]
        [TestCase("   ", "   ", "   ", "   ", "   ", null, true, "Last Name is required!", "UserRegistration", 6)]
        [TestCase("   ", "   ", "   ", "   ", "   ", null, true, "Username is required!", "UserRegistration", 6)]
        [TestCase("   ", "   ", "   ", "   ", "   ", null, true, "Email is required!", "UserRegistration", 6)]
        [TestCase("   ", "   ", "   ", "   ", "   ", null, true, "Password is required!", "UserRegistration", 6)]
        [TestCase("   ", "   ", "   ", "   ", "   ", null, true, "Please confirm your password!", "UserRegistration", 6)]
        [TestCase("   ", "   ", "   ", "   ", "   ", null, false, "Please accept the terms and conditions!", "UserRegistration", 7)]
        // First Name is required!
        [TestCase("", "Doe", "", "", "", null, true, "First Name is required!", "UserRegistration", 5)]
        [TestCase("", "Doe", "", "", "", null, true, "Username is required!", "UserRegistration", 5)]
        [TestCase("", "Doe", "", "", "", null, true, "Email is required!", "UserRegistration", 5)]
        [TestCase("", "Doe", "", "", "", null, true, "Password is required!", "UserRegistration", 5)]
        [TestCase("", "Doe", "", "", "", null, true, "Please confirm your password!", "UserRegistration", 5)]
        [TestCase("", "Doe", "", "", "", null, false, "Please accept the terms and conditions!", "UserRegistration", 6)]
        // Last Name is required!
        [TestCase("John", "", "", "", "", null, true, "Last Name is required!", "UserRegistration", 5)]
        [TestCase("John", "", "", "", "", null, true, "Username is required!", "UserRegistration", 5)]
        [TestCase("John", "", "", "", "", null, true, "Email is required!", "UserRegistration", 5)]
        [TestCase("John", "", "", "", "", null, true, "Password is required!", "UserRegistration", 5)]
        [TestCase("John", "", "", "", "", null, true, "Please confirm your password!", "UserRegistration", 5)]
        [TestCase("John", "", "", "", "", null, false, "Please accept the terms and conditions!", "UserRegistration", 6)]
        // Username is required!
        [TestCase("John", "Doe", "", "", "", null, true, "Username is required!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "", "", "", null, true, "Email is required!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "", "", "", null, true, "Password is required!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "", "", "", null, true, "Please confirm your password!", "UserRegistration", 4)]
        // [TestCase("John", "Doe", "", "", "", null, true, "Please accept the terms and conditions!", "UserRegistration")]    // fails
        [TestCase("John", "Doe", "", "", "", null, false, "Please accept the terms and conditions!", "UserRegistration", 5)]
        // Username Must be 12 Characters long
        [TestCase("John", "Doe", "JohnDoe", "", "", null, true, "Username must be between 12 and 75 characters long!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "JohnDoe", "", "", null, true, "Email is required!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "JohnDoe", "", "", null, true, "Password is required!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "JohnDoe", "", "", null, true, "Please confirm your password!", "UserRegistration", 4)]
        //Password Required
        [TestCase("John", "Doe", "JohnDUser123", "", "", null, true, "Password is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        //Password Must be 12 Characters long
        [TestCase("John", "Doe", "JohnDUser123", "123", "", null, true, "Password must be atleast 12 characters long!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "JohnDUser123", "123", "", null, true, "Email is required!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "JohnDUser123", "123", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 4)]
        [TestCase("John", "Doe", "JohnDUser123", "123", "", null, true, "Please confirm your password!", "UserRegistration", 4)]
        // Password should contain 2 UPPERCASE letters, 2 lowercase letters, 2 numbers, 2 allowed special characters - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "password1234", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // all lower: Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "password1234", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "password1234", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Password should contain 2 UPPERCASE letters, 2 lowercase letters, 2 numbers, 2 allowed special characters - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "passWord1234", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // 1 Uppper: Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "passWord1234", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "passWord1234", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Password should contain 2 uppercase letters, 2 LOWERCASE letters, 2 numbers, 2 allowed special characters - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "PASSWORD12$#", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // 1 Uppper: Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "PASSWORD12$#", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "PASSWORD12$#", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Password should contain 2 uppercase letters, 2 LOWERCASE letters, 2 numbers, 2 allowed special characters - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "PAsSWORD12$#", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // 1 Uppper: Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "PAsSWORD12$#", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "PAsSWORD12$#", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Password should contain 2 uppercase letters, 2 lowercase letters, 2 numbers, 2 ALLOWED SPECIAL CHARACTERS - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "PassWord1234", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // Missing: Special chars. Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "PassWord1234", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord1234", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Password should contain 2 uppercase letters, 2 lowercase letters, 2 numbers, 2 ALLOWED SPECIAL CHARACTERS - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "PassWord123$", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // Missing: Special chars. Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "PassWord123$", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord123$", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Password should contain 2 uppercase letters, 2 lowercase letters, 2 NUMBERS, 2 allowed special characters - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "PassWord$%@^", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // Missing Numbers: Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "PassWord$%@^", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord$%@^", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Password should contain 2 uppercase letters, 2 lowercase letters, 2 NUMBERS, 2 allowed special characters - %$@!^&#().
        [TestCase("John", "Doe", "JohnDUser123", "PassWord$%@1", "", null, true, "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!", "UserRegistration", 3)] // Missing Numbers: Password must contain atleast 2 uppercase, 2 lowercase, 2 numbers and 2 of %$@!^&#()
        [TestCase("John", "Doe", "JohnDUser123", "PassWord$%@1", "", null, true, "Email is required!", "UserRegistration", 3)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord$%@1", "", null, true, "Please confirm your password!", "UserRegistration", 3)]
        // Confirm Password Required!
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "", null, true, "Please confirm your password!", "UserRegistration", 2)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "", null, true, "Email is required!", "UserRegistration", 2)]
        // Confirm Password must be same as Password
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "password43@^", null, true, "The password and confirmation password do not match!", "UserRegistration", 2)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "password43@^", null, true, "Email is required!", "UserRegistration", 2)]
        // email should be in right format!
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "test", true, "Please enter a valid email address!", "UserRegistration", 1)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "test@", true, "Please enter a valid email address!", "UserRegistration", 1)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "test@test", true, "Please enter a valid email address!", "UserRegistration", 1)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "test@test.", true, "Please enter a valid email address!", "UserRegistration", 1)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "test@test.t", true, "Please enter a valid email address!", "UserRegistration", 1)]

        // should not get the validation errors for other than 'termsAndConditionAccepted' as it is intentionally 'false' to check other values validation errors!!!Should Pass!!!
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "test@test.tt", false, "Please accept the terms and conditions!", "UserRegistration", 1)]
        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "chiragpatel.android@gmail.com", false, "Please accept the terms and conditions!", "UserRegistration", 1)]
        public void AuthenticationUserRegistration_ShouldPassWithModelValidationErrorMessages
            (string firstName, string lastName, string userName, string password, string confirmPassword, string? email, bool termsAndConditionAccepted, string expectedErrorMessage, string expectedViewName, int totalValidationErrors)
        {

            // Arrange
            var model = new UserRegistration
            {
                FirstName = firstName,
                LastName = lastName,
                UserName = userName,
                Password = password,
                ConfirmPassword = confirmPassword,
                Email = email,
                AgreeToTermsAndPrivacyPolicy = termsAndConditionAccepted
            };


            // Act

            // Run DataAnnotation validation - to get the Validation Errors from the ValidationAttributes of the 'UserRegistration' class.
            var validationResults = ModelValidationHelper.ValidateModel(model);

            // Copy validation errors into ModelState of the Controller
            foreach (var result in validationResults)
            {
                foreach (var member in result.MemberNames)
                {
                    _controller.ModelState.AddModelError(member, result.ErrorMessage);
                }
            }

            // Get the returned view name
            var response = _controller.Register(model, "pft_con_str_testenv") as ViewResult;

            // Assert
            Assert.That(_controller.ModelState.IsValid, Is.False);          // Confirm the state of Controller's Model
            Assert.That(response.ViewName, Is.EqualTo(expectedViewName));   // Confirm if the returned view is correct
            Assert.That(                                                    // Confirm the error message...
                _controller.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Any(e => e.ErrorMessage == expectedErrorMessage)
            );
            Assert.That(validationResults.Count(), Is.EqualTo(totalValidationErrors));
            
        }


        [TestCase("John", "Doe", "JohnDUser123", "PassWord43@^", "PassWord43@^", "test@test.tt", true, "", "Login", 0, 1)]
        public void AuthenticationUserRegistration_ShouldPassForNewRecordInsertion
            (string firstName, string lastName, string userName, string password, string confirmPassword, string? email, bool termsAndConditionAccepted, string expectedErrorMessage, string expectedViewName, int totalValidationErrors, int expectedRecordCountInDB = 0)
        {
            // Arrange:
            int actRecordCountInDB = 0;
            var model = new UserRegistration
            {
                FirstName = firstName,
                LastName = lastName,
                UserName = userName,
                Password = password,
                ConfirmPassword = confirmPassword,
                Email = email,
                AgreeToTermsAndPrivacyPolicy = termsAndConditionAccepted
            };

            // Act
            var response = _controller.Register(model, "pft_con_str_testenv") as RedirectToActionResult;

            // Check here if the user is inserted into the database!!!
            
            IDbConnection _connection = _factory.GetDBConnection("pft_con_str_testenv");
            actRecordCountInDB = DatabaseHelperForTests.GetUserCountsByUserNameAndEmail(userName, email, _connection);
            DatabaseHelperForTests.DeleteNewUserByUserNameAndEmail(userName, email, _connection);

            
            _connection.Close();
            _connection.Dispose();

            // Assert
            Assert.That(_controller.ModelState.IsValid, Is.True);
            Assert.That(                                                    // Confirm the error message...
                     _controller.ModelState.Values
                    .SelectMany(v => v.Errors).Count, Is.EqualTo(totalValidationErrors));
            Assert.That(response.ActionName, Is.EqualTo(expectedViewName));
            Assert.That(actRecordCountInDB, Is.EqualTo(expectedRecordCountInDB));
        }


        // User with this Email already exists!
        [TestCase("invalidEmail@form", "False")]        // Not a valid email
        [TestCase("chiragpatel.android@gmail.com", "User with this Email already exists!")]
        [TestCase("not_existing_email@gmail.com", "True")]
        public void AuthenticationUserWithEmailAlreadyExists_ShouldPassWithResponseMessages
            (string? existingEmail, string expectedErrorMessage)
        {

            // Act
            var result = _controller.UserWithEmailAlreadyExists(existingEmail, "pft_con_str_testenv") as JsonResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.ToString(), Is.EqualTo(expectedErrorMessage)); // or whatever your action returns

        }

        // User with this Name already exists!
        [TestCase("ChiragPatel", "False")]       // When Username length is less than 12 characters!!!
        [TestCase("ChiragPatelTestUser", "User with this Username already exists!")]
        [TestCase("NotAnExistingUser", "True")]
        public void AuthenticationUserWithSameUserNameExists_ShouldPassWithResponseMessages
            (string? existingEmail, string expectedErrorMessage)
        {

            // Act
            var result = _controller.UserWithSameUserNameExists(existingEmail, "pft_con_str_testenv") as JsonResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.ToString(), Is.EqualTo(expectedErrorMessage)); // or whatever your action returns

        }


        [TestCase("Login", "Login")]
        
        [TestCase("Register", "UserRegistration")]
        [TestCase("AccessDenied", "AccessDenied")]
        public void AuthenticationGetActions_ShouldPassReturnedViewNames(string actionToCall, string expectedViewName)
        {
            ViewResult response = new ViewResult();
            switch (actionToCall)
            {
                case "Login":
                    response = _controller.Login() as ViewResult;
                    break;
                case "Register":
                    response = _controller.Register() as ViewResult;
                    break;
                case "AccessDenied":
                    response = _controller.AccessDenied() as ViewResult;
                    break;
                default:
                    throw new ArgumentException($"Unknown action: {actionToCall}");

            }

            // Assert
            Assert.That(response, Is.Not.Null, $"The action '{actionToCall}' did not return a ViewResult.");

            // Fallback to the action name if the ViewName property is null
            string actualViewName = response.ViewName ?? actionToCall;

            Assert.That(actualViewName, Is.EqualTo(expectedViewName));

        }

        // TODO: PENDING
        [TestCase("Logout", "Login")]
        public async Task AuthenticationGetAyncActions_ShouldPassReturnedViewNames(string actionToCall, string expectedViewName)
        {
            RedirectToActionResult response = new RedirectToActionResult(null, null, null);
            switch (actionToCall)
            {
                case "Logout":
                    response = await _controller.Logout() as RedirectToActionResult;
                    break;
                default:
                    throw new ArgumentException($"Unknown action: {actionToCall}");

            }

            // Assert
            Assert.That(response, Is.Not.Null, $"The action '{actionToCall}' did not return a ViewResult.");

            // Fallback to the action name if the ViewName property is null
            string actualViewName = response.ActionName ?? actionToCall;

            Assert.That(actualViewName, Is.EqualTo(expectedViewName));

        }



    }

}
