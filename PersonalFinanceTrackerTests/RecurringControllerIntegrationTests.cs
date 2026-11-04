using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.Ocsp;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using Syncfusion.EJ2.FileManager.Base;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

/*
 * 
 * The tests of this module must be run in the same order in order to get passed!!!
 * 
 */
namespace PersonalFinanceTrackerTests
{
    [TestFixture]
    public class RecurringControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private string? _uncategorizedRecurringId = null, _businessIncomeRecurringId = null, _educationRecurringId = null;
        private Recurring? _recurringRecord = null;
        IDbConnection _tempConnection;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // (Instantiate a standalone connection factory just for this lookup)

            // Load the test configuration
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(TestContext.CurrentContext.TestDirectory)
                .AddJsonFile("appsettings.test.json", optional: false)
                .Build();

            var tempConnectionFactory = new DbConnectionFactory(configuration);
            _tempConnection = tempConnectionFactory.GetDBConnection("pft_con_str_testenv");

            // Query the test database for the user ID using your existing helper
            _userId = DatabaseHelperForTests.GetUserIdByUserNameAndEmail(
                "PFT_Test_User_123", "test@test.tt", _tempConnection).ToString();

            // Otherwise tokens are generated for the _userId = 0
            if (_userId == "0")
                return;

            _businessIncomeRecurringId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Business Income", _tempConnection).ToString();

            var recurringRec = DatabaseHelperForTests.GetSingleRecordFromRecurringsTable(_userId, _tempConnection);
            if (recurringRec != null) {

                _recurringRecord = recurringRec;

            }
            else
            {

                DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("recurrings", _userId, _tempConnection, null, null);
                _recurringRecord = DatabaseHelperForTests.GetSingleRecordFromRecurringsTable(_userId, _tempConnection);

            }


            // Initialize the WebApplicationFactory with the fetched user ID
            _factory = new CustomWithConfigurationWebApplicationFactory(_userId);

