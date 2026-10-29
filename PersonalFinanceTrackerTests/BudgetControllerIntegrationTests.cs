
using Google.Protobuf.WellKnownTypes;
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
using Syncfusion.EJ2.Linq;
using System.Data;
using System.Globalization;
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
    public class BudgetControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;

        private string _educationCategoryIdForUser;

        private string _budgetIdForEducationCategoryIdForUser;

        private Budget? _budgetSettingsRecord = null;
        private BudgetDetails? _budgetDetailsRecord = null;
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

            // Set "Education" CategoryId:
            _educationCategoryIdForUser = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Education", _tempConnection).ToString();
            _budgetIdForEducationCategoryIdForUser = DatabaseHelperForTests.GetBudgetIdByCategoryName("Education", _tempConnection).ToString();

            var budgetSettingsRecord = DatabaseHelperForTests.GetBudgetRecordByCategoryName("Education", _userId, _tempConnection);
            if (budgetSettingsRecord != null) 
            {
                _budgetSettingsRecord = budgetSettingsRecord;
            }
            else
            {
                _budgetSettingsRecord = new Budget  
                {
                    UserId = ulong.Parse(_userId),
                    CategoryId = _educationCategoryIdForUser,
                    CategoryName = "Education",
                    BudgetToBeRolledOver = 0,
                    BudgetIfUnderSettings = 0,
                    BudgetIfOverSettings = 0
                };
            }

            var budgetDetailsRecord = DatabaseHelperForTests.GetBudgetDetailsByBudgetId(_budgetIdForEducationCategoryIdForUser, _userId, _tempConnection);
            if (budgetDetailsRecord != null)
            {
                _budgetDetailsRecord = budgetDetailsRecord;
            }
            else
            {
                _budgetDetailsRecord = new BudgetDetails
                {

                    BudgetId = _budgetIdForEducationCategoryIdForUser,
                    userId = ulong.Parse(_userId),
                    CategoryId = _educationCategoryIdForUser,
                    CategoryName = "Education",
                    CategoryType = "-1",
                    BudgetDateDigits = "20000101",
                    BudgetMonthYear = "Jan 2000",
                    BudgetAmount = 50

                };
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
        [TestCase("/budgets")]
        public async Task AccountsAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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
        [TestCase("/budgets")]
        [TestCase("/Budget/SaveBudget")]
        [TestCase("/Budget/DeleteBudget")]
        [TestCase("/Budget/GetBudgetSettings")]
        [TestCase("/Budget/SaveBudgetSettings")]
        public async Task AccountsAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
            (string actionName)
        {

            var requestPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, actionName);
            var requestPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, actionName);

            // InValidAuthorize
            requestPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");

            // InValidAntiforgeryToken
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId, false);
            requestPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);   // 'cookie' eliminated as null;

            switch (actionName)
            {

                case "/budgets":
                    var contents = new { budgetDetailsStartDate = new DateOnly(DateTime.Now.Year, 1, 1).ToString("yyyy-MM-dd"), budgetDetailsEndDate = new DateOnly(DateTime.Now.Year, 12, 31).ToString("yyyy-MM-dd") };
                    requestPostInValidAuthorize.Content = JsonContent.Create(contents);
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(contents);
                    break;

                case "/Budget/SaveBudget":
                case "/Budget/DeleteBudget":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _budgetDetailsRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _budgetDetailsRecord });
                    break;

                case "/Budget/GetBudgetSettings":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { });
                    break;

                case "/Budget/SaveBudgetSettings":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _budgetSettingsRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _budgetSettingsRecord });
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
        public async Task BudgetsIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {
            
            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/budgets");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>Budgets"), Is.True);   // Contents of budget view

        }


        [Test]
        [TestCase("2026-12-31", "2026-01-01", 400, "BudgetDetailsStartDate", "Start date must be earlier than end date!")]
        public async Task BudgetsIndexPost_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
           (DateOnly budgetDetailsStartDateParameter, DateOnly budgetDetailsEndDateParameter, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/budgets?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            BudgetDetailsFilter invalidBudgetDetailsFilter = new BudgetDetailsFilter
            {

                BudgetDetailsStartDate = budgetDetailsStartDateParameter,
                BudgetDetailsEndDate = budgetDetailsEndDateParameter

            };

            request.Content = JsonContent.Create(invalidBudgetDetailsFilter);
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

        }

        
        [Test]
        public async Task BudgetsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/budgets?connString=pft_con_str_testenv");
            
            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);
            request.Content = JsonContent.Create(new { request = "true", pagination = new { } });

            // ACT
            var responseRequest = await _client.SendAsync(request);
            var jsonResponseRequest = await responseRequest.Content.ReadAsStringAsync();

            
            // ASSERT
            Assert.That(responseRequest.IsSuccessStatusCode, Is.True);

            /*
             *  Parse the JSON tree:
             *  Using JsonDocument allows to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             *  or worry about deserialization exceptions if a property happens to be null (like budgetTypes when request is false).
             */
            using var docRequest = JsonDocument.Parse(jsonResponseRequest);
            var rootRequest = docRequest.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(rootRequest.TryGetProperty("budgetDetails", out var budgetDetailsElement), Is.True);
            Assert.That(rootRequest.TryGetProperty("budgetsMonthYear", out var budgetsMonthYearElement), Is.True);
            Assert.That(rootRequest.TryGetProperty("userCurrencyDetails", out var userCurrencyDetailsElement), Is.True);
            
            // Check if budgets & budget types are arrays and has data (if expected)
            Assert.That(budgetDetailsElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(budgetDetailsElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            Assert.That(budgetsMonthYearElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(budgetsMonthYearElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            // Assert on specific values inside pagination or reports
            var currencyCode = userCurrencyDetailsElement.GetProperty("currencyCode").GetString();
            var currencySymbol = userCurrencyDetailsElement.GetProperty("currencySymbol").GetString();

            Assert.That(currencyCode, Is.Not.Empty.Or.Null);
            Assert.That(currencySymbol, Is.Not.Empty.Or.Null);

        }

        
        [Test]
        [TestCase("SaveBudget", "", "", "Education", "-1", "20000101", "Jan 2000", 250, 400, "BudgetId", "Budget id is required!")]
        [TestCase("SaveBudget", "6666666666666666666", "", "Education", "-1", "", "Jan 2000", 250, 400, "BudgetDateDigits", "Budget date digits is required!")]
        [TestCase("DeleteBudget", "", "", "Education", "-1", "20000101", "Jan 2000", 250, 400, "BudgetId", "Budget id is required!")]
        [TestCase("DeleteBudget", "6666666666666666666", "", "Education", "-1", "", "Jan 2000", 250, 400, "BudgetDateDigits", "Budget date digits is required!")]
        public async Task BudgetsSaveAndDeleteBudget_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string controllerActionName, string budgetId, string categoryId, string categoryName, string categoryType, string budgetDateDigits, string budgetMonthYear, double? budgetAmount, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {
            
            categoryId = _educationCategoryIdForUser;

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, $"/Budget/{controllerActionName}?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            BudgetDetails invalidBudgetDetails = new BudgetDetails
            {
                BudgetId = budgetId,
                CategoryId = categoryId,
                CategoryName = categoryName,
                CategoryType = categoryType,
                BudgetDateDigits = budgetDateDigits,
                BudgetMonthYear = budgetMonthYear,
                BudgetAmount = budgetAmount
            };

            request.Content = JsonContent.Create(invalidBudgetDetails);
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

        }

        
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        [TestCase(false)]
        public async Task BudgetsSaveBudgetDetails_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord
            ( bool firstEverEntry )
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Budget/SaveBudget?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Reset entries for specified budget id.
            if (firstEverEntry)
            {
                DatabaseHelperForTests.DeleteRecordFromTableById("pft_budgets_details", "budget_id", _budgetDetailsRecord.BudgetId, _tempConnection);
            }
            else
            {
                _budgetDetailsRecord.BudgetDateDigits = (ulong.Parse(_budgetDetailsRecord.BudgetDateDigits) + 100).ToString();
                _budgetDetailsRecord.BudgetMonthYear = DateTime.ParseExact(_budgetDetailsRecord.BudgetDateDigits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None).ToString("MMM yyyy");
                _budgetDetailsRecord.BudgetAmount = _budgetDetailsRecord.BudgetAmount + 100;
            }

            double? actualBudgetBaseAmountBeforeInsert = DatabaseHelperForTests.GetBudgetBaseAmountBudgetId(_budgetDetailsRecord.BudgetId, _userId, _tempConnection);

            // Get existing budget details for comparision in database update
            BudgetDetails newBudgetDetailsBeforeSave = new BudgetDetails
            {

                userId = _budgetDetailsRecord.userId,
                BudgetId = _budgetDetailsRecord.BudgetId,
                CategoryId = _budgetDetailsRecord.CategoryId,
                CategoryName = _budgetDetailsRecord.CategoryName,
                CategoryType = _budgetDetailsRecord.CategoryType,
                BudgetDateDigits = _budgetDetailsRecord.BudgetDateDigits,
                BudgetMonthYear = _budgetDetailsRecord.BudgetMonthYear,
                BudgetAmount = _budgetDetailsRecord.BudgetAmount

            };
            
            request.Content = JsonContent.Create(_budgetDetailsRecord);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            double? actualBudgetBaseAmountAfterInsert = DatabaseHelperForTests.GetBudgetBaseAmountBudgetId(_budgetDetailsRecord.BudgetId, _userId, _tempConnection);


            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            
            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            // Validate Newly saved record...Replaces the existing '_budgetDetailsRecord' values
            _budgetDetailsRecord = DatabaseHelperForTests.GetBudgetDetailsByBudgetId(_budgetDetailsRecord.BudgetId, _userId, _tempConnection);
            Assert.That(_budgetDetailsRecord, Is.Not.Null);
            Assert.That(_budgetDetailsRecord.BudgetDateDigits, Is.EqualTo(newBudgetDetailsBeforeSave.BudgetDateDigits));
            Assert.That(_budgetDetailsRecord.BudgetAmount, Is.EqualTo(newBudgetDetailsBeforeSave.BudgetAmount));

            if (firstEverEntry)
            {
                Assert.That(actualBudgetBaseAmountBeforeInsert, Is.Null);
                Assert.That(actualBudgetBaseAmountAfterInsert, Is.EqualTo(newBudgetDetailsBeforeSave.BudgetAmount));
            }
            else
            {
                Assert.That(actualBudgetBaseAmountBeforeInsert, Is.Not.Null);
                Assert.That(actualBudgetBaseAmountAfterInsert, Is.Not.EqualTo(newBudgetDetailsBeforeSave.BudgetAmount));
            }

        }

        
        /*
         * 
         * When an Budget Detail is deleted:
         * Only the specified record in the Budget Details table is deleted!
         * 
         */
        [Test]
        public async Task BudgetsDeleteBudget_AuthenticatedRequest_DeletesBudgetRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Budget/DeleteBudget?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create( _budgetDetailsRecord );
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nullBudgetDetailsRecord = DatabaseHelperForTests.GetBudgetDetailsByBudgetIdAndBudgetDateDigits(_userId, _budgetDetailsRecord.BudgetId, _budgetDetailsRecord.BudgetDateDigits, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nullBudgetDetailsRecord, Is.Null);

        }


        [Test]
        public async Task BudgetsGetBudgetSettings_AuthenticatedRequest_ReturnsBudgetRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Budget/GetBudgetSettings?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            Assert.That(root.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(root.GetArrayLength(), Is.GreaterThan(0));  // Permanent Categories can't be deleted

        }

        
        [Test]
        [TestCase("", "Education", (byte)0, (byte)0, (byte)0, 400, "CategoryId", "Category id is required!")]
        public async Task BudgetsSaveBudgetSettings_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string categoryId, string? categoryName, byte budgetToBeRolledOver, byte? budgetIfUnderSettings, byte? budgetIfOverSettings, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, $"/Budget/SaveBudgetSettings?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Budget invalidBudget = new Budget
            {
                UserId = ulong.Parse(_userId),
                CategoryId = categoryId,
                CategoryName = categoryName,
                BudgetToBeRolledOver = budgetToBeRolledOver,
                BudgetIfUnderSettings = budgetIfUnderSettings,
                BudgetIfOverSettings = budgetIfOverSettings
            };

            request.Content = JsonContent.Create(invalidBudget);
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

        }


        [Test]
        public async Task BudgetsSaveBudgetSettings_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Budget/SaveBudgetSettings?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Budget updatedBudget = new Budget();
            updatedBudget.UserId = _budgetSettingsRecord.UserId;
            updatedBudget.CategoryId = _budgetSettingsRecord.CategoryId;
            updatedBudget.CategoryName = _budgetSettingsRecord.CategoryName;
            updatedBudget.BudgetToBeRolledOver = (byte)(_budgetSettingsRecord.BudgetToBeRolledOver == 0 ? 1 : 0);
            updatedBudget.BudgetIfOverSettings = (byte)(_budgetSettingsRecord.BudgetIfOverSettings == 0 ? 1 : 0);
            updatedBudget.BudgetIfUnderSettings = (byte)(_budgetSettingsRecord.BudgetIfUnderSettings == 0 ? 1 : 0);

            request.Content = JsonContent.Create(updatedBudget);
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

            // Validate Newly saved record...Replace the existing '_budgetRecord' values
            _budgetSettingsRecord = DatabaseHelperForTests.GetBudgetRecordByCategoryName(updatedBudget.CategoryName, _userId, _tempConnection);

            Assert.That(_budgetSettingsRecord.CategoryId, Is.EqualTo(updatedBudget.CategoryId));
            Assert.That(_budgetSettingsRecord.BudgetToBeRolledOver, Is.EqualTo(updatedBudget.BudgetToBeRolledOver));
            Assert.That(_budgetSettingsRecord.BudgetIfUnderSettings, Is.EqualTo(updatedBudget.BudgetIfUnderSettings));
            Assert.That(_budgetSettingsRecord.BudgetIfOverSettings, Is.EqualTo(updatedBudget.BudgetIfOverSettings));

        }

    }

}
