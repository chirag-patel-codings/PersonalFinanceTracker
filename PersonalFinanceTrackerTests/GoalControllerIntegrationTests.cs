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
    public class GoalControllerIntegrationTests
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private Goal? _goalRecord = null;
        IDbConnection _tempConnection;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // Query the test database for the user ID using your existing helper
            // (You can instantiate a standalone connection factory just for this lookup)

            // Load the test configuration
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(TestContext.CurrentContext.TestDirectory)
                .AddJsonFile("appsettings.test.json", optional: false)
                .Build();

            var tempConnectionFactory = new DbConnectionFactory(configuration);
            _tempConnection = tempConnectionFactory.GetDBConnection("pft_con_str_testenv");
            
            _userId = DatabaseHelperForTests.GetUserIdByUserNameAndEmail(
                "PFT_Test_User_123", "test@test.tt", _tempConnection).ToString();

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
        public async Task GoalsAllActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest()
        {

            // ARRANGE - Build an explicit request message to attach the test header
            var requestIndexGet = new HttpRequestMessage(HttpMethod.Get, "/goals");

            var requestIndexPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/goals");
            var requestIndexPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/goals");

            var requestGetGoalPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Goal/GetGoal");
            var requestGetGoalPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Goal/GetGoal");

            var requestSaveGoalPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Goal/SaveGoal");
            var requestSaveGoalPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Goal/SaveGoal");

            var requestDeleteGoalPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Goal/DeleteGoal");
            var requestDeleteGoalPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Goal/DeleteGoal");

            // Configure Header to fail authentication for this request by TestAuthHandler 
            // requestIndexGet
            requestIndexGet.Headers.Add("X-Test-Unauthorized", "true");

            // requestIndexPostInValidAuthorize
            requestIndexPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestIndexPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });

            // InValidAntiforgeryToken
            var (token, cookie) = await GetAntiforgeryTokensAsync(false);

            requestIndexPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);   // 'cookie' eliminated as null;
            requestIndexPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });

            // requestGetGoalPostInValidAuthorize
            requestGetGoalPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestGetGoalPostInValidAuthorize.Content = JsonContent.Create(new { goalId = _goalRecord == null ? "7777777777777777777" : _goalRecord.GoalId });

            // requestGetGoalPostInValidAntiforgeryToken
            requestGetGoalPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestGetGoalPostInValidAntiforgeryToken.Content = JsonContent.Create(new { goalId = _goalRecord == null ? "7777777777777777777" : _goalRecord.GoalId });

            // requestSaveGoalPostInValidAuthorize
            requestSaveGoalPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestSaveGoalPostInValidAuthorize.Content = JsonContent.Create(new { _goalRecord });

            // requestSaveGoalPostInValidAntiforgeryToken
            requestSaveGoalPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestSaveGoalPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _goalRecord });

            // requestDeleteGoalPostInValidAuthorize
            requestDeleteGoalPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestDeleteGoalPostInValidAuthorize.Content = JsonContent.Create(new { goalId = _goalRecord == null ? "7777777777777777777" : _goalRecord.GoalId });

            // requestDeleteGoalPostInValidAntiforgeryToken
            requestDeleteGoalPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestDeleteGoalPostInValidAntiforgeryToken.Content = JsonContent.Create(new { goalId = _goalRecord == null ? "7777777777777777777" : _goalRecord.GoalId });


            // ACT - Send the GET request through the custom client
            var responseIndexGet = await _client.SendAsync(requestIndexGet);

            var responseIndexPostInValidAuthorize = await _client.SendAsync(requestIndexPostInValidAuthorize);
            var responseIndexPostInValidAntiforgeryToken = await _client.SendAsync(requestIndexPostInValidAntiforgeryToken);

            var responseGetGoalPostInValidAuthorize = await _client.SendAsync(requestGetGoalPostInValidAuthorize);
            var responseGetGoalPostInValidAntiforgeryToken = await _client.SendAsync(requestGetGoalPostInValidAntiforgeryToken);

            var responseSaveGoalPostInValidAuthorize = await _client.SendAsync(requestSaveGoalPostInValidAuthorize);
            var responseSaveGoalPostInValidAntiforgeryToken = await _client.SendAsync(requestSaveGoalPostInValidAntiforgeryToken);

            var responseDeleteGoalPostInValidAuthorize = await _client.SendAsync(requestDeleteGoalPostInValidAuthorize);
            var responseDeleteGoalPostInValidAntiforgeryToken = await _client.SendAsync(requestDeleteGoalPostInValidAntiforgeryToken);


            // ASSERT - Verify the server successfully blocked the request with 401 Unauthorized
            Assert.That(responseIndexGet.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            Assert.That(responseIndexPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseIndexPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseGetGoalPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseGetGoalPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseSaveGoalPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseSaveGoalPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseDeleteGoalPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseDeleteGoalPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

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
        public async Task GoalsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/goals?connString=pft_con_str_testenv");
            var consecutiveRequests = new HttpRequestMessage(HttpMethod.Post, "/goals?connString=pft_con_str_testenv");


            firstRequest.Headers.Add("Request-Verification-Token", token);
            firstRequest.Headers.Add("Cookie", cookie);
            firstRequest.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });

            consecutiveRequests.Headers.Add("Request-Verification-Token", token);
            consecutiveRequests.Headers.Add("Cookie", cookie);
            consecutiveRequests.Content = JsonContent.Create(new { firstRequest = "false", pagination = new { } });

            // ACT
            var responseFirstRequest = await _client.SendAsync(firstRequest);
            var jsonResponseFirstRequest = await responseFirstRequest.Content.ReadAsStringAsync();

            var responseConsecutiveRequests = await _client.SendAsync(consecutiveRequests);
            var jsonResponseConsecutiveRequests = await responseConsecutiveRequests.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(responseFirstRequest.IsSuccessStatusCode, Is.True);
            Assert.That(responseConsecutiveRequests.IsSuccessStatusCode, Is.True);

            /*
             * Parse the JSON tree:
             * Using JsonDocument allows you to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             * or worry about deserialization exceptions if a property happens to be null (like goalTypes when firstRequest is false).
             */
            using var docFirstRequest = JsonDocument.Parse(jsonResponseFirstRequest);
            var rootFirstRequest = docFirstRequest.RootElement;

            using var docConsecutiveRequests = JsonDocument.Parse(jsonResponseConsecutiveRequests);
            var rootConsecutiveRequests = docConsecutiveRequests.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(rootFirstRequest.TryGetProperty("goals", out var goalsElementFirstRequest), Is.True);
            Assert.That(rootFirstRequest.TryGetProperty("pagination", out var paginationElementFirstRequest), Is.True);
            Assert.That(rootFirstRequest.TryGetProperty("goalTypes", out var goalTypesElementFirstRequest), Is.True);
            Assert.That(rootFirstRequest.TryGetProperty("goalIntervalTypes", out var goalIntervalTypesElementFirstRequest), Is.True);

            // Verify all three root properties exist in the consecutive request's response
            Assert.That(rootConsecutiveRequests.TryGetProperty("goals", out var goalsElementConsecutiveRequests), Is.True);
            Assert.That(rootConsecutiveRequests.TryGetProperty("pagination", out var paginationElementConsecutiveRequests), Is.True);
            Assert.That(rootConsecutiveRequests.TryGetProperty("goalTypes", out var goalTypesElementConsecutiveRequest), Is.True);
            Assert.That(rootConsecutiveRequests.TryGetProperty("goalIntervalTypes", out var goalIntervalTypesElementConsecutiveRequest), Is.True);

            // Assert on specific values inside pagination or goals
            var totalRecordsFirstRequest = int.Parse(paginationElementFirstRequest.GetProperty("totalNumberOfRecords").GetString());
            var totalRecordsConsecutiveRequest = int.Parse(paginationElementConsecutiveRequests.GetProperty("totalNumberOfRecords").GetString());


            Assert.That(totalRecordsFirstRequest, Is.GreaterThanOrEqualTo(0));  // there is a possibility that goals has no data...
            Assert.That(totalRecordsConsecutiveRequest, Is.GreaterThanOrEqualTo(0));  // there is a possibility that goals has no data...


            // Check if goals & goal types are arrays and has data (if expected)
            Assert.That(goalsElementFirstRequest.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(goalsElementFirstRequest.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            Assert.That(goalTypesElementFirstRequest.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(goalTypesElementFirstRequest.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            Assert.That(goalIntervalTypesElementFirstRequest.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(goalIntervalTypesElementFirstRequest.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            // Check if goals is an array and has data (if expected) & goal types is null
            Assert.That(goalsElementConsecutiveRequests.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(goalsElementConsecutiveRequests.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            Assert.That(goalTypesElementConsecutiveRequest.ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(goalIntervalTypesElementConsecutiveRequest.ValueKind, Is.EqualTo(JsonValueKind.Null));

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
            var (token, cookie) = await GetAntiforgeryTokensAsync();

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

            // Validate Newly saved record...Replace the existing '_goalRecord' values
            invalidGoal = DatabaseHelperForTests.GetGoalRecordByName(invalidGoal.GoalName, _userId, _tempConnection);
            Assert.That(invalidGoal, Is.Null);

        }

        [Test]
        public async Task GoalsAddGoal_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

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
            var (token, cookie) = await GetAntiforgeryTokensAsync();

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
            var (token, cookie) = await GetAntiforgeryTokensAsync();

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
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Goal/DeleteGoal?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { goalId = _goalRecord.GoalId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

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

            // Cleanup Dummy Data in Related Tables:
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("goals", _tempConnection);

        }

        // For Server to authenticates. (Part of client request)
        // Before a real browser submits a form or a secure POST request with anti-forgery protection, it needs to hit the server to get a valid validation token and cookie.
        // This helper method simulates that exact browser behavior programmatically.
        protected async Task<(string? token, string? cookieHeader)> GetAntiforgeryTokensAsync(bool generateAntiforgeryToken = true)
        {
            using var scope = _factory.Services.CreateScope();
            var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = scope.ServiceProvider;

            if (generateAntiforgeryToken)
            {
                // Attach the authenticated user so the token binds to this user ID
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _userId),
                    new Claim(ClaimTypes.Name, "PFT_Test_User_123"),
                    new Claim("CurrencyCode", "USD"),
                    new Claim("CurrencySymbol", "$")
                };
                var identity = new ClaimsIdentity(claims, "TestScheme");
                httpContext.User = new ClaimsPrincipal(identity);

                // Generate the tokens
                var tokens = antiforgery.GetAndStoreTokens(httpContext);

                // Use the exact static cookie name we configured in PostConfigure
                string cookieHeader = $".AspNetCore.Antiforgery.TestCookie={tokens.CookieToken}";

                return (tokens.RequestToken, cookieHeader);

            }
            else
            {
                return (null, null);    // Invalid Request
            }

        }

    }

}




