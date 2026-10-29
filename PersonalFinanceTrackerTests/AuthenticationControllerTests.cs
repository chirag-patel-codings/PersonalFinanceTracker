using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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


namespace PersonalFinanceTrackerTests
{

    [TestFixture]
    public class AuthenticationControllerTests
    {

        // DECLARE FIELDS ONCE HERE (added null-forgiving ! to prevent null warnings)
        private AuthenticationController _controller = null!;
        private IServiceScope _testScope = null!;
        private IDbConnectionFactory _factory = null!;

        IDbConnection _connection = null!;
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

            // THIS IS THE FIX
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
            _controller = provider.GetRequiredService<AuthenticationController>();      // NO HttpContext Services added to this controller instance

            
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

            _connection = _factory.GetDBConnection("pft_con_str_testenv");

        }

        // Runs once at the END of all the tests
        [OneTimeTearDown]
        public void OneTimeTeardown()
        {

            _connection.Close();
            _connection.Dispose();

            _controller.Dispose();
            _testScope.Dispose();

        }

        /*
        *   THIS TEST MUST RUN BEFORE ANY OTHER TESTS
        *   When a new user is created:
        *   A new record added to users table
        *   37 new categories records added to the categories table
        *   34 budget (settings) records added to the budgets table as "Permanent Categories" are excluded from budget settings!
        *   1 email varification token record added to the tokens table
        */
        [Test]
        [TestCase("PFT_Test", "User123", "PFT_Test_User_123", "PassWord43@^", "PassWord43@^", "test@test.tt", true, "", "Login", 0, 1)]
        public void Authentication_UserRegistration_ShouldPassForNewRecordInsertion
            (string firstName, string lastName, string userName, string password, string confirmPassword, string? email, bool termsAndConditionAccepted, string expectedErrorMessage, string expectedViewName, int totalValidationErrors, int expectedRecordCountInDB = 0)
        {

            // Arrange:
            string existingUserId = "0";
            string newUserId = "0";
            int userRecordsCountAfterDelete = 0;
            int categoryRecordsCountAfterDelete = 0;
            int budgetRecordsCountAfterDelete = 0;
            int userTokenRecordsCountAfterDelete = 0;
            
            int userRecordsCountAfterInsert = 0;
            int categoryRecordsCountAfterInsert = 0;
            int budgetRecordsCountAfterInsert = 0;
            int userTokenRecordsCountAfterInsert = 0;

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

            // Check here if the user is inserted into the database!!!
            // IDbConnection _connection = _factory.GetDBConnection("pft_con_str_testenv");

            existingUserId = DatabaseHelperForTests.GetUserIdByUserNameAndEmail(userName, email, _connection).ToString();
            DatabaseHelperForTests.DeleteNewUserByUserNameAndEmail(userName, email, _connection);       // DELETE IF ANY EXSITING USER WITH THE SAME NAME AND EMAIL
            
            userRecordsCountAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(existingUserId, "pft_users", "user_id", existingUserId, _connection);
            categoryRecordsCountAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(existingUserId, "pft_categories", "user_id", existingUserId, _connection);
            budgetRecordsCountAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(existingUserId, "pft_budgets", "user_id", existingUserId, _connection);
            userTokenRecordsCountAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(existingUserId, "pft_user_tokens", "user_id", existingUserId, _connection);

            // Act
            var response = _controller.Register(model, "pft_con_str_testenv") as RedirectToActionResult;
            
            userRecordsCountAfterInsert = DatabaseHelperForTests.GetUserCountsByUserNameAndEmail(userName, email, _connection);  // Keep this user for to run all the tests!!!
            newUserId = DatabaseHelperForTests.GetUserIdByUserNameAndEmail(userName, email, _connection).ToString();

            categoryRecordsCountAfterInsert = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(newUserId, "pft_categories", "user_id", newUserId, _connection);
            budgetRecordsCountAfterInsert = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(newUserId, "pft_budgets", "user_id", newUserId, _connection);
            userTokenRecordsCountAfterInsert = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(newUserId, "pft_user_tokens", "user_id", newUserId, _connection);

            // Assert
            Assert.That(userRecordsCountAfterDelete, Is.EqualTo(0));
            Assert.That(categoryRecordsCountAfterDelete, Is.EqualTo(0));
            Assert.That(budgetRecordsCountAfterDelete, Is.EqualTo(0));
            Assert.That(userTokenRecordsCountAfterDelete, Is.EqualTo(0));

            Assert.That(_controller.ModelState.IsValid, Is.True);
            Assert.That(                                                    // Confirm the error message...
                     _controller.ModelState.Values
                    .SelectMany(v => v.Errors).Count, Is.EqualTo(totalValidationErrors));
            Assert.That(response.ActionName, Is.EqualTo(expectedViewName));

            Assert.That(userRecordsCountAfterInsert, Is.EqualTo(expectedRecordCountInDB));
            Assert.That(categoryRecordsCountAfterInsert, Is.EqualTo(37));
            Assert.That(budgetRecordsCountAfterInsert, Is.EqualTo(34)); 
            Assert.That(userTokenRecordsCountAfterInsert, Is.EqualTo(1));

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
        public void Authentication_UserRegistration_ShouldPassForModelValidationErrorMessages
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

        

        [Test]
        [TestCase("", "PassWord43@^", "Username or Email is required!", null, 1)]
        [TestCase(null, "PassWord43@^", "Username or Email is required!", null, 1)]
        [TestCase("PFT_Test_User_123", "", "Password is required!", null, 1)]
        [TestCase("PFT_Test_User_123", null, "Password is required!", null, 1)]
        public async Task Authentication_SessionUser_ShouldPassForModelValidationErrorMessages
            (string? userNameOrEmail, string? password, string expectedErrorMessage, string? expectedViewName, int totalValidationErrors)
        {
            
            // Arrange
            var model = new SessionUser
            {
                UserNameOrEmail = userNameOrEmail,
                Password = password
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
            var response = await _controller.Login(model, "pft_con_str_testenv") as ViewResult;

            // Assert
            Assert.That(_controller.ModelState.IsValid, Is.False);          // Confirm the state of Controller's Model
            
            Assert.That(response.ViewName, Is.EqualTo(expectedViewName));   // Confirm if the returned view is correct
            Assert.That(                                                    // Confirm the error message...
                _controller.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Any(e => e.ErrorMessage == expectedErrorMessage)
            );
            Assert.That(                                                    // Confirm the error message...
                _controller.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Any(e => e.ErrorMessage == "Invalid username or password!")
            );
            Assert.That(validationResults.Count(), Is.EqualTo(totalValidationErrors));
            Assert.That(_controller.ModelState.Count, Is.EqualTo(totalValidationErrors + 1));   // One more error added!!!

        }


        [Test]
        [TestCase("PFT_Test_User_123", "PassWord43@^", "Dashboard", "Index", "Login")]
        [TestCase("PFT_Test_User_123", "PassWord43@^", "Authentication", "Login", "Logout")]
        public async Task Authentication_LoginAndLogOutActions_ShouldPassWithSuccess
            (string? userNameOrEmail, string? password, string expectedControllerName, string expectedActionName, string actionName)
        {

            AuthenticationController controller = GetAuthenticationControllerWithDifferentHttpContext();

            RedirectToActionResult response;
            // Arrange Model
            if (actionName == "Login")
            {

                var model = new SessionUser
                {
                    UserNameOrEmail = userNameOrEmail,
                    Password = password
                };

                // Act (Run Validation & Call Login)
                var validationResults = ModelValidationHelper.ValidateModel(model);

                foreach (var result in validationResults)
                {
                    foreach (var member in result.MemberNames)
                    {
                        controller.ModelState.AddModelError(member, result.ErrorMessage);
                    }
                }

                response = await controller.Login(model, "pft_con_str_testenv") as RedirectToActionResult;
            }
            else
            {
                response = await controller.Logout() as RedirectToActionResult;
            }
            
            // Assert
            // Login
            if (actionName == "Login")
            {
                Assert.That(controller.ModelState.IsValid, Is.True);
            }

            Assert.That(response, Is.Not.Null);
            Assert.That(response.ControllerName, Is.EqualTo(expectedControllerName));
            Assert.That(response.ActionName, Is.EqualTo(expectedActionName));

        }


        [Test]
        [TestCase("PFT_Test_User_123", "PassWord43@^", "Dashboard", "Index", 200)]
        public async Task Authentication_LoginAndWindowClosedLogoutActions_ShouldPassWithSuccess
            (string? userNameOrEmail, string? password, string expectedLoginControllerName, string expectedLoginActionName, int windowClosedLogoutStatusCode)
        {

            AuthenticationController controller = GetAuthenticationControllerWithDifferentHttpContext();

            // Arrange Model
            var model = new SessionUser
            {
                UserNameOrEmail = userNameOrEmail,
                Password = password
            };

            // Act (Run Validation & Call Login)
            var validationResults = ModelValidationHelper.ValidateModel(model);

            foreach (var result in validationResults)
            {
                foreach (var member in result.MemberNames)
                {
                    controller.ModelState.AddModelError(member, result.ErrorMessage);
                }
            }

            var responseLogin = await controller.Login(model, "pft_con_str_testenv") as RedirectToActionResult;
            var reponseWindowClosedLogout = await controller.WindowClosedLogout() as OkResult;

            // Assert
            // Login
            Assert.That(responseLogin, Is.Not.Null);
            Assert.That(controller.ModelState.IsValid, Is.True);
            Assert.That(responseLogin.ControllerName, Is.EqualTo(expectedLoginControllerName));
            Assert.That(responseLogin.ActionName, Is.EqualTo(expectedLoginActionName));

            // Logout
            Assert.That(reponseWindowClosedLogout.StatusCode, Is.EqualTo(windowClosedLogoutStatusCode));
            

        }


        // User with this Email already exists!
        [TestCase("invalidEmail@form", "False")]        // Not a valid email
        [TestCase("chiragpatel.android@gmail.com", "User with this Email already exists!")]
        [TestCase("not_existing_email@gmail.com", "True")]
        public void Authentication_UserWithEmailAlreadyExists_ShouldPassWithResponseMessages
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
        [TestCase("pft_pft_admin", "User with this Username already exists!")]
        [TestCase("NotAnExistingUser", "True")]
        public void Authentication_UserWithSameUserNameExists_ShouldPassWithResponseMessages
            (string? existingEmail, string expectedErrorMessage)
        {

            // Act
            var result = _controller.UserWithSameUserNameExists(existingEmail, "pft_con_str_testenv") as JsonResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.ToString(), Is.EqualTo(expectedErrorMessage)); // or whatever your action returns

        }


        [TestCase("/", "/")]    
        [TestCase("Login", "Login")]
        [TestCase("Register", "UserRegistration")]
        [TestCase("AccessDenied", "AccessDenied")]
        public void Authentication_GetActions_ShouldPassReturnedViewNames(string actionToCall, string expectedViewName)
        {

            ViewResult response = new ViewResult();

            switch (actionToCall)
            {
                case "/":
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


        // Returns the 'Authentication Controller' with 'AddControllers', 'AddCookie' AND 'DefaultHttpContext' to make an http request. 
        private AuthenticationController GetAuthenticationControllerWithDifferentHttpContext()
        {

            // Load test configuration
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(TestContext.CurrentContext.TestDirectory)
                .AddJsonFile("appsettings.test.json", optional: false)
                .Build();

            // Build the Service Provider for this test run (includes Authentication & your dependencies)
            var services = new ServiceCollection();
            services.AddSingleton(configuration); // Your configuration instance

            services.AddControllers();

            // Required for Cookie Authentication inside unit tests
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie();

            services.AddLogging();
            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserRegistrationRepository, UserRegistrationRepository>();
            services.AddScoped<IEmailService, EmailService>();

            var serviceProvider = services.BuildServiceProvider();

            // Create a DefaultHttpContext and assign the ServiceProvider to RequestServices
            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            };

            // Instantiate the controller directly (passing its dependencies from the container)
            // This avoids sharing a dirty/cached controller instance across tests.
            var controller = new AuthenticationController(
                serviceProvider.GetRequiredService<IDbConnectionFactory>(),
                serviceProvider.GetRequiredService<IUserRepository>(),
                serviceProvider.GetRequiredService<IUserRegistrationRepository>(),
                serviceProvider.GetRequiredService<IEmailService>() // adjust if your logger is different
            )
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext,
                    RouteData = new RouteData(),
                    ActionDescriptor = new ControllerActionDescriptor()
                }
            };

            return controller;

        }



    }

}
