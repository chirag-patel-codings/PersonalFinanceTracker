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
    public class GoalControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private Goal? _goalRecord = null;
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

            var goalRec = DatabaseHelperForTests.GetSingleRecordFromGoalsTable(_userId, _tempConnection);
            if (goalRec != null) { 

                _goalRecord = goalRec;

            }
            else
            {

                _goalRecord = new Goal
                {

                    UserId = ulong.Parse(_userId),
                    GoalId = "7777777777777777777",      // MAX BIGINT: 9223372036854775807
                    GoalName = "Test Goal Name",
                    GoalDescription = "Test Goal Description",
                    GoalType = 1,        // Savings = 1, Reduce Debt = 2, Investing = 3, Tax Planning = 4, Other = 5
                    GoalDetails =   new List<GoalDetails>
                    { new GoalDetails
                            {
                                GoalID = 7777777777777777777,
                                GoalInterval = 1,
                                GoalAmount = 50,
                                GoalPeriodStartDate = new DateOnly(DateTime.Now.Year, 1, 1),
                                GoalPeriodEndDate = new DateOnly(DateTime.Now.Year, 12, 31)
                            },
                        new GoalDetails
                            {
                                GoalID = 7777777777777777777,
                                GoalInterval = 2,
                                GoalAmount = 100,
                                GoalPeriodStartDate = new DateOnly(DateTime.Now.Year, 1, 1),
                                GoalPeriodEndDate = new DateOnly(DateTime.Now.Year, 12, 31)
                            },
                    }

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
        [TestCase("/goals")]
        public async Task GoalsAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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
        [TestCase("/goals")]
        [TestCase("/Goal/GetGoal")]
        [TestCase("/Goal/SaveGoal")]
        [TestCase("/Goal/DeleteGoal")]
        public async Task GoalsAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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

                case "/goals":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    break;

                case "/Goal/GetGoal":
                case "/Goal/DeleteGoal":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { goalId = _goalRecord.GoalId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { goalId = _goalRecord.GoalId });
                    break;

                case "/Goal/SaveGoal":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _goalRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _goalRecord });
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
        public async Task GoalsIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/goals");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>Goals "), Is.True);   // Contents of goal view

        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task GoalsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload
            (bool isFirstRequest)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/goals?connString=pft_con_str_testenv");
            
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
             * or worry about deserialization exceptions if a property happens to be null (like goalTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(root.TryGetProperty("goals", out var goalsElement), Is.True);
            Assert.That(root.TryGetProperty("pagination", out var paginationElement), Is.True);
            Assert.That(root.TryGetProperty("goalTypes", out var goalTypesElement), Is.True);
            Assert.That(root.TryGetProperty("goalIntervalTypes", out var goalIntervalTypesElement), Is.True);

            // Assert on specific values inside pagination or goals
            var totalRecords = int.Parse(paginationElement.GetProperty("totalNumberOfRecords").GetString());

            Assert.That(totalRecords, Is.GreaterThanOrEqualTo(0));  // there is a possibility that goals has no data...

            // Check if goals & goal types are arrays and has data (if expected)
            Assert.That(goalsElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(goalsElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            if (isFirstRequest)
            {
                Assert.That(goalTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(goalTypesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(goalIntervalTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(goalIntervalTypesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            }
            else
            {
                Assert.That(goalTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(goalIntervalTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Null));
            }

        }


        /*
         *  GoalDetails:
         *  For the below test, goal details does not need to be provided as
         *  the model will fail for any incorrect value of it's member.
         */
        
        [Test]
        // GoalType
        [TestCase("", null, null, "From_Test_Goal_Name", "From_Test_Goal_Description", null, 400, "GoalType", "Please provide the type of goal!")]
        [TestCase(null, null, null, "From_Test_Goal_Name", "From_Test_Goal_Description", null, 400, "GoalType", "Please provide the type of goal!")]

        // GoalName
        [TestCase("", null, 1, "", "From_Test_Goal_Description", null, 400, "GoalName", "Goal name is required!")]
        [TestCase(null, null, 1, "", "From_Test_Goal_Description", null, 400, "GoalName", "Goal name is required!")]
        [TestCase("", null, 1, "     ", "From_Test_Goal_Description", null, 400, "GoalName", "Goal name is required!")]
        [TestCase(null, null, 1, "     ", "From_Test_Goal_Description", null, 400, "GoalName", "Goal name is required!")]
        [TestCase("", null, 1, "This Goal Name is more than 100 characters long to test the max length for the goal name value.......", "From_Test_Goal_Description", null, 400, "GoalName", "Goal name should not be more than 100 characters long!")]
        [TestCase(null, null, 1, "This Goal Name is more than 100 characters long to test the max length for the goal name value.......", "From_Test_Goal_Description", null, 400, "GoalName", "Goal name should not be more than 100 characters long!")]

        // GoalDescription
        [TestCase("", null, 1, "From_Test_Goal_Name", "This Goal Description is more than 255 characters long to test the max length for the goal description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", null, 400, "GoalDescription", "Goal description should not be more than 255 characters long!")]
        [TestCase(null, null, 1, "From_Test_Goal_Name", "This Goal Description is more than 255 characters long to test the max length for the goal description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", null, 400, "GoalDescription", "Goal description should not be more than 255 characters long!")]
        public async Task GoalsSaveGoal_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? goalId, ulong? userId, sbyte? goalType, string goalName, string? goalDescription, IEnumerable<GoalDetails>? goalDetails, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Goal/SaveGoal?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Goal invalidGoal = new Goal
            {
                GoalId = goalId,
                UserId = userId,
                GoalType = goalType,
                GoalName = goalName,
                GoalDescription = goalDescription,
                GoalDetails = goalDetails
            };

            request.Content = JsonContent.Create(invalidGoal);
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
            invalidGoal = DatabaseHelperForTests.GetGoalRecordByName(invalidGoal.GoalName, _userId, _tempConnection);
            Assert.That(invalidGoal, Is.Null);

        }


        [Test]
        public async Task GoalsAddGoal_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Goal/SaveGoal?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Delete any existing record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_goals", "goal_id", _goalRecord.GoalId, _tempConnection, "pft_goal_details", "goal_id");

            _goalRecord.GoalId = "";    // To insert a new record!!!
            request.Content = JsonContent.Create( _goalRecord );
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

            // Validate Newly saved record...Replace the existing '_goalRecord' values
            _goalRecord = DatabaseHelperForTests.GetGoalRecordByName(_goalRecord.GoalName, _userId, _tempConnection);
            _goalRecord.GoalDetails = DatabaseHelperForTests.GetGoalDetailsByGoalName(_goalRecord.GoalName, _userId, _tempConnection);

            Assert.That(_goalRecord.GoalId, Is.Not.Null);
            Assert.That(_goalRecord.GoalDetails.Count, Is.GreaterThanOrEqualTo(0));

        }


        [Test]
        public async Task GoalsUpdateGoal_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Goal/SaveGoal?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Goal updatedGoal = new Goal();
            updatedGoal.GoalId = _goalRecord.GoalId;
            updatedGoal.GoalName = _goalRecord.GoalName + " - Updated";
            updatedGoal.GoalDescription = _goalRecord.GoalDescription + " - Updated";
            updatedGoal.GoalType = 2;
            updatedGoal.GoalDetails = _goalRecord.GoalDetails;

            foreach (var item in updatedGoal.GoalDetails)
            {

                item.GoalPeriodStartDate = item.GoalPeriodStartDate.AddMonths(3);
                item.GoalPeriodEndDate = item.GoalPeriodEndDate.AddMonths(-3);
                item.GoalInterval = (byte)(item.GoalInterval + (byte)2);
                item.GoalAmount = item.GoalAmount + 1;

            }

            request.Content = JsonContent.Create(updatedGoal);
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

            // Validate Newly saved record...Replace the existing '_goalRecord' values
            _goalRecord = DatabaseHelperForTests.GetGoalRecordByName(updatedGoal.GoalName, _userId, _tempConnection);
            _goalRecord.GoalDetails = DatabaseHelperForTests.GetGoalDetailsByGoalName(updatedGoal.GoalName, _userId, _tempConnection);

            Assert.That(_goalRecord.GoalName, Is.EqualTo(updatedGoal.GoalName));
            Assert.That(_goalRecord.GoalDescription, Is.EqualTo(updatedGoal.GoalDescription));
            Assert.That(_goalRecord.GoalType, Is.EqualTo(updatedGoal.GoalType));

            foreach (var item in _goalRecord.GoalDetails)
            {
                var updatedGoalItem = updatedGoal.GoalDetails.Where(u => u.GoalID == item.GoalID && u.GoalInterval == item.GoalInterval && u.GoalAmount == item.GoalAmount && u.GoalPeriodStartDate == item.GoalPeriodStartDate && u.GoalPeriodEndDate == item.GoalPeriodEndDate);
                Assert.That(updatedGoalItem.Count, Is.GreaterThanOrEqualTo(1));
            }

        }

        [Test]
        public async Task GoalsGetGoal_AuthenticatedRequest_ReturnsGoalRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Goal/GetGoal?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { goalId = _goalRecord.GoalId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Deserialize the JsonElement into your Goal class
            Goal? goalFromRoot = root.Deserialize<Goal>(new JsonSerializerOptions
                                                {
                                                    PropertyNameCaseInsensitive = true, // Recommended if JSON keys are camelCase and C# properties are PascalCase
                                                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                                                });

            Assert.That(goalFromRoot, Is.Not.Null);
            Assert.That(goalFromRoot.GoalName, Is.EqualTo(_goalRecord.GoalName));
            Assert.That(goalFromRoot.GoalDescription, Is.EqualTo(_goalRecord.GoalDescription));
            Assert.That(goalFromRoot.GoalType, Is.EqualTo(_goalRecord.GoalType));

            foreach (var item in goalFromRoot.GoalDetails)
            {
                // g.GoalID == item.GoalID to be excluded as goaldetails records does not contains the goal_id but they are retrieved using goal_id
                var goalFromRootItem = _goalRecord.GoalDetails.Where(g => g.GoalInterval == item.GoalInterval && g.GoalAmount == item.GoalAmount && g.GoalPeriodStartDate == item.GoalPeriodStartDate && g.GoalPeriodEndDate == item.GoalPeriodEndDate);
                Assert.That(goalFromRootItem.Count, Is.GreaterThanOrEqualTo(1));
            }

        }

        [Test]
        /*
         *  
         *  When a goal is deleted:
         *  pft_transactions: goal_id = null
         *  pft_recurring_schedules: goal_id = null
         *  pft_transactions_rules: goal_id = null
         *  pft_goal_details: all the related records are deleted
         *  pft_goals: the record with specific goal_id is deleted.
         *  
         */
        public async Task GoalsDeleteGoal_AuthenticatedRequest_DeletesGoalRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Goal/DeleteGoal?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { goalId = _goalRecord.GoalId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // Cleanup Dummy Data in Related Tables:
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("goals", _tempConnection);

            // Generate dummy data in related tables...pft_transactions, pft_recurring_schedules
            DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("goals", _userId, _tempConnection, _goalRecord.GoalId, null);

            // ACT
            int transactionsRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "goal_id", _goalRecord.GoalId, _tempConnection);
            int recurringSchedulesRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "goal_id", _goalRecord.GoalId, _tempConnection);
            int transactionsRepeatRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId,"pft_trasactions_repeats", "goal_id", _goalRecord.GoalId, _tempConnection);
            int transactionsRuleRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "goal_id", _goalRecord.GoalId, _tempConnection);
            int goalDetailsRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_goal_details", "goal_id", _goalRecord.GoalId, _tempConnection);
            int goalRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_goals", "goal_id", _goalRecord.GoalId, _tempConnection);


            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nullGoalDetails = DatabaseHelperForTests.GetGoalDetailsByGoalName(_goalRecord.GoalName, _userId, _tempConnection);
            var nullGoalRecord = DatabaseHelperForTests.GetGoalRecordByName(_goalRecord.GoalName, _userId, _tempConnection);

            int transactionsRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "goal_id", _goalRecord.GoalId, _tempConnection);
            int recurringSchedulesRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "goal_id", _goalRecord.GoalId, _tempConnection);
            int transactionsRepeatRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_trasactions_repeats", "goal_id", _goalRecord.GoalId, _tempConnection);
            int transactionsRuleRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "goal_id", _goalRecord.GoalId, _tempConnection);
            int goalDetailsRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_goal_details", "goal_id", _goalRecord.GoalId, _tempConnection);
            int goalRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_goals", "goal_id", _goalRecord.GoalId, _tempConnection);


            // ASSERT
            Assert.That(transactionsRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(recurringSchedulesRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(transactionsRepeatRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(transactionsRuleRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(goalDetailsRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(goalRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));

            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nullGoalDetails.Count, Is.EqualTo(0));
            Assert.That(nullGoalRecord, Is.Null);

            Assert.That(transactionsRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(recurringSchedulesRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(transactionsRepeatRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(transactionsRuleRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(goalDetailsRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(goalRecordsCountsAfterDelete, Is.EqualTo(0));

        }

    }

}