            // Create the test client that sends real HTTP requests in-memory
            _client = _factory.CreateClient();

        }

        [OneTimeTearDown]
        public void OneTearDown()
        {
            _tempConnection?.Close();
            _tempConnection?.Dispose();

            _client?.Dispose();
            _factory?.Dispose();

        }


        [Test]
        [TestCase("/transactions/recurring")]
        public async Task RecurringsAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
            (string actionName)
        {

            // ARRANGE - Build an explicit request message to attach the test header
            var requestIndexGet = new HttpRequestMessage(HttpMethod.Get, actionName);

            // Configure Header to fail authentication for this request by TestAuthHandler 
            requestIndexGet.Headers.Add("X-Test-Unauthorized", "true");

            // ACT - Send the GET request through the custom client
            var responseIndexGet = await _client.SendAsync(requestIndexGet);

            // ASSERT - Verify the server successfully blocked the request with 401 Unauthorized
            Assert.That(responseIndexGet.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        }


        [Test]
        [TestCase("/transactions/recurring")]
        [TestCase("/Recurring/GetRecurring")]
        [TestCase("/Recurring/SaveRecurring")]
        [TestCase("/Recurring/DeleteRecurring")]
        public async Task RecurringsAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
            (string actionName)
        {

            var requestPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, actionName);
            var requestPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, actionName);

            // requestIndexPostInValidAuthorize
            requestPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");

            // InValidAntiforgeryToken
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId, false);
            requestPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);   // 'cookie' eliminated as null;

            switch (actionName)
            {

                case "/transactions/recurring":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    break;

                case "/Recurring/GetRecurring":
                case "/Recurring/DeleteRecurring":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { recurringId = _recurringRecord.RecurringId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { recurringId = _recurringRecord.RecurringId });
                    break;

                case "/Recurring/SaveRecurring":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _recurringRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _recurringRecord });
                    break;

                default:
                    break;

            }

            // ACT - Send the GET request through the custom client
            var responsePostInValidAuthorize = await _client.SendAsync(requestPostInValidAuthorize);
            var responsePostInValidAntiforgeryToken = await _client.SendAsync(requestPostInValidAntiforgeryToken);

            // ASSERT - Verify the server successfully blocked the request with 401 Unauthorized
            Assert.That(responsePostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responsePostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        }


        [Test]
        public async Task RecurringsIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/transactions/recurring");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3> Recurring Schedules "), Is.True);   // Contents of recurring view

        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task RecurringsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload
            (bool isFirstRequest)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/transactions/recurring?connString=pft_con_str_testenv");

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);
            request.Content = JsonContent.Create(new { firstRequest = isFirstRequest.ToString().ToLower(), pagination = new { } });


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();


            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True);

            /*
             * Parse the JSON tree:
             * Using JsonDocument allows to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             * or worry about deserialization exceptions if a property happens to be null (like recurringTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response   (recurrings, pagination, standardRecurrings, recurringTypes)
            Assert.That(root.TryGetProperty("recurrings", out var recurringsElement), Is.True);
            Assert.That(root.TryGetProperty("pagination", out var paginationElement), Is.True);

            // Assert on specific values inside pagination or recurrings
            var totalRecords = int.Parse(paginationElement.GetProperty("totalNumberOfRecords").GetString());

            Assert.That(totalRecords, Is.GreaterThanOrEqualTo(0));  // there is a possibility that recurrings has no data...

            Assert.That(recurringsElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(recurringsElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            if (isFirstRequest)
            {

                Assert.That(root.TryGetProperty("categoriesList", out var categoriesListElement), Is.True);
                Assert.That(root.TryGetProperty("accountsList", out var accountsListElement), Is.True);
                Assert.That(root.TryGetProperty("tagsList", out var tagsListElement), Is.True);
                Assert.That(root.TryGetProperty("goalsList", out var goalsListElement), Is.True);
                Assert.That(root.TryGetProperty("transactionRepeatIntervalList", out var transactionRepeatIntervalListElement), Is.True);
                Assert.That(root.TryGetProperty("userCurrencyDetails", out var userCurrencyDetailsElement), Is.True);

                // First Request: Check if recurrings & recurring types are arrays and has data (if expected)
                Assert.That(categoriesListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(categoriesListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(accountsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(accountsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(tagsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(tagsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(goalsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(goalsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(transactionRepeatIntervalListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(transactionRepeatIntervalListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(userCurrencyDetailsElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
                Assert.That(userCurrencyDetailsElement.GetPropertyCount(), Is.GreaterThanOrEqualTo(0));

                var currencyCode = userCurrencyDetailsElement.GetProperty("currencyCode").GetString();
                var currencySymbol = userCurrencyDetailsElement.GetProperty("currencySymbol").GetString();

                Assert.That(currencyCode, Is.Not.Empty.Or.Null);
                Assert.That(currencySymbol, Is.Not.Empty.Or.Null);

            }

        }


        [Test]
        // RecurringNextDate
        [TestCase("", null, null, "Dummy Test Recurring Description", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringNextDate", "Please provide recurring date!")]
        [TestCase(null, null, null, "Dummy Test Recurring Description", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringNextDate", "Please provide recurring date!")]
        [TestCase("", null, "0001-01-01", "Dummy Test Recurring Description", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringNextDate", "First date must be today or later!")]
        [TestCase(null, null, "0001-01-01", "Dummy Test Recurring Description", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringNextDate", "First date must be today or later!")]
        // RecurringDescription
        [TestCase("", null, null, "", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringDescription", "Please provide recurring description!")]
        [TestCase(null, null, null, "", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringDescription", "Please provide recurring description!")]
        [TestCase("", null, null, "This Recurring Description is more than 255 characters long...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah.", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringDescription", "Recurring description should not be more than 255 characters long!")]
        [TestCase(null, null, null, "This Recurring Description is more than 255 characters long...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah.", 234.56, "123", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "RecurringDescription", "Recurring description should not be more than 255 characters long!")]
        // RecurringAmount: NO NEED AS MODEL WILL FAIL FOR INVALID OR NULL VALUE
        // AccountId
        [TestCase("", null, null, "Dummy Test Recurring Description", 234.56, "", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "AccountId", "Please provide account id!")]
        [TestCase(null, null, null, "Dummy Test Recurring Description", 234.56, "", "123", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "AccountId", "Please provide account id!")]
        // CategoryId
        [TestCase("", null, null, "Dummy Test Recurring Description", 234.56, "123", "", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "CategoryId", "Please provide category id!")]
        [TestCase(null, null, null, "Dummy Test Recurring Description", 234.56, "123", "", null, null, (byte)1, null, (byte)1, (byte)1, null, 400, "CategoryId", "Please provide category id!")]

        public async Task RecurringsSaveRecurring_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? recurringId, ulong? userId, DateOnly? recurringNextDate, string recurringDescription, double recurringAmount, string accountId, string categoryId, string? goalId, string? tagId, byte transactionCategorization, int? transactionRepeatId, byte isTransactionRepeatActive, byte transactionRepeatInterval, DateOnly? transactionRepeatEndDate, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE

            // Replace TestCase assignment Values to actual values:
            recurringNextDate = recurringNextDate == DateOnly.Parse("0001-01-01") ? DateOnly.Parse(DateTime.Now.AddDays(-1).ToShortDateString()) : recurringNextDate;
            accountId = accountId == "123" ? _recurringRecord.AccountId : accountId;
            categoryId = categoryId == "123" ? _businessIncomeRecurringId : categoryId;  // Default to a valid standard recurring ID if "123" is passed in the test case

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Recurring/SaveRecurring?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Recurring invalidRecurring = new Recurring
            {

                RecurringId = recurringId,
                UserId = userId,
                RecurringNextDate = recurringNextDate,
                RecurringDescription = recurringDescription,
                RecurringAmount = recurringAmount,
                AccountId = accountId,
                CategoryId = categoryId,
                GoalId = goalId,
                TagId = tagId,
                TransactionCategorization = transactionCategorization,
                TransactionRepeatId = transactionRepeatId,
                IsTransactionRepeatActive = isTransactionRepeatActive,
                TransactionRepeatInterval = transactionRepeatInterval,
                TransactionRepeatEndDate = transactionRepeatEndDate

            };

            request.Content = JsonContent.Create(invalidRecurring);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.False, "The request failed with status: " + response.StatusCode);
            Assert.That((int)response.StatusCode, Is.EqualTo(expectedStatusCode));
            Assert.That(response.ReasonPhrase, Is.EqualTo("Bad Request"));

            // Parse the JSON response
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.False);

            // Get validation error message
            Assert.That(root.TryGetProperty("message", out var messageElement), Is.True);

            Assert.That(messageElement.TryGetProperty(failedValidationPropertyName, out var validationErrorElement), Is.True);
            Assert.That(validationErrorElement[0].GetString(), Is.EqualTo(expectedErrorMessage));

            // Validate if record is saved...
            invalidRecurring = DatabaseHelperForTests.GetRecurringRecordByDescription(invalidRecurring.RecurringDescription, _userId, _tempConnection);
            Assert.That(invalidRecurring, Is.Null);

        }

        [Test]
        public async Task RecurringsAddRecurring_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Recurring/SaveRecurring?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Delete any existing record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_trasactions_repeats", "transaction_repeat_id", _recurringRecord?.TransactionRepeatId.ToString(), _tempConnection);
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_recurring_schedules", "transaction_repeat_id", _recurringRecord?.TransactionRepeatId.ToString(), _tempConnection);

            _recurringRecord.RecurringId = "";    // To insert a new record!!!
            _recurringRecord.RecurringDescription = "Test Recurring Description";
            // _recurringRecord.RecurringDisplayOrder = 150;      // Automatically generated at save...
            request.Content = JsonContent.Create(_recurringRecord);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Check if the related record is generated in transactions repeats table
            bool relatedRecordGeneratedInTransactionRepeats = DatabaseHelperForTests.RecordExistsInTable("pft_trasactions_repeats", "transaction_repeat_id", (_recurringRecord.TransactionRepeatId + 1).ToString(), _userId, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            // Validate Newly saved record...Replace the existing '_recurringRecord' values
            _recurringRecord = DatabaseHelperForTests.GetRecurringRecordByDescription(_recurringRecord.RecurringDescription, _userId, _tempConnection);
            Assert.That(_recurringRecord?.RecurringId, Is.Not.Null);

            Assert.That(relatedRecordGeneratedInTransactionRepeats, Is.True);

        }

        // Category from "Income" to "Expense" or vise-a-versa, is not checked in the middle layer as already been checked by the UI.

        [Test]
        public async Task RecurringsUpdateRecurring_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Recurring/SaveRecurring?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Change the existing values of recurring record!
            string newAccountId = DatabaseHelperForTests.ExecuteSQLAndReturnId(DatabaseHelperForTests.GetInsertDummyAccountRecordAndOrRetrieveIdSQL(_userId), _tempConnection);

            _recurringRecord.RecurringNextDate = DateOnly.Parse(DateTime.Now.AddDays(2).ToShortDateString());
            _recurringRecord.RecurringDescription = _recurringRecord.RecurringDescription + " - Updated";
            _recurringRecord.RecurringAmount = _recurringRecord.RecurringAmount + 50;
            _recurringRecord.AccountId = newAccountId;
            _recurringRecord.CategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Other Income", _tempConnection).ToString();
            _recurringRecord.GoalId = null;
            _recurringRecord.TagId = null;
            _recurringRecord.TransactionCategorization = (byte)2;
            _recurringRecord.IsTransactionRepeatActive = (byte)0;
            _recurringRecord.TransactionRepeatInterval = (byte)2;  // set to weekly from bi-weekly
            _recurringRecord.TransactionRepeatEndDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(2));   // 1 month to 2 months

            // Get Existing values to validate the saved record
            Recurring updatedRecurring = new Recurring();
            updatedRecurring.RecurringId = _recurringRecord.RecurringId;
            updatedRecurring.UserId = _recurringRecord.UserId;
            updatedRecurring.RecurringNextDate = _recurringRecord.RecurringNextDate;
            updatedRecurring.RecurringDescription = _recurringRecord.RecurringDescription;
            updatedRecurring.RecurringAmount = _recurringRecord.RecurringAmount;
            updatedRecurring.AccountId = _recurringRecord.AccountId;
            updatedRecurring.CategoryId = _recurringRecord.CategoryId;
            updatedRecurring.GoalId = _recurringRecord.GoalId;
            updatedRecurring.TagId = _recurringRecord.TagId;
            updatedRecurring.TransactionCategorization = _recurringRecord.TransactionCategorization;
            updatedRecurring.TransactionRepeatId = _recurringRecord.TransactionRepeatId;
            updatedRecurring.IsTransactionRepeatActive = _recurringRecord.IsTransactionRepeatActive;
            updatedRecurring.TransactionRepeatInterval = _recurringRecord.TransactionRepeatInterval;
            updatedRecurring.TransactionRepeatEndDate = _recurringRecord.TransactionRepeatEndDate;


            request.Content = JsonContent.Create(updatedRecurring);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            // Validate Newly saved record...Replace the existing '_recurringRecord' values
            _recurringRecord = DatabaseHelperForTests.GetRecurringRecordByDescription(updatedRecurring.RecurringDescription, _userId, _tempConnection);
            Assert.That(_recurringRecord, Is.Not.Null);
            Assert.That(_recurringRecord.RecurringNextDate, Is.EqualTo(updatedRecurring.RecurringNextDate));
            Assert.That(_recurringRecord.RecurringDescription, Is.EqualTo(updatedRecurring.RecurringDescription));
            Assert.That(_recurringRecord.RecurringAmount, Is.EqualTo(updatedRecurring.RecurringAmount));
            Assert.That(_recurringRecord.AccountId, Is.EqualTo(updatedRecurring.AccountId));
            Assert.That(_recurringRecord.CategoryId, Is.EqualTo(updatedRecurring.CategoryId));
            Assert.That(_recurringRecord.GoalId, Is.EqualTo(updatedRecurring.GoalId));
            Assert.That(_recurringRecord.TagId, Is.EqualTo(updatedRecurring.TagId));
            Assert.That(_recurringRecord.TransactionCategorization, Is.EqualTo(updatedRecurring.TransactionCategorization));
            Assert.That(_recurringRecord.TransactionRepeatId, Is.EqualTo(updatedRecurring.TransactionRepeatId));
            Assert.That(_recurringRecord.IsTransactionRepeatActive, Is.EqualTo(updatedRecurring.IsTransactionRepeatActive));
            Assert.That(_recurringRecord.TransactionRepeatInterval, Is.EqualTo(updatedRecurring.TransactionRepeatInterval));
            Assert.That(_recurringRecord.TransactionRepeatEndDate, Is.EqualTo(updatedRecurring.TransactionRepeatEndDate));

        }


        [Test]
        public async Task RecurringsGetRecurring_AuthenticatedRequest_ReturnsRecurringRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Recurring/GetRecurring?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { recurringId = _recurringRecord?.RecurringId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Deserialize the JsonElement into your Recurring class
            Recurring? recurringFromRoot = root.Deserialize<Recurring>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Recommended if JSON keys are camelCase and C# properties are PascalCase
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            Assert.That(recurringFromRoot, Is.Not.Null);

            Assert.That(recurringFromRoot.RecurringNextDate, Is.EqualTo(_recurringRecord.RecurringNextDate));
            Assert.That(recurringFromRoot.RecurringDescription, Is.EqualTo(_recurringRecord.RecurringDescription));
            Assert.That(recurringFromRoot.RecurringAmount, Is.EqualTo(_recurringRecord.RecurringAmount));
            Assert.That(recurringFromRoot.AccountId, Is.EqualTo(_recurringRecord.AccountId));
            Assert.That(recurringFromRoot.CategoryId, Is.EqualTo(_recurringRecord.CategoryId));
            Assert.That(recurringFromRoot.GoalId, Is.EqualTo(_recurringRecord.GoalId));
            Assert.That(recurringFromRoot.TagId, Is.EqualTo(_recurringRecord.TagId));
            Assert.That(recurringFromRoot.TransactionCategorization, Is.EqualTo(_recurringRecord.TransactionCategorization));
            Assert.That(recurringFromRoot.TransactionRepeatId, Is.EqualTo(_recurringRecord.TransactionRepeatId));
            Assert.That(recurringFromRoot.IsTransactionRepeatActive, Is.EqualTo(_recurringRecord.IsTransactionRepeatActive));
            Assert.That(recurringFromRoot.TransactionRepeatInterval, Is.EqualTo(_recurringRecord.TransactionRepeatInterval));
            Assert.That(recurringFromRoot.TransactionRepeatEndDate, Is.EqualTo(_recurringRecord.TransactionRepeatEndDate));

        }

        [Test]
        /*
         *  
         *  When a recurring record is deleted:
         *  
         *      If RecurringNextDate IS NOT NULL: RecurringSchedules (TransactionRecurringId, RecurringNextDate) is updated, TrasactionsRepeats (TransactionId, IsTransactionRepeatActive) is updated
         *      If RecurringNextDate IS NULL: RecurringSchedules is deleted, TrasactionsRepeats is deleted
         *  
         *  When a recurring series is deleted:
         *      RecurringSchedules is deleted, TrasactionsRepeats is deleted
         *      
         *  
         */
        [TestCase(true, false)]    // 1st test
        [TestCase(false, false)]
        [TestCase(false, false)]
        [TestCase(false, false)]
        [TestCase(false, false)]
        [TestCase(false, false)]
        [TestCase(false, false)]    // Confirmed Extra/Additional TestCase
        [TestCase(false, true)]
        public async Task RecurringsDeleteRecurring_AuthenticatedRequest_DeletesRecurringRecord
            (bool isFirstEverDelete, bool deleteSeries)
        {

            // ARRANGE
            if (isFirstEverDelete || deleteSeries)
            {

                // Cleanup Dummy Data in Related Tables:
                DatabaseHelperForTests.CleanupDummyDataInRelatedTables("recurrings", _tempConnection);

                // Generate dummy data in related tables...pft_transactions, pft_recurring_schedules
                DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("recurrings", _userId, _tempConnection, null, null);

            }

            // Get the record before delete
            _recurringRecord = DatabaseHelperForTests.GetSingleRecordFromRecurringsTable(_userId, _tempConnection);

            // No need to ACT
            if (_recurringRecord == null)
            {
                Assert.That(true, Is.EqualTo(true));
                return;
            }

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Recurring/DeleteRecurring?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { deleteSeries = deleteSeries.ToString().ToLower(), recurringId = _recurringRecord.RecurringId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            // Check before delete (Client Request)
            
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nextRecurringRecord = DatabaseHelperForTests.GetRecurringRecordByDescription(_recurringRecord.RecurringDescription, _userId, _tempConnection);

            
            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            
            if (_recurringRecord.RecurringNextDate != null && deleteSeries == false)
            {

                Assert.That(nextRecurringRecord, Is.Not.Null);

                if (_recurringRecord.TransactionRepeatEndDate > _recurringRecord.RecurringNextDate.Value.AddDays(7))
                {
                    
                    Assert.That(nextRecurringRecord.RecurringNextDate, Is.Not.Null);
                }
                else
                {
                    Assert.That(nextRecurringRecord.RecurringNextDate, Is.Null);
                }

                Assert.That(_recurringRecord.RecurringId, Is.Not.EqualTo(nextRecurringRecord.RecurringId));
                Assert.That(nextRecurringRecord.RecurringNextDate, Is.Null.Or.Not.EqualTo(_recurringRecord.RecurringNextDate));
                Assert.That(nextRecurringRecord.RecurringNextDate, Is.Null.Or.EqualTo(_recurringRecord.RecurringNextDate.Value.AddDays(7)));    // As per the test data configuration!

                if (nextRecurringRecord.RecurringNextDate != null)
                {
                    Assert.That(nextRecurringRecord.IsTransactionRepeatActive, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(nextRecurringRecord.IsTransactionRepeatActive, Is.EqualTo(0));
                }

            }


            if (_recurringRecord.RecurringNextDate == null || deleteSeries)
            {

                Assert.That(nextRecurringRecord, Is.Null);

                // Confirm here that both tables have no records for the matching "transaction_repeat_id"
                bool recordExistsInRecurringSchedules = DatabaseHelperForTests.RecordExistsInTable("pft_recurring_schedules", "transaction_repeat_id", _recurringRecord.TransactionRepeatId.ToString(), null, _tempConnection);
                bool recordExistsInTransactionRepeats = DatabaseHelperForTests.RecordExistsInTable("pft_trasactions_repeats", "transaction_repeat_id", _recurringRecord.TransactionRepeatId.ToString(), null, _tempConnection);

                Assert.That(recordExistsInRecurringSchedules, Is.EqualTo(false));
                Assert.That(recordExistsInRecurringSchedules, Is.EqualTo(false));

            }


        }


    }

}

