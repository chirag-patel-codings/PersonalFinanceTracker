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
    public class TagControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private Tag? _tagRecord = null;
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
        [TestCase("/categories/tags")]
        public async Task TagsAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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
        [TestCase("/categories/tags")]
        [TestCase("/Tag/GetTag")]
        [TestCase("/Tag/SaveTag")]
        [TestCase("/Tag/DeleteTag")]
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

                case "/categories/tags":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    break;

                case "/Tag/GetTag":
                case "/Tag/DeleteTag":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { tagId = _tagRecord.TagId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { tagId = _tagRecord.TagId });
                    break;

                case "/Tag/SaveTag":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _tagRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _tagRecord });
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
        [TestCase(true)]
        [TestCase(false)]
        public async Task TagsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload
            (bool isFirstRequest)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/categories/tags?connString=pft_con_str_testenv");
            
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
             * or worry about deserialization exceptions if a property happens to be null (like tagTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(root.TryGetProperty("tags", out var tagsElement), Is.True);
            Assert.That(root.TryGetProperty("pagination", out var paginationElement), Is.True);
            
            // Assert on specific values inside pagination or tags
            var totalRecords = int.Parse(paginationElement.GetProperty("totalNumberOfRecords").GetString());

            Assert.That(totalRecords, Is.GreaterThanOrEqualTo(0));  // there is a possibility that tags has no data...

            // Check if tags & tag types are arrays and has data (if expected)
            Assert.That(tagsElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(tagsElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            
        }


        [Test]
        
        // TagName:
        [TestCase("", null, null, "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "     ", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "     ", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase(null, null, null, "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase(null, null, "", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name is required!")]
        [TestCase("", null, "This Tag Name is more than 100 characters long to test the max length for the tag name value.........", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name should not be more than 100 characters long!")]
        [TestCase(null, null, "This Tag Name is more than 100 characters long to test the max length for the tag name value.........", "From_Test_Tag_Description", "#ffffff", (sbyte)1, 400, "TagName", "Tag name should not be more than 100 characters long!")]
        
        // TagDescription:
        [TestCase("", null, "From_Test_Tag_Name", "This Tag Description is more than 255 characters long to test the max length for the tag description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", "#ffffff", (sbyte)1, 400, "TagDescription", "Tag description should not be more than 255 characters long!")]
        [TestCase(null, null, "From_Test_Tag_Name", "This Tag Description is more than 255 characters long to test the max length for the tag description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", "#ffffff", (sbyte)1, 400, "TagDescription", "Tag description should not be more than 255 characters long!")]

        [TestCase("", null, "From_Test_Tag_Name", "From_Test_Tag_Description", "#fffffffffffffffffffffffffffffffffffffffffffffffffff", (sbyte)1, 400, "TagColor", "Tag color should not be more than 50 characters long!")]
        [TestCase("", null, "From_Test_Tag_Name", "From_Test_Tag_Institution_Name", "#fffffffffffffffffffffffffffffffffffffffffffffffffff", (sbyte)1, 400, "TagColor", "Tag color should not be more than 50 characters long!")]
        
        public async Task TagsSaveTag_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? tagId, ulong? userId, string? tagName, string? tagDescription, string? tagColor, sbyte? isTagActive, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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

            // Validate if record is saved...
            invalidTag = DatabaseHelperForTests.GetTagRecordByName(invalidTag.TagName, _userId, _tempConnection);
            Assert.That(invalidTag, Is.Null);

        }

        [Test]
        public async Task TagsAddTag_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Tag/DeleteTag?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { tagId = _tagRecord.TagId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // Cleanup Dummy Data in Related Tables:
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("tags", _tempConnection);

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

        }

    }

}




