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
using Syncfusion.EJ2.Charts;
using Syncfusion.EJ2.FileManager.Base;
using System.Data;
using System.Diagnostics;
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
    public class TransactionControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private string? _businessIncomeCategoryId = null;
        private string? _educationCategoryId = null;
        private Transaction? _transactionRecord = null;

        private (string[] accountIds, string[] categoryIds, string[] goalIds, string[] tagIds) _lists = new(new string[] { }, new string[] { }, new string[] { }, new string[] { });
        private bool _areDummyDataInsertedForTransactionFilterTest = false;
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

            _businessIncomeCategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Business Income", _tempConnection).ToString();
            _educationCategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Education", _tempConnection).ToString();

            var transactionRec = DatabaseHelperForTests.GetSingleRecordFromTransactionsTable(_userId, _tempConnection);
            if (transactionRec != null) {

                _transactionRecord = transactionRec;

            }
            else
            {

                DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("transactions", _userId, _tempConnection, null, null);
                _transactionRecord = DatabaseHelperForTests.GetSingleRecordFromTransactionsTable(_userId, _tempConnection);

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
        [TestCase("/transactions")]
        public async Task TransactionsAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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
        [TestCase("/transactions")]
        [TestCase("/Transaction/GetTransaction")]
        [TestCase("/Transaction/SaveTransaction")]
        [TestCase("/Transaction/DeleteTransaction")]
        public async Task TransactionsAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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

                case "/transactions":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", transactionFilter = new { }, pagination = new { } });    
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", transactionFilter = new { }, pagination = new { } });
                    break;

                case "/Transaction/GetTransaction":
                case "/Transaction/DeleteTransaction":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { transactionId = _transactionRecord.TransactionId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { transactionId = _transactionRecord.TransactionId });
                    break;

                case "/Transaction/SaveTransaction":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _transactionRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _transactionRecord });
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
        public async Task TransactionsIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/transactions");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3> Transactions"), Is.True);   // Contents of transaction view

        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task TransactionsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload
            (bool isFirstRequest)
        {

            // ARRANGE
            // Create a transaction filter to retrieve transaction record(s)
            TransactionFilter transactionFilter = new TransactionFilter();
            transactionFilter.TransactionFilterStartDate = DateOnly.FromDateTime(DateTime.Now);
            transactionFilter.TransactionFilterEndDate = DateOnly.FromDateTime(DateTime.Now);
            transactionFilter.TransactionFilterCategorization = 0;      // 0 = all, 1 = manual, 2 = auto;
            transactionFilter.TransactionFilterType = 0;                // 0 = all, 1 = Regular, 2 - Recurring

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/transactions?connString=pft_con_str_testenv");

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);
            request.Content = JsonContent.Create(new { pagination = new { }, transactionFilter = transactionFilter, firstRequest = isFirstRequest.ToString().ToLower() });


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();


            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True);

            /*
             * Parse the JSON tree:
             * Using JsonDocument allows to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             * or worry about deserialization exceptions if a property happens to be null (like transactionTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response   (transactions, pagination, standardTransactions, transactionTypes)
            Assert.That(root.TryGetProperty("transactions", out var transactionsElement), Is.True);
            Assert.That(root.TryGetProperty("pagination", out var paginationElement), Is.True);

            // Assert on specific values inside pagination or transactions
            var totalRecords = int.Parse(paginationElement.GetProperty("totalNumberOfRecords").GetString());

            Assert.That(totalRecords, Is.GreaterThanOrEqualTo(0));  // there is a possibility that transactions has no data...

            Assert.That(transactionsElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(transactionsElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            if (isFirstRequest)
            {

                Assert.That(root.TryGetProperty("categoriesList", out var categoriesListElement), Is.True);
                Assert.That(root.TryGetProperty("accountsList", out var accountsListElement), Is.True);
                Assert.That(root.TryGetProperty("tagsList", out var tagsListElement), Is.True);
                Assert.That(root.TryGetProperty("goalsList", out var goalsListElement), Is.True);
                Assert.That(root.TryGetProperty("transactionCategorizationList", out var transactionCategorizationListElement), Is.True);
                Assert.That(root.TryGetProperty("transactionTypeList", out var transactionTypeListElement), Is.True);
                Assert.That(root.TryGetProperty("transactionRepeatIntervalList", out var transactionRepeatIntervalListElement), Is.True);
                Assert.That(root.TryGetProperty("userCurrencyDetails", out var userCurrencyDetailsElement), Is.True);

                // First Request: Check if transactions & transaction types are arrays and has data (if expected)
                Assert.That(categoriesListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(categoriesListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(accountsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(accountsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(tagsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(tagsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(goalsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(goalsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(transactionCategorizationListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(transactionCategorizationListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(transactionTypeListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(transactionTypeListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
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

        
        // transactionDate + transactionRepeatEndDate
        [TestCase("", "0002-01-01", "Test Transaction Description", 150, "123", "123", null, null, (byte) 1, (byte)1, "1", null, (byte) 0, (byte)1, "0001-01-01", 400, "TransactionDate", "The date must be earlier than recurring end date!")]
        [TestCase(null, "0002-01-01", "Test Transaction Description", 150, "123", "123", null, null, (byte) 1, (byte)1, "1", null, (byte) 0, (byte)1, "0001-01-01", 400, "TransactionDate", "The date must be earlier than recurring end date!")]
        // transactionDate + isTransactionRepeatActive
        [TestCase("", "0001-01-01", "Test Transaction Description", 150, "123", "123", null, null, (byte) 1, (byte)1, "1", null, (byte) 1, (byte)1, null, 400, "TransactionDate", "The date must be today or greater to generate a transaction recurrings!")]
        [TestCase(null, "0001-01-01", "Test Transaction Description", 150, "123", "123", null, null, (byte) 1, (byte)1, "1", null, (byte) 1, (byte)1, null, 400, "TransactionDate", "The date must be today or greater to generate a transaction recurrings!")]

        // transactionDescription
        [TestCase("", "0001-01-01", "", 150, "123", "123", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, null, 400, "TransactionDescription", "Please provide transaction description!")]
        [TestCase(null, "0001-01-01", "", 150, "123", "123", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, null, 400, "TransactionDescription", "Please provide transaction description!")]
        [TestCase("", "0001-01-01", "This transaction description is more than 255 characters long...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah......", 150, "123", "123", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, null, 400, "TransactionDescription", "Transaction description should not be more than 255 characters long!")]
        [TestCase(null, "0001-01-01", "This transaction description is more than 255 characters long...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah......", 150, "123", "123", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, null, 400, "TransactionDescription", "Transaction description should not be more than 255 characters long!")]
        
        // transactionAmount
        [TestCase("", "0001-01-01", "Test Transaction Description", 150, "123", "123", null, null, (byte)1, (byte)1, "-1", null, (byte)0, (byte)1, "0001-01-01", 400, "TransactionAmount", "For expense type of category, amount should be negative(-) otherwise positive(+).")]
        [TestCase(null, "0001-01-01", "Test Transaction Description", 150, "123", "123", null, null, (byte)1, (byte)1, "-1", null, (byte)0, (byte)1, "0001-01-01", 400, "TransactionAmount", "For expense type of category, amount should be negative(-) otherwise positive(+).")]
        [TestCase("", "0001-01-01", "Test Transaction Description", -150, "123", "123", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, "0001-01-01", 400, "TransactionAmount", "For expense type of category, amount should be negative(-) otherwise positive(+).")]
        [TestCase(null, "0001-01-01", "Test Transaction Description", -150, "123", "123", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, "0001-01-01", 400, "TransactionAmount", "For expense type of category, amount should be negative(-) otherwise positive(+).")]

        // accountId
        [TestCase("", "0001-01-01", "Test Transaction Description", 150, "", "123", null, null, (byte)1, (byte)1, "-1", null, (byte)0, (byte)1, "0001-01-01", 400, "AccountId", "Please provide account id!")]
        [TestCase(null, "0001-01-01", "Test Transaction Description", 150, "", "123", null, null, (byte)1, (byte)1, "-1", null, (byte)0, (byte)1, "0001-01-01", 400, "AccountId", "Please provide account id!")]

        // categoryId
        [TestCase("", "0001-01-01", "Test Transaction Description", 150, "123", "", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, "0001-01-01", 400, "CategoryId", "Please provide category id!")]
        [TestCase(null, "0001-01-01", "Test Transaction Description", 150, "123", "", null, null, (byte)1, (byte)1, "1", null, (byte)0, (byte)1, "0001-01-01", 400, "CategoryId", "Please provide category id!")]

        public async Task TransactionsSaveTransaction_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? transactionId, DateOnly transactionDate, string transactionDescription, double transactionAmount, string accountId, string categoryId, string? goalId, string? tagId, byte transactionCategorization, byte transactionType, string? categoryType, int? transactionRepeatId, byte? isTransactionRepeatActive, byte? transactionRepeatInterval, DateOnly? transactionRepeatEndDate, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            
            accountId = accountId == "123" ? _transactionRecord.AccountId : accountId;
            categoryId = categoryId == "123" ? (categoryType == "-1" ? _educationCategoryId : _businessIncomeCategoryId) : categoryId;  // Default to a valid standard transaction ID if "123" is passed in the test case

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/SaveTransaction?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Transaction invalidTransaction = new Transaction
            {

                TransactionId = transactionId,
                UserId = ulong.Parse(_userId),
                TransactionDate = transactionDate,
                TransactionDescription = transactionDescription,
                TransactionAmount = transactionAmount,
                AccountId = accountId,
                CategoryId = categoryId,
                GoalId = goalId,
                TagId = tagId,
                CategoryType = categoryType,
                TransactionCategorization = transactionCategorization,
                TransactionRepeatId = transactionRepeatId,
                IsTransactionRepeatActive = isTransactionRepeatActive,
                TransactionRepeatInterval = transactionRepeatInterval,
                TransactionRepeatEndDate = transactionRepeatEndDate

            };

            request.Content = JsonContent.Create(invalidTransaction);
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
            invalidTransaction = DatabaseHelperForTests.GetTransactionRecordByDescription(invalidTransaction.TransactionDescription, _userId, _tempConnection);
            Assert.That(invalidTransaction, Is.Null);

        }

        // Add inserts a new records in the Transactions table if isTransactionRepeatActive == 0
        // And inserts a new records in the Transactions, Transaction Repeats and Recurring Schedules tables if isTransactionRepeatActive == 1
        [Test]
        [TestCase(0)]   // Should only generate record in Transactions Table
        [TestCase(1)]   // Should generate records in Transactions, Transaction Repeats and Recurring Schedules
        public async Task TransactionsAddTransaction_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord
            (byte isTransactionRepeatActive)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/SaveTransaction?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Delete any existing record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_transactions", "transaction_id", _transactionRecord?.TransactionId.ToString(), _tempConnection);
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("transactions", _tempConnection, new string[] { "pft_recurring_schedules", "pft_trasactions_repeats"});

            _transactionRecord.TransactionId = "";    // To insert a new record!!!
            _transactionRecord.TransactionDescription = "Test Transaction Description";
            _transactionRecord.IsTransactionRepeatActive = isTransactionRepeatActive;
            _transactionRecord.TransactionType = isTransactionRepeatActive == 0 ? (byte) 1: (byte) 2;

            request.Content = JsonContent.Create(_transactionRecord);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Check if the related record is generated in transactions repeats table
            bool newRecordsGeneratedInRelatedTables = DatabaseHelperForTests.AreNewRecordsCreatedInTransactionRepeatsAndRecurringSchedules(_transactionRecord.TransactionDescription, _userId, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            // Validate Newly saved record...Replace the existing '_transactionRecord' values
            _transactionRecord = DatabaseHelperForTests.GetTransactionRecordByDescription(_transactionRecord.TransactionDescription, _userId, _tempConnection);
            Assert.That(_transactionRecord?.TransactionId, Is.Not.Null);
            
            if(isTransactionRepeatActive == 0)
            {
                Assert.That(newRecordsGeneratedInRelatedTables, Is.False);
                Assert.That(_transactionRecord.TransactionType, Is.EqualTo((byte)1));   // '1' is Default, but provided in the test here...
            }
            else
            {
                Assert.That(newRecordsGeneratedInRelatedTables, Is.True);
                Assert.That(_transactionRecord.TransactionType, Is.EqualTo((byte)2));   // To test the value
            }

        }

        // Update only updates the transactions table record values. 
        [Test]
        public async Task TransactionsUpdateTransaction_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/SaveTransaction?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Change the existing values of transaction record!
            string newAccountId = DatabaseHelperForTests.ExecuteSQLAndReturnId(DatabaseHelperForTests.GetInsertDummyAccountRecordAndOrRetrieveIdSQL(_userId), _tempConnection);

            _transactionRecord.TransactionDate = DateOnly.Parse(DateTime.Now.AddDays(7).ToShortDateString());
            _transactionRecord.TransactionDescription = _transactionRecord.TransactionDescription + " - Updated";
            _transactionRecord.TransactionAmount = _transactionRecord.TransactionAmount + 50;
            _transactionRecord.AccountId = newAccountId;
            _transactionRecord.CategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Education", _tempConnection).ToString();
            _transactionRecord.GoalId = null;
            _transactionRecord.TagId = null;
            _transactionRecord.TransactionCategorization = (byte)2;
            
            _transactionRecord.TransactionType = (byte)2;
            _transactionRecord.CategoryType = "";
            _transactionRecord.TransactionRepeatId = null;
            _transactionRecord.IsTransactionRepeatActive = (byte)0;

            _transactionRecord.TransactionRepeatInterval = null;  // set to weekly from bi-weekly
            _transactionRecord.TransactionRepeatEndDate = null;   // 1 month to 2 months

            // Get Existing values to validate the saved record
            Transaction updatedTransaction = new Transaction();
            updatedTransaction.TransactionId = _transactionRecord.TransactionId;
            updatedTransaction.UserId = _transactionRecord.UserId;
            updatedTransaction.TransactionDate = _transactionRecord.TransactionDate;
            updatedTransaction.TransactionDescription = _transactionRecord.TransactionDescription;
            updatedTransaction.TransactionAmount = _transactionRecord.TransactionAmount;
            updatedTransaction.AccountId = _transactionRecord.AccountId;
            updatedTransaction.CategoryId = _transactionRecord.CategoryId;
            updatedTransaction.GoalId = _transactionRecord.GoalId;
            updatedTransaction.TagId = _transactionRecord.TagId;
            updatedTransaction.TransactionCategorization = _transactionRecord.TransactionCategorization;
            updatedTransaction.TransactionType = _transactionRecord.TransactionType;
            
            /*
                updatedTransaction.CategoryType = _transactionRecord.CategoryType;
                updatedTransaction.TransactionRepeatId  = _transactionRecord.TransactionRepeatId;
                updatedTransaction.IsTransactionRepeatActive = _transactionRecord.IsTransactionRepeatActive;
                updatedTransaction.TransactionRepeatInterval = _transactionRecord.TransactionRepeatInterval;
                updatedTransaction.TransactionRepeatEndDate = _transactionRecord.TransactionRepeatEndDate;
            */

            request.Content = JsonContent.Create(updatedTransaction);
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

            // Validate Newly saved record...Replace the existing '_transactionRecord' values
            _transactionRecord = DatabaseHelperForTests.GetTransactionRecordByDescription(updatedTransaction.TransactionDescription, _userId, _tempConnection);
            Assert.That(_transactionRecord, Is.Not.Null);
            Assert.That(_transactionRecord.TransactionId, Is.EqualTo(updatedTransaction.TransactionId));
            Assert.That(_transactionRecord.TransactionDate, Is.EqualTo(updatedTransaction.TransactionDate));
            Assert.That(_transactionRecord.TransactionDescription, Is.EqualTo(updatedTransaction.TransactionDescription));
            Assert.That(_transactionRecord.TransactionAmount, Is.EqualTo(updatedTransaction.TransactionAmount));
            Assert.That(_transactionRecord.AccountId, Is.EqualTo(updatedTransaction.AccountId));
            Assert.That(_transactionRecord.CategoryId, Is.EqualTo(updatedTransaction.CategoryId));
            Assert.That(_transactionRecord.GoalId, Is.EqualTo(updatedTransaction.GoalId));
            Assert.That(_transactionRecord.TagId, Is.EqualTo(updatedTransaction.TagId));
            // NOT UPDATED
            Assert.That(_transactionRecord.TransactionCategorization, Is.Not.EqualTo(updatedTransaction.TransactionCategorization));
            Assert.That(_transactionRecord.TransactionType, Is.Not.EqualTo(updatedTransaction.TransactionType));  // Default Value which is not updated when saved for update

        }


        [Test]
        public async Task TransactionsGetTransaction_AuthenticatedRequest_ReturnsTransactionRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/GetTransaction?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { transactionId = _transactionRecord?.TransactionId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Deserialize the JsonElement into your Transaction class
            Transaction? transactionFromRoot = root.Deserialize<Transaction>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Recommended if JSON keys are camelCase and C# properties are PascalCase
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            Assert.That(transactionFromRoot, Is.Not.Null);

            Assert.That(transactionFromRoot.TransactionDate, Is.EqualTo(_transactionRecord.TransactionDate));
            Assert.That(transactionFromRoot.TransactionDescription, Is.EqualTo(_transactionRecord.TransactionDescription));
            Assert.That(transactionFromRoot.TransactionAmount, Is.EqualTo(_transactionRecord.TransactionAmount));
            Assert.That(transactionFromRoot.AccountId, Is.EqualTo(_transactionRecord.AccountId));
            Assert.That(transactionFromRoot.CategoryId, Is.EqualTo(_transactionRecord.CategoryId));
            Assert.That(transactionFromRoot.GoalId, Is.EqualTo(_transactionRecord.GoalId));
            Assert.That(transactionFromRoot.TagId, Is.EqualTo(_transactionRecord.TagId));
            Assert.That(transactionFromRoot.TransactionCategorization, Is.EqualTo(_transactionRecord.TransactionCategorization));
            Assert.That(transactionFromRoot.TransactionType, Is.EqualTo(_transactionRecord.TransactionType));    // Not updated when saved for update

            Assert.That(transactionFromRoot.TransactionRepeatId, Is.EqualTo(_transactionRecord.TransactionRepeatId));
            Assert.That(transactionFromRoot.IsTransactionRepeatActive, Is.EqualTo(_transactionRecord.IsTransactionRepeatActive));
            Assert.That(transactionFromRoot.TransactionRepeatInterval, Is.EqualTo(_transactionRecord.TransactionRepeatInterval));
            Assert.That(transactionFromRoot.TransactionRepeatEndDate, Is.EqualTo(_transactionRecord.TransactionRepeatEndDate));

        }

        [Test]
        /*
         *  
         *  When a transaction record is deleted:
         *  
         *    A single record from pft_transactions table is deleted for the user  
         *  
         */
        public async Task TransactionsDeleteTransaction_AuthenticatedRequest_DeletesTransactionRecord()
        {

            // ARRANGE

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/DeleteTransaction?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { transactionId = _transactionRecord.TransactionId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            // Check before delete (Client Request)

            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nextTransactionRecord = DatabaseHelperForTests.GetTransactionRecordByDescription(_transactionRecord.TransactionDescription, _userId, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nextTransactionRecord, Is.Null);

        }


        // Transaction Filter Validations And Query Record Tests - Start Here

        [Test]
        // transactionFilterStartDate + transactionFilterEndDate: Model will fail for null values
        [TestCase("2025-01-01", "2024-01-01", null, null, "Invalid Transaction Description", null, null, (byte) 1, (byte)1, 400, "TransactionFilterStartDate", "Start date must be earlier than end date!")]

        public async Task TransactionsIndexPostTransaction_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string transactionFilterStartDate, string transactionFilterEndDate, string? transactionFilterCategoryId, string? transactionFilterAccountId, string? transactionFilterDescription, string? transactionFilterTagId, string? transactionFilterGoalId, byte transactionFilterCategorization, byte transactionFilterType, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            transactionFilterAccountId = transactionFilterAccountId == "123" ? _transactionRecord.AccountId : transactionFilterAccountId;
            transactionFilterCategoryId = transactionFilterCategoryId == "123" ? _businessIncomeCategoryId : transactionFilterCategoryId;  // Default to a valid standard transaction ID if "123" is passed in the test case

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/transactions?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            TransactionFilter invalidTransactionFilter = new TransactionFilter
            {

                TransactionFilterStartDate = DateOnly.Parse(transactionFilterStartDate),
                TransactionFilterEndDate = DateOnly.Parse(transactionFilterEndDate),
                TransactionFilterCategoryId = transactionFilterCategoryId,
                TransactionFilterAccountId = transactionFilterAccountId,
                TransactionFilterDescription = transactionFilterDescription,
                TransactionFilterTagId = transactionFilterTagId,
                TransactionFilterGoalId = transactionFilterGoalId,
                TransactionFilterCategorization = transactionFilterCategorization,
                TransactionFilterType = transactionFilterType,
            };

            request.Content = JsonContent.Create(new { pagination = new { }, transactionFilter = invalidTransactionFilter, firstRequest = "true" });
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

    }

}

