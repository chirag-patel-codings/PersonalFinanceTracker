using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components;
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
    public class TagControllerIntegrationTests
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private Tag? _tagRecord = null;
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

            var tagRec = DatabaseHelperForTests.GetSingleRecordFromTagsTable(_userId, _tempConnection);
            if (tagRec != null) { 

                _tagRecord = tagRec;

            }
            else
            {

                _tagRecord = new Tag
                                {

                                    UserId = ulong.Parse(_userId),
                                    TagId = "8888888888888888888",      // MAX BIGINT: 9223372036854775807
                                    TagName = "Test Tag Name",
                                    TagDescription = "Test Tag Description",
                                    TagColor = "#ffffff",
                                    IsTagActive = (sbyte)1

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
        public async Task TagsAllActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest()
        {

            // ARRANGE - Build an explicit request message to attach the test header
            var requestIndexGet = new HttpRequestMessage(HttpMethod.Get, "/categories/tags");

            var requestIndexPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/categories/tags");
            var requestIndexPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/categories/tags");

            var requestGetTagPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Tag/GetTag");
            var requestGetTagPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Tag/GetTag");

            var requestSaveTagPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Tag/SaveTag");
            var requestSaveTagPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Tag/SaveTag");

            var requestDeleteTagPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Tag/DeleteTag");
            var requestDeleteTagPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Tag/DeleteTag");

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

            // requestGetTagPostInValidAuthorize
            requestGetTagPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestGetTagPostInValidAuthorize.Content = JsonContent.Create(new { tagId = _tagRecord == null ? "8888888888888888888" : _tagRecord.TagId });

            // requestGetTagPostInValidAntiforgeryToken
            requestGetTagPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestGetTagPostInValidAntiforgeryToken.Content = JsonContent.Create(new { tagId = _tagRecord == null ? "8888888888888888888" : _tagRecord.TagId });

            // requestSaveTagPostInValidAuthorize
            requestSaveTagPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestSaveTagPostInValidAuthorize.Content = JsonContent.Create(new { _tagRecord });

            // requestSaveTagPostInValidAntiforgeryToken
            requestSaveTagPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestSaveTagPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _tagRecord });

            // requestDeleteTagPostInValidAuthorize
            requestDeleteTagPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestDeleteTagPostInValidAuthorize.Content = JsonContent.Create(new { tagId = _tagRecord == null ? "8888888888888888888" : _tagRecord.TagId });

            // requestDeleteTagPostInValidAntiforgeryToken
            requestDeleteTagPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestDeleteTagPostInValidAntiforgeryToken.Content = JsonContent.Create(new { tagId = _tagRecord == null ? "8888888888888888888" : _tagRecord.TagId });


            // ACT - Send the GET request through the custom client
            var responseIndexGet = await _client.SendAsync(requestIndexGet);

            var responseIndexPostInValidAuthorize = await _client.SendAsync(requestIndexPostInValidAuthorize);
            var responseIndexPostInValidAntiforgeryToken = await _client.SendAsync(requestIndexPostInValidAntiforgeryToken);

            var responseGetTagPostInValidAuthorize = await _client.SendAsync(requestGetTagPostInValidAuthorize);
            var responseGetTagPostInValidAntiforgeryToken = await _client.SendAsync(requestGetTagPostInValidAntiforgeryToken);

            var responseSaveTagPostInValidAuthorize = await _client.SendAsync(requestSaveTagPostInValidAuthorize);
            var responseSaveTagPostInValidAntiforgeryToken = await _client.SendAsync(requestSaveTagPostInValidAntiforgeryToken);

            var responseDeleteTagPostInValidAuthorize = await _client.SendAsync(requestDeleteTagPostInValidAuthorize);
            var responseDeleteTagPostInValidAntiforgeryToken = await _client.SendAsync(requestDeleteTagPostInValidAntiforgeryToken);


            // ASSERT - Verify the server successfully blocked the request with 401 Unauthorized
            Assert.That(responseIndexGet.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            Assert.That(responseIndexPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseIndexPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseGetTagPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseGetTagPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseSaveTagPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseSaveTagPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseDeleteTagPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseDeleteTagPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        }


        [Test]
        public async Task TagsIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/categories/tags");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>Tags "), Is.True);   // Contents of tag view

        }


        [Test]
        public async Task TagsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/categories/tags?connString=pft_con_str_testenv");
            var consecutiveRequests = new HttpRequestMessage(HttpMethod.Post, "/categories/tags?connString=pft_con_str_testenv");


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
             * or worry about deserialization exceptions if a property happens to be null (like tagTypes when firstRequest is false).
             */
            using var docFirstRequest = JsonDocument.Parse(jsonResponseFirstRequest);
            var rootFirstRequest = docFirstRequest.RootElement;

            using var docConsecutiveRequests = JsonDocument.Parse(jsonResponseConsecutiveRequests);
            var rootConsecutiveRequests = docConsecutiveRequests.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(rootFirstRequest.TryGetProperty("tags", out var tagsElementFirstRequest), Is.True);
            Assert.That(rootFirstRequest.TryGetProperty("pagination", out var paginationElementFirstRequest), Is.True);
            
            // Verify all three root properties exist in the consecutive request's response
            Assert.That(rootConsecutiveRequests.TryGetProperty("tags", out var tagsElementConsecutiveRequests), Is.True);
            Assert.That(rootConsecutiveRequests.TryGetProperty("pagination", out var paginationElementConsecutiveRequests), Is.True);
            
            // Assert on specific values inside pagination or tags
            var totalRecordsFirstRequest = int.Parse(paginationElementFirstRequest.GetProperty("totalNumberOfRecords").GetString());
            var totalRecordsConsecutiveRequest = int.Parse(paginationElementConsecutiveRequests.GetProperty("totalNumberOfRecords").GetString());


            Assert.That(totalRecordsFirstRequest, Is.GreaterThanOrEqualTo(0));  // there is a possibility that tags has no data...
            Assert.That(totalRecordsConsecutiveRequest, Is.GreaterThanOrEqualTo(0));  // there is a possibility that tags has no data...


            // Check if tags & tag types are arrays and has data (if expected)
            Assert.That(tagsElementFirstRequest.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(tagsElementFirstRequest.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            
            // Check if tags is an array and has data (if expected) & tag types is null
            Assert.That(tagsElementConsecutiveRequests.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(tagsElementConsecutiveRequests.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

        }


        [Test]
        
        // TagName: "" / null;
        [TestCase("", null, null, "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "     ", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "     ", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase(null, null, null, "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase(null, null, "", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "This Tag Name is more than 100 characters long to test the max length for the tag name value.........", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name should not be more than 100 characters long!")]
        [TestCase(null, null, "This Tag Name is more than 100 characters long to test the max length for the tag name value.........", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name should not be more than 100 characters long!")]
        
        // TagDescription: "" / null;
        [TestCase("", null, "From_Test_Tag_Name", "This Tag Description is more than 255 characters long to test the max length for the tag description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", "#ffffff", (sbyte)1, 400, "TagDescription", "Tag description should not be more than 255 characters long!")]
        [TestCase(null, null, "From_Test_Tag_Name", "This Tag Description is more than 255 characters long to test the max length for the tag description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", "#ffffff", (sbyte)1, 400, "TagDescription", "Tag description should not be more than 255 characters long!")]

        [TestCase("", null, "From_Test_Tag_Name", "From_Test_Tag_Description", "#fffffffffffffffffffffffffffffffffffffffffffffffffff", (sbyte)1, 400, "TagColor", "Tag color should not be more than 50 characters long!")]
        [TestCase("", null, "From_Test_Tag_Name", "From_Test_Tag_Institution_Name", "#fffffffffffffffffffffffffffffffffffffffffffffffffff", (sbyte)1, 400, "TagColor", "Tag color should not be more than 50 characters long!")]
        
        public async Task TagsSaveTag_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? tagId, ulong? userId, string? tagName, string? tagDescription, string? tagColor, sbyte? isTagActive, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Tag/SaveTag?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Tag invalidTag = new Tag
            {
                TagId = tagId,
                UserId = userId,
                TagName = tagName,
                TagDescription = tagDescription,
                TagColor = tagColor,
                IsTagActive = isTagActive
            };

            request.Content = JsonContent.Create(invalidTag);
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

            // Validate Newly saved record...Replace the existing '_tagRecord' values
            invalidTag = DatabaseHelperForTests.GetTagRecordByName(invalidTag.TagName, _userId, _tempConnection);
            Assert.That(invalidTag, Is.Null);

        }

        [Test]
        public async Task TagsAddTag_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Tag/SaveTag?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Delete any existing record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_tags", "tag_id", _tagRecord.TagId, _tempConnection);

            _tagRecord.TagId = "";    // To insert a new record!!!
            request.Content = JsonContent.Create( _tagRecord );
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

            // Validate Newly saved record...Replace the existing '_tagRecord' values
            _tagRecord = DatabaseHelperForTests.GetTagRecordByName(_tagRecord.TagName, _userId, _tempConnection);
            Assert.That(_tagRecord.TagId, Is.Not.Null);

        }


        [Test]
        public async Task TagsUpdateTag_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Tag/SaveTag?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Tag updatedTag = new Tag();
            updatedTag.TagId = _tagRecord.TagId;
            updatedTag.TagName = _tagRecord.TagName + " - Updated";
            updatedTag.TagDescription = _tagRecord.TagDescription + " - Updated";
            updatedTag.TagColor = "#000000";
            updatedTag.IsTagActive = (sbyte)0;

            request.Content = JsonContent.Create(updatedTag);
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

            // Validate Newly saved record...Replace the existing '_tagRecord' values
            _tagRecord = DatabaseHelperForTests.GetTagRecordByName(updatedTag.TagName, _userId, _tempConnection);
            Assert.That(_tagRecord.TagName, Is.EqualTo(updatedTag.TagName));
            Assert.That(_tagRecord.TagDescription, Is.EqualTo(updatedTag.TagDescription));
            Assert.That(_tagRecord.TagColor, Is.EqualTo(updatedTag.TagColor));
            Assert.That(_tagRecord.IsTagActive, Is.EqualTo(updatedTag.IsTagActive));


        }

        [Test]
        public async Task TagsGetTag_AuthenticatedRequest_ReturnsTagRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Tag/GetTag?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { tagId = _tagRecord.TagId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Deserialize the JsonElement into your Tag class
            Tag? tagFromRoot = root.Deserialize<Tag>(new JsonSerializerOptions
                                                {
                                                    PropertyNameCaseInsensitive = true, // Recommended if JSON keys are camelCase and C# properties are PascalCase
                                                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                                                });

            Assert.That(tagFromRoot, Is.Not.Null);
            Assert.That(tagFromRoot.TagName, Is.EqualTo(_tagRecord.TagName));
            Assert.That(tagFromRoot.TagDescription, Is.EqualTo(_tagRecord.TagDescription));
            Assert.That(tagFromRoot.TagColor, Is.EqualTo(_tagRecord.TagColor));
            Assert.That(tagFromRoot.IsTagActive, Is.EqualTo(_tagRecord.IsTagActive));

        }

        [Test]
        /*
         *  
         *  When a tag is deleted:
         *  pft_transactions: tag_id = null
         *  pft_recurring_schedules: tag_id = null
         *  
         */
        public async Task TagsDeleteTag_AuthenticatedRequest_DeletesTagRecord()
        {

            // ARRANGE
            // Prepare request
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Tag/DeleteTag?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { tagId = _tagRecord.TagId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // Generate dummy data in related tables...pft_transactions, pft_recurring_schedules
            DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("tags", _userId, _tempConnection, _tagRecord.TagId, null);

            // ACT
            int transactionsRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "tag_id", _tagRecord.TagId, _tempConnection);
            int recurringSchedulesRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "tag_id", _tagRecord.TagId, _tempConnection);
            int transactionsRepeatRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_trasactions_repeats", "tag_id", _tagRecord.TagId, _tempConnection);

            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nullTagRecord = DatabaseHelperForTests.GetTagRecordByName(_tagRecord.TagName, _userId, _tempConnection);

            int transactionsRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "tag_id", _tagRecord.TagId, _tempConnection);
            int recurringSchedulesRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "tag_id", _tagRecord.TagId, _tempConnection);
            int transactionsRepeatRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_trasactions_repeats", "tag_id", _tagRecord.TagId, _tempConnection);


            // ASSERT
            Assert.That(transactionsRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(recurringSchedulesRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(transactionsRepeatRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));

            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nullTagRecord, Is.Null);

            Assert.That(transactionsRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(recurringSchedulesRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(transactionsRepeatRecordsCountsAfterDelete, Is.EqualTo(0));


            // Cleanup Dummy Data in Related Tables:
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("tags", _tempConnection);

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




