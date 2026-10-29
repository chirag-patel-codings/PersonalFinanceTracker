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
    public class TransactionRuleControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private TransactionRule? _transactionRuleRecord = null;
        private bool _areTransactionsDataGeneratedForAdd = false;

        private bool _areTransactionsDataGeneratedForUpdate = false;
        private bool _areNewAccountAndGoalIdsGenerated = false; // For updating existing TransactionRule
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

            var transactionRuleRec = DatabaseHelperForTests.GetSingleRecordFromTransactionRulesTable(_userId, _tempConnection);
            if (transactionRuleRec != null)
            {

                _transactionRuleRecord = transactionRuleRec;

            }
            else
            {

                DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("transactionrules", _userId, _tempConnection, null, null);
                _transactionRuleRecord = DatabaseHelperForTests.GetSingleRecordFromTransactionRulesTable(_userId, _tempConnection);

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
        [TestCase("/transactions/rules")]
        public async Task TransactionRulesAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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
        [TestCase("/transactions/rules")]
        [TestCase("/TransactionRule/GetTransactionRule")]
        [TestCase("/TransactionRule/SaveTransactionRule")]
        [TestCase("/TransactionRule/DeleteTransactionRule")]
        public async Task TransactionRulesAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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

                case "/transactions/rules":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    break;

                case "/TransactionRule/GetTransactionRule":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { transactionRuleId = _transactionRuleRecord.TransactionRuleId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { transactionRuleId = _transactionRuleRecord.TransactionRuleId });
                    break;

                case "/TransactionRule/SaveTransactionRule":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _transactionRuleRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _transactionRuleRecord });
                    break;

                case "/TransactionRule/DeleteTransactionRule":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { transactionRuleId = _transactionRuleRecord.TransactionRuleId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { transactionRuleId = _transactionRuleRecord.TransactionRuleId });
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
        public async Task TransactionRulesIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {
            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/transactions/rules");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>TransactionRule Rules "), Is.True);   // Contents of transactionRule view

        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task TransactionRulesIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload
            (bool isFirstRequest)
        {
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/transactions/rules?connString=pft_con_str_testenv");

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);
            request.Content = JsonContent.Create(new { firstRequest = isFirstRequest.ToString(), pagination = new { } });

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True);

            /*
             * Parse the JSON tree:
             * Using JsonDocument allows to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             * or worry about deserialization exceptions if a property happens to be null (like transactionRuleTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(root.TryGetProperty("transactionRules", out var transactionRulesElement), Is.True);
            Assert.That(root.TryGetProperty("pagination", out var paginationElement), Is.True);

            // Assert on specific values inside pagination or transactionRules
            var totalRecords = int.Parse(paginationElement.GetProperty("totalNumberOfRecords").GetString());

            Assert.That(totalRecords, Is.GreaterThanOrEqualTo(0));  // there is a possibility that transactionRules has no data...

            // Check if transactionRules & transactionRule types are arrays and has data (if expected)
            Assert.That(transactionRulesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(transactionRulesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            if (isFirstRequest)
            {

                Assert.That(root.TryGetProperty("categoriesList", out var categoriesListElement), Is.True);
                Assert.That(root.TryGetProperty("accountsList", out var accountsListElement), Is.True);
                Assert.That(root.TryGetProperty("goalsList", out var goalsListElement), Is.True);
                Assert.That(root.TryGetProperty("transactionTextMatchComparisionList", out var transactionTextMatchComparisionListElement), Is.True);
                Assert.That(root.TryGetProperty("transactionAmountMatchComparisionList", out var transactionAmountMatchComparisionListElement), Is.True);
                Assert.That(root.TryGetProperty("userCurrencyDetails", out var userCurrencyDetailsElement), Is.True);

                Assert.That(categoriesListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(categoriesListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(accountsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(accountsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(transactionTextMatchComparisionListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(transactionTextMatchComparisionListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(transactionAmountMatchComparisionListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(transactionAmountMatchComparisionListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

                // Assert on specific values inside pagination or reports
                Assert.That(userCurrencyDetailsElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
                Assert.That(userCurrencyDetailsElement.GetPropertyCount(), Is.GreaterThanOrEqualTo(2));

                var currencyCode = userCurrencyDetailsElement.GetProperty("currencyCode").GetString();
                var currencySymbol = userCurrencyDetailsElement.GetProperty("currencySymbol").GetString();

                Assert.That(currencyCode, Is.Not.Empty.Or.Null);
                Assert.That(currencySymbol, Is.Not.Empty.Or.Null);

            }

        }


        [Test]
        [TestCase(null, null, "", null, "Transaction to match text", (byte)0, (byte)0, null, null, 400, "CategoryId", "Please provide category!")]
        [TestCase(null, null, "123", null, "", (byte)0, (byte)0, null, null, 400, "TransactionTextToMatch", "Please provide match text!")]
        [TestCase(null, null, "123", null, "Transaction to match text...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", (byte)0, (byte)0, null, null, 400, "TransactionTextToMatch", "Transaction description match text should not be more than 255 characters long!")]
        [TestCase(null, null, "123", null, "Transaction to match text", (byte)0, (byte)3, null, null, 400, "TransactionExactAmount", "Exact amount is required!")]
        [TestCase(null, null, "123", null, "Transaction to match text", (byte)0, (byte)3, 250, "Transaction Description Override...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah", 400, "TransactionDescriptionOverride", "Transaction description should not be more than 255 characters long!")]
        public async Task TransactionRulesSaveTransactionRule_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? transactionRuleId, ulong? userId, string categoryId, string? goalId, string transactionTextToMatch, byte transactionTextMatchComparision, byte transactionAmountMatchComparision, double? transactionExactAmount, string? transactionDescriptionOverride, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            string accountId = _transactionRuleRecord.AccountId;
            categoryId = categoryId == "123" ? _transactionRuleRecord.CategoryId : categoryId;

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/TransactionRule/SaveTransactionRule?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            TransactionRule invalidTransactionRule = new TransactionRule
            {

                TransactionRuleId = transactionRuleId,
                UserId = userId,
                AccountId = accountId,
                CategoryId = categoryId,
                GoalId = goalId,
                TransactionTextToMatch = transactionTextToMatch,
                TransactionTextMatchComparision = transactionTextMatchComparision,
                TransactionAmountMatchComparision = transactionAmountMatchComparision,
                TransactionExactAmount = transactionExactAmount,
                TransactionDescriptionOverride = transactionDescriptionOverride

            };

            request.Content = JsonContent.Create(new { data = invalidTransactionRule, applyRule = 0 });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.False, "The request failed with status: " + response.StatusCode);
            Assert.That((int)response.StatusCode, Is.EqualTo(expectedStatusCode));
            Assert.That(response.ReasonPhrase, Is.EqualTo("Bad Request"));

            // Parse the JSON response: return Ok(new { success = true });
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
            invalidTransactionRule = DatabaseHelperForTests.GetTransactionRuleRecordByTransactionTextToMatch(invalidTransactionRule.TransactionTextToMatch, _userId, _tempConnection);
            Assert.That(invalidTransactionRule, Is.Null);

        }


        [Test]
        [TestCase(0, "Contains Description", (byte)0, (byte)0, 0, null, 0, "Rule has been successfully saved!")]
        [TestCase(0, "Starts With", (byte)1, (byte)0, 0, null, 0, "Rule has been successfully saved!")]
        [TestCase(0, "Exact Match Description", (byte)2, (byte)0, 0, null, 0, "Rule has been successfully saved!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)0, 0, null, 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Any Amount
        [TestCase(1, "Starts With", (byte)1, (byte)0, 0, null, 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)0, 0, null, 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)1, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Money In
        [TestCase(1, "Starts With", (byte)1, (byte)1, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)1, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)2, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Money Out
        [TestCase(1, "Starts With", (byte)1, (byte)2, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)2, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)3, 123.01, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Exact Amount
        [TestCase(1, "Starts With", (byte)1, (byte)3, 123.02, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)3, -123.06, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)0, 0, "Transaction Contains Description Overridden", 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Description should be overridden...Any Amount (works same with Money In, Money Out & Exact Amount)
        [TestCase(1, "Starts With", (byte)1, (byte)0, 0, "Starts With Transaction Description Overridden", 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)0, 0, "Exact Match Description Overridden", 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]

        public async Task TransactionRulesAddTransactionRule_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord
            (int applyRule, string transactionTextToMatch, byte transactionTextMatchComparision, byte transactionAmountMatchComparision, double? transactionExactAmount, string? transactionDescriptionOverride, int expectedNoOfRecords, string expectedReponseMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/TransactionRule/SaveTransactionRule?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Generate Transaction records to test the rule application...
            if (!_areTransactionsDataGeneratedForAdd)
            {
                DatabaseHelperForTests.InsertTransactionRecordsForTransactionRuleTests(_userId, _transactionRuleRecord.AccountId, true, _tempConnection);
                _areTransactionsDataGeneratedForAdd = true;
            }

            // Delete any existing "transaction rule" record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_transactions_rules", "transctions_rule_id", _transactionRuleRecord.TransactionRuleId, _tempConnection);

            _transactionRuleRecord.TransactionRuleId = "";    // To insert a new record!!!
            _transactionRuleRecord.TransactionTextToMatch = transactionTextToMatch;
            _transactionRuleRecord.TransactionTextMatchComparision = transactionTextMatchComparision;
            _transactionRuleRecord.TransactionAmountMatchComparision = transactionAmountMatchComparision;
            _transactionRuleRecord.TransactionExactAmount = transactionExactAmount;
            _transactionRuleRecord.TransactionDescriptionOverride = transactionDescriptionOverride;

            request.Content = JsonContent.Create(new { data = _transactionRuleRecord, applyRule = applyRule }); // PENDING: CHECK goal_id here!!!
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            int affectedRecordsManualEntriesCount = DatabaseHelperForTests.GetNoOfRecordsCountsFromTransactionTable(true, _userId, _transactionRuleRecord.AccountId, _transactionRuleRecord.CategoryId, _transactionRuleRecord.GoalId, transactionDescriptionOverride ?? _transactionRuleRecord.TransactionTextToMatch, _tempConnection);
            int affectedRecordsAutoEntriesCount = DatabaseHelperForTests.GetNoOfRecordsCountsFromTransactionTable(false, _userId, _transactionRuleRecord.AccountId, _transactionRuleRecord.CategoryId, _transactionRuleRecord.GoalId, transactionDescriptionOverride ?? _transactionRuleRecord.TransactionTextToMatch, _tempConnection);

            // Reset category and goal id for next test record counts!!!
            DatabaseHelperForTests.ResetToDefaultCategoryAndGoalIDsInTransactionTable(_userId, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            Assert.That(root.TryGetProperty("message", out var messageElement), Is.True);
            Assert.That(messageElement.GetString(), Is.EqualTo(expectedReponseMessage));

            Assert.That(affectedRecordsManualEntriesCount, Is.EqualTo(0));  // Manual Entries are not affected by rules
            Assert.That(affectedRecordsAutoEntriesCount, Is.EqualTo(expectedNoOfRecords));

            // Validate Newly saved record...Replace the existing '_transactionRuleRecord' values
            _transactionRuleRecord = DatabaseHelperForTests.GetTransactionRuleRecordByTransactionTextToMatch(_transactionRuleRecord.TransactionTextToMatch, _userId, _tempConnection);
            Assert.That(_transactionRuleRecord.TransactionRuleId, Is.Not.Null);

        }


        [Test]
        [TestCase(0, "Contains Description", (byte)0, (byte)0, 0, null, 0, "Rule has been successfully saved!")]
        [TestCase(0, "Starts With", (byte)1, (byte)0, 0, null, 0, "Rule has been successfully saved!")]
        [TestCase(0, "Exact Match Description Overridden", (byte)2, (byte)0, 0, null, 0, "Rule has been successfully saved!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)0, 0, null, 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Any Amount
        [TestCase(1, "Starts With", (byte)1, (byte)0, 0, null, 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)0, 0, null, 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)1, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Money In
        [TestCase(1, "Starts With", (byte)1, (byte)1, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)1, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)2, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Money Out
        [TestCase(1, "Starts With", (byte)1, (byte)2, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)2, 0, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)3, 123.01, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Exact Amount
        [TestCase(1, "Starts With", (byte)1, (byte)3, 123.02, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)3, -123.06, null, 2, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Contains Description", (byte)0, (byte)0, 0, "Transaction Contains Description Overridden Again", 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]        // Any Amount
        [TestCase(1, "Starts With", (byte)1, (byte)0, 0, "Starts With Transaction Description Overridden Again", 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        [TestCase(1, "Exact Match Description", (byte)2, (byte)0, 0, "Exact Match Description Overridden Again", 4, "Rule has been successfully saved! And matching transaction records have been updated sucessfully!")]
        public async Task TransactionRulesUpdateTransactionRule_AuthenticatedRequest_ReturnsOkAndUpdatesRecord
            (int applyRule, string transactionTextToMatch, byte transactionTextMatchComparision, byte transactionAmountMatchComparision, double? transactionExactAmount, string? transactionDescriptionOverride, int expectedNoOfRecords, string expectedReponseMessage)
        {

            // ARRANGE
            // Last Rule Record will be from:
            // [TestCase(1, "Exact Match Description", (byte)2, (byte)0, 0, "Exact Match Description Overridden", 4)]

            (string accountId, string goalId) newRecordIdsForTransactionRule = new(_transactionRuleRecord.AccountId, _transactionRuleRecord.GoalId);

            if (!_areNewAccountAndGoalIdsGenerated)
            {
                newRecordIdsForTransactionRule = DatabaseHelperForTests.GenerateNewAccountAndGoalRecords(_userId, _tempConnection);
                _areNewAccountAndGoalIdsGenerated = true;

                _transactionRuleRecord.AccountId = newRecordIdsForTransactionRule.accountId;
                _transactionRuleRecord.GoalId = newRecordIdsForTransactionRule.goalId;
                _transactionRuleRecord.CategoryId = DatabaseHelperForTests.GetRecIdByName("pft_categories", "category_id", "category_name", "Transfers", _userId, _tempConnection).ToString();        // Assign new Category Id to here!!!

            }

            if (!_areTransactionsDataGeneratedForUpdate)
            {
                // Keep the existing Data and add the new data to match the exptected updated records count...existing data will not be WITH RULE UPDATE touched...
                DatabaseHelperForTests.InsertTransactionRecordsForTransactionRuleTests(_userId, _transactionRuleRecord.AccountId, false, _tempConnection);
                _areTransactionsDataGeneratedForUpdate = true;
            }

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/TransactionRule/SaveTransactionRule?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // apply new values to existing transaction rule record!

            _transactionRuleRecord.TransactionTextToMatch = transactionTextToMatch;
            _transactionRuleRecord.TransactionTextMatchComparision = transactionTextMatchComparision;
            _transactionRuleRecord.TransactionAmountMatchComparision = transactionAmountMatchComparision;
            _transactionRuleRecord.TransactionExactAmount = transactionExactAmount;
            _transactionRuleRecord.TransactionDescriptionOverride = transactionDescriptionOverride;

            TransactionRule updatedTransactionRule = new TransactionRule();
            updatedTransactionRule.TransactionRuleId = _transactionRuleRecord.TransactionRuleId;
            updatedTransactionRule.AccountId = _transactionRuleRecord.AccountId;
            updatedTransactionRule.CategoryId = _transactionRuleRecord.CategoryId;
            updatedTransactionRule.GoalId = _transactionRuleRecord.GoalId;
            updatedTransactionRule.TransactionTextToMatch = _transactionRuleRecord.TransactionTextToMatch;
            updatedTransactionRule.TransactionTextMatchComparision = _transactionRuleRecord.TransactionTextMatchComparision;
            updatedTransactionRule.TransactionAmountMatchComparision = _transactionRuleRecord.TransactionAmountMatchComparision;
            updatedTransactionRule.TransactionExactAmount = _transactionRuleRecord.TransactionExactAmount;
            updatedTransactionRule.TransactionDescriptionOverride = _transactionRuleRecord.TransactionDescriptionOverride;

            request.Content = JsonContent.Create(new { data = _transactionRuleRecord, applyRule = applyRule });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // AS RULE UPDATE CAN ALSO UPDATE TRANSACTION RECORDS
            int affectedRecordsManualEntriesCount = DatabaseHelperForTests.GetNoOfRecordsCountsFromTransactionTable(true, _userId, _transactionRuleRecord.AccountId, _transactionRuleRecord.CategoryId, _transactionRuleRecord.GoalId, transactionDescriptionOverride ?? _transactionRuleRecord.TransactionTextToMatch, _tempConnection);
            int affectedRecordsAutoEntriesCount = DatabaseHelperForTests.GetNoOfRecordsCountsFromTransactionTable(false, _userId, _transactionRuleRecord.AccountId, _transactionRuleRecord.CategoryId, _transactionRuleRecord.GoalId, transactionDescriptionOverride ?? _transactionRuleRecord.TransactionTextToMatch, _tempConnection);

            // Reset category and goal id for next test record counts!!!
            DatabaseHelperForTests.ResetToDefaultCategoryAndGoalIDsInTransactionTable(_userId, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            Assert.That(root.TryGetProperty("message", out var messageElement), Is.True);
            Assert.That(messageElement.GetString(), Is.EqualTo(expectedReponseMessage));

            Assert.That(affectedRecordsManualEntriesCount, Is.EqualTo(0));  // Manual Entries are not affected by rules
            Assert.That(affectedRecordsAutoEntriesCount, Is.EqualTo(expectedNoOfRecords));

            // Validate Newly saved record...Replace the existing '_transactionRuleRecord' values
            _transactionRuleRecord = DatabaseHelperForTests.GetTransactionRuleRecordByTransactionTextToMatch(_transactionRuleRecord.TransactionTextToMatch, _userId, _tempConnection);
            Assert.That(_transactionRuleRecord.AccountId, Is.EqualTo(updatedTransactionRule.AccountId));
            Assert.That(_transactionRuleRecord.CategoryId, Is.EqualTo(updatedTransactionRule.CategoryId));
            Assert.That(_transactionRuleRecord.GoalId, Is.EqualTo(updatedTransactionRule.GoalId));
            Assert.That(_transactionRuleRecord.TransactionTextToMatch, Is.EqualTo(updatedTransactionRule.TransactionTextToMatch));
            Assert.That(_transactionRuleRecord.TransactionTextMatchComparision, Is.EqualTo(updatedTransactionRule.TransactionTextMatchComparision));
            Assert.That(_transactionRuleRecord.TransactionAmountMatchComparision, Is.EqualTo(updatedTransactionRule.TransactionAmountMatchComparision));
            Assert.That(_transactionRuleRecord.TransactionExactAmount, Is.EqualTo(updatedTransactionRule.TransactionExactAmount));
            Assert.That(_transactionRuleRecord.TransactionDescriptionOverride, Is.EqualTo(updatedTransactionRule.TransactionDescriptionOverride));

        }


        // contains (Default) = 0, starts with = 1, exact match = 2
        // Any Amount = 0, Money In = 1, Money Out = 2, Exact Amount = 3.
        [Test]
        [TestCase(1, "A", "Contains Description", (byte)0, (byte)0, 0, null, 2)]      // Add
        [TestCase(1, "A", "Starts With", (byte)1, (byte)0, 0, null, 2)]
        [TestCase(1, "A", "Exact Match Description", (byte)2, (byte)0, 0, null, 2)]
        [TestCase(-1, "A", "Contains Description", (byte)0, (byte)0, 0, null, 2)]
        [TestCase(-1, "A", "Starts With", (byte)1, (byte)0, 0, null, 2)]
        [TestCase(-1, "A", "Exact Match Description", (byte)2, (byte)0, 0, null, 2)]
        [TestCase(1, "U", "Contains Description", (byte)0, (byte)0, 0, null, 2)]       // Update
        [TestCase(1, "U", "Starts With", (byte)1, (byte)0, 0, null, 2)]
        [TestCase(1, "U", "Exact Match Description", (byte)2, (byte)0, 0, null, 2)]
        [TestCase(-1, "U", "Contains Description", (byte)0, (byte)0, 0, null, 2)]
        [TestCase(-1, "U", "Starts With", (byte)1, (byte)0, 0, null, 2)]
        [TestCase(-1, "U", "Exact Match Description", (byte)2, (byte)0, 0, null, 2)]
        public async Task TransactionRulesSaveTransactionRule_AuthenticatedRequest_ReturnsOkWithPartialDataUpdateMessage
            (int applyCategoryType, string dataMode, string transactionTextToMatch, byte transactionTextMatchComparision, byte transactionAmountMatchComparision, double? transactionExactAmount, string? transactionDescriptionOverride, int expectedNoOfRecords)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/TransactionRule/SaveTransactionRule?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Generate Transaction records to test the rule application...
            if (!_areTransactionsDataGeneratedForAdd)
            {
                DatabaseHelperForTests.InsertTransactionRecordsForTransactionRuleTests(_userId, _transactionRuleRecord.AccountId, true, _tempConnection);
                _areTransactionsDataGeneratedForAdd = true;
            }

            if (dataMode == "A")
            {
                // Delete any existing "transaction rule" record
                DatabaseHelperForTests.DeleteRecordFromTableById("pft_transactions_rules", "transctions_rule_id", _transactionRuleRecord.TransactionRuleId, _tempConnection);

                _transactionRuleRecord.TransactionRuleId = "";    // To insert a new record!!!
            }

            if (applyCategoryType > 0)
            {
                _transactionRuleRecord.CategoryId = DatabaseHelperForTests.GetRecIdByName("pft_categories", "category_id", "category_name", "Business Income", _userId, _tempConnection).ToString();
            }
            else
            {
                _transactionRuleRecord.CategoryId = DatabaseHelperForTests.GetRecIdByName("pft_categories", "category_id", "category_name", "Groceries", _userId, _tempConnection).ToString();
            }
            _transactionRuleRecord.TransactionTextToMatch = transactionTextToMatch;
            _transactionRuleRecord.TransactionTextMatchComparision = transactionTextMatchComparision;
            _transactionRuleRecord.TransactionAmountMatchComparision = transactionAmountMatchComparision;
            _transactionRuleRecord.TransactionExactAmount = transactionExactAmount;
            _transactionRuleRecord.TransactionDescriptionOverride = transactionDescriptionOverride;

            request.Content = JsonContent.Create(new { data = _transactionRuleRecord, applyRule = 1 });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            int affectedRecordsManualEntriesCount = DatabaseHelperForTests.GetNoOfRecordsCountsFromTransactionTable(true, _userId, _transactionRuleRecord.AccountId, _transactionRuleRecord.CategoryId, _transactionRuleRecord.GoalId, transactionDescriptionOverride ?? _transactionRuleRecord.TransactionTextToMatch, _tempConnection);
            int affectedRecordsAutoEntriesCount = DatabaseHelperForTests.GetNoOfRecordsCountsFromTransactionTable(false, _userId, _transactionRuleRecord.AccountId, _transactionRuleRecord.CategoryId, _transactionRuleRecord.GoalId, transactionDescriptionOverride ?? _transactionRuleRecord.TransactionTextToMatch, _tempConnection);

            // Reset category and goal id for next test record counts!!!
            DatabaseHelperForTests.ResetToDefaultCategoryAndGoalIDsInTransactionTable(_userId, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            Assert.That(root.TryGetProperty("message", out var messageElement), Is.True);
            Assert.That(messageElement.GetString(), Is.EqualTo("Rule has been successfully saved! BUT SOME TRANSACTIONS RECORDS ARE NOT UPDATED DUE TO INVALID CATEGORY TYPE ASSIGNMENT FROM THIS RULE!"));

            Assert.That(affectedRecordsManualEntriesCount, Is.EqualTo(0));  // Manual Entries are not affected by rules
            Assert.That(affectedRecordsAutoEntriesCount, Is.EqualTo(expectedNoOfRecords));

            // Validate Newly saved record...Replace the existing '_transactionRuleRecord' values
            _transactionRuleRecord = DatabaseHelperForTests.GetTransactionRuleRecordByTransactionTextToMatch(_transactionRuleRecord.TransactionTextToMatch, _userId, _tempConnection);
            Assert.That(_transactionRuleRecord.TransactionRuleId, Is.Not.Null);

        }


        [Test]
        public async Task TransactionRulesGetTransactionRule_AuthenticatedRequest_ReturnsTransactionRuleRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/TransactionRule/GetTransactionRule?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { transactionRuleId = _transactionRuleRecord.TransactionRuleId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Deserialize the JsonElement into your TransactionRule class
            TransactionRule? transactionRuleFromRoot = root.Deserialize<TransactionRule>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Recommended if JSON keys are camelCase and C# properties are PascalCase
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            Assert.That(transactionRuleFromRoot, Is.Not.Null);
            Assert.That(transactionRuleFromRoot.AccountId, Is.EqualTo(_transactionRuleRecord.AccountId));
            Assert.That(transactionRuleFromRoot.CategoryId, Is.EqualTo(_transactionRuleRecord.CategoryId));
            Assert.That(transactionRuleFromRoot.GoalId, Is.EqualTo(_transactionRuleRecord.GoalId));
            Assert.That(transactionRuleFromRoot.TransactionTextToMatch, Is.EqualTo(_transactionRuleRecord.TransactionTextToMatch));
            Assert.That(transactionRuleFromRoot.TransactionTextMatchComparision, Is.EqualTo(_transactionRuleRecord.TransactionTextMatchComparision));
            Assert.That(transactionRuleFromRoot.TransactionAmountMatchComparision, Is.EqualTo(_transactionRuleRecord.TransactionAmountMatchComparision));
            Assert.That(transactionRuleFromRoot.TransactionExactAmount, Is.EqualTo(_transactionRuleRecord.TransactionExactAmount));
            Assert.That(transactionRuleFromRoot.TransactionDescriptionOverride, Is.EqualTo(_transactionRuleRecord.TransactionDescriptionOverride));

        }


        [Test]
        /*
         * 
         *  When an TransactionRule is deleted:
         *      
         *      pft_transactions_rules: A single record is deleted!
         * 
         */
        public async Task TransactionRulesDeleteTransactionRule_AuthenticatedRequest_DeletesTransactionRuleRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/TransactionRule/DeleteTransactionRule?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { transactionRuleId = _transactionRuleRecord.TransactionRuleId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nullTransactionRuleRecord = DatabaseHelperForTests.GetTransactionRuleRecordByTransactionTextToMatch(_transactionRuleRecord.TransactionTextToMatch, _userId, _tempConnection);


            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nullTransactionRuleRecord, Is.Null);

            // Clean up the data:
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("transactionrules", _tempConnection);

        }

    }

}
