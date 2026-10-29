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
    public class ReportControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private ReportFilter _reportFilter = null;
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

            // Report Filter
            _reportFilter = new ReportFilter
            {

                ReportStartDate = new DateOnly(DateTime.Now.Year, 1, 1),
                ReportEndDate = new DateOnly(DateTime.Now.Year, 12, 31),
                ReportName = "IncomeExpense",          // IncomeExpense, NetWorth, Goal
                ReportInterval = 1,                     // 1 - Monthly, 2 - Duration
                TagId = null

            };

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
        [TestCase("/reports")]
        public async Task ReportsAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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
        [TestCase("/Report/GetTagsAndCurrencyDetails")]
        [TestCase("/Report/GetReport")]
        public async Task ReportsAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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

                case "/Report/GetTagsAndCurrencyDetails":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { });
                    break;

                case "/Report/GetReport":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _reportFilter });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _reportFilter });
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
        public async Task ReportsIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/reports");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>Reports "), Is.True);   // Contents of report view

        }

        
        [Test]
        public async Task ReportsGetTagsAndCurrencyDetailsPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Report/GetTagsAndCurrencyDetails");
            
            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);
            request.Content = JsonContent.Create(new { connString = "pft_con_str_testenv" });

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();


            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True);

            /*
             * Parse the JSON tree:
             * Using JsonDocument allows to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             * or worry about deserialization exceptions if a property happens to be null (like reportTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(root.TryGetProperty("tagsList", out var tagsListElement), Is.True);
            Assert.That(root.TryGetProperty("userCurrencyDetails", out var userCurrencyDetailsElement), Is.True);
            
            // Assert on specific values inside pagination or reports
            var currencyCode = userCurrencyDetailsElement.GetProperty("currencyCode").GetString();
            var currencySymbol = userCurrencyDetailsElement.GetProperty("currencySymbol").GetString();

            Assert.That(currencyCode, Is.Not.Empty.Or.Null);
            Assert.That(currencySymbol, Is.Not.Empty.Or.Null);

            // Check if reports & report types are arrays and has data (if expected)
            Assert.That(tagsListElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(tagsListElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            
        }

        
        [Test]
        // ReportStartDate
        // [TestCase("", "2026-12-31", "IncomeExpense", (byte)1, null, 400, "ReportStartDate", "Start date is required!")]      // Eliminated as default value is generated and provided and null values will fail model
        [TestCase("2026-12-31", "2026-01-01", "IncomeExpense", (byte)1, null, 400, "ReportStartDate", "Start date must be earlier than end date!")]

        // ReportEndDate
        // [TestCase("2026-01-01", "", "IncomeExpense", (byte)1, null, 400, "ReportEndDate", "End date is required!")]
        public async Task ReportsGetReport_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (DateOnly reportStartDate, DateOnly reportEndDate, string reportName, byte reportInterval, string? tagId, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Report/GetReport?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            ReportFilter invalidReport = new ReportFilter
            {
                ReportStartDate = reportStartDate,
                ReportEndDate = reportEndDate,
                ReportName = reportName,          // IncomeExpense, NetWorth, Goal
                ReportInterval = reportInterval,                     // 1 - Monthly, 2 - Duration
                TagId = tagId
            };

            request.Content = JsonContent.Create(invalidReport);
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
        [TestCase("2000-01-01", "2050-12-31", "IncomeExpense", (byte)1, null)]
        [TestCase("2000-01-01", "2050-12-31", "NetWorth", (byte)1, null)]
        [TestCase("2000-01-01", "2050-12-31", "Goal", (byte)1, null)]
        [TestCase("2000-01-01", "2050-12-31", "IncomeExpense", (byte)2, null)]
        [TestCase("2000-01-01", "2050-12-31", "NetWorth", (byte)2, null)]
        [TestCase("2000-01-01", "2050-12-31", "Goal", (byte)2, null)]
        
        public async Task ReportsGetReport_AuthenticatedRequest_ReturnsOk
            (DateOnly reportStartDate, DateOnly reportEndDate, string reportName, byte reportInterval, string? tagId)
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Report/GetReport?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            _reportFilter.ReportStartDate = reportStartDate;
            _reportFilter.ReportEndDate = reportEndDate;
            _reportFilter.ReportName = reportName;
            _reportFilter.ReportInterval = reportInterval;
            _reportFilter.TagId = tagId;

            request.Content = JsonContent.Create(_reportFilter);
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
            Assert.That(root.TryGetProperty("report", out var reportElement), Is.True);
            Assert.That(reportElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(reportElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

        }

    }

}




