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
    public class DashboardControllerIntegrationTests
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private DashboardFilter _dashboardFilter = null;
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

            // Dashboard Filter
            _dashboardFilter = new DashboardFilter
            {

                DashboardParametersStartDate = new DateOnly(DateTime.Now.Year, 1, 1),
                DashboardParametersEndDate = new DateOnly(DateTime.Now.Year, 12, 31),

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
        public async Task DashboardUnAuthenticatedRequests_ForAuthenticatedActions_ReturnsUnauthorizedOrBadRequest()
        {

            // ARRANGE - Build an explicit request message to attach the test header
            var requestIndexGet = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/Index");

            var requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetUserCurrencyDetailsAndLicenseKey");
            var requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetUserCurrencyDetailsAndLicenseKey");

            var requestGetReportsDataPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetReportsData");
            var requestGetReportsDataPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetReportsData");


            // Configure Header to fail authentication for this request by TestAuthHandler 
            // requestIndexGet
            requestIndexGet.Headers.Add("X-Test-Unauthorized", "true");

            // InValidAntiforgeryToken
            var (token, cookie) = await GetAntiforgeryTokensAsync(false);

            // requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize.Content = JsonContent.Create(new {  });

            // requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken.Content = JsonContent.Create(new { });

            // requestGetDashboardPostInValidAuthorize
            requestGetReportsDataPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestGetReportsDataPostInValidAuthorize.Content = JsonContent.Create(new { _dashboardFilter });

            // requestGetDashboardPostInValidAntiforgeryToken
            requestGetReportsDataPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestGetReportsDataPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _dashboardFilter });


            // ACT - Send the GET request through the custom client
            var responseIndexGet = await _client.SendAsync(requestIndexGet);

            var responseGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize = await _client.SendAsync(requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize);
            var responseGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken = await _client.SendAsync(requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken);

            var responseGetReportsDataPostInValidAuthorize = await _client.SendAsync(requestGetReportsDataPostInValidAuthorize);
            var responseGetReportsDataPostInValidAntiforgeryToken = await _client.SendAsync(requestGetReportsDataPostInValidAntiforgeryToken);


            // ASSERT - Verify the server successfully blocked the request with 401 Unauthorized
            Assert.That(responseIndexGet.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            Assert.That(responseGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseGetReportsDataPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseGetReportsDataPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        }

        [Test]
        public async Task DashboardIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/Index");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.SendAsync(request);
            
            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>Dashboard</h3>"), Is.True);   // Contents of report view

        }

        [Test]
        public async Task DashboardsAllowAnonymousActions_UnAuthenticatedRequests_ReturnsValidRedirectToActions()
        {

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var responseVerifyEmail = await _client.GetAsync("VerifyEmail");
            var responsePrivacy = await _client.GetAsync("/Dashboard/Privacy");

            // Assert
            Assert.That(responseVerifyEmail.IsSuccessStatusCode, Is.True, "The request failed with status code: " + responseVerifyEmail.StatusCode);
            Assert.That(responsePrivacy.IsSuccessStatusCode, Is.True, "The request failed with status code: " + responsePrivacy.StatusCode);

            Assert.That(responseVerifyEmail.RequestMessage.RequestUri.AbsoluteUri, Is.EqualTo("http://localhost/Error/Error%2FInternalServerError")); // As no tokens provided but request reached

            var jsonResponsePrivacy = await responsePrivacy.Content.ReadAsStringAsync();
            Assert.That(jsonResponsePrivacy, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponsePrivacy.Contains("<h1>Privacy Policy</h1>"), Is.True);   // Contents of account view

        }

        [Test]
        [TestCase(null, false, "pft_con_str_testenv")]
        [TestCase("waGUTqmYYZrDZSLAqBLGaPdUZvCNfV46f89OTZbDVq48vwUg-77wyt9c7Sp-c12bt-vA1xwkyjaQX4vDl8zxFnblK_3USlROAO6zGfsxRdDfhNe5cAHBg0eC66oconm7tVUs06pjkzs1KijnPo7PVaDbx2TYJ7dAi-LQxF6mAmM28FkmUa6xvEcTckDamTR5Y1Y8C0YBhRk8vxudCxwg2nqOhKxTHxY7250gWTwtzLdI8Zs2AsY38Z2Kqrou75", false, "pft_con_str_testenv")]
        [TestCase("LneO3S8w325fcNYNei0hkgEw0md-vR1YKOL4UlP2Ay1Pckp5oU5whtNCwhD6hDrUyEyOQAMfNosdsWunkLj60ZwaGUTqmYYZrDZSLAqBLGaPdUZvCNfV46f89OTZbDVq48vwUg-77wyt9c7Sp-c12bt-vA1xwkyjaQX4vDl8zxFnblK_3USlROAO6zGfsxRdDfhNe5cAHBg0eC66oconm7tVUs06pjkzs1KijnPo7PVaDbx2TYJ7dAi-LQxF6mAmM28FkmUa6xvEcTckDamTR5Y1Y8C0YBhRk8vxudCxwg2nqOhKxTHxY7250gWTwtzLdI8Zs2AsY38Z2Kqrou75", true, "pft_con_str_testenv")]
        public async Task DashboardsVerifyEmailGet_NoAuthenticationRequest_ReturnsRedirectToIndexActionOrErrorPage(string? token, bool isValidTokenUsed, string? connString)
        {

            // ARRANGE
            DatabaseHelperForTests.ResetEmailVarificationFieldValuesForUser(_userId, _tempConnection);
            (int isUserEmailVarifiedBefore, int isUsedBefore) beforeEmailVarificationValues = DatabaseHelperForTests.GetEmailVarificationFieldValuesForUser(_userId, _tempConnection);

            string _emailVarificationUri = $"VerifyEmail?{(token != null ? $"token={token}&" : "")}connString={connString}";
                                                    
            // ACT
            var response = await _client.GetAsync(_emailVarificationUri);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            (int isUserEmailVarifiedAfter, int isUsedAfter) afterEmailVarificationValues = DatabaseHelperForTests.GetEmailVarificationFieldValuesForUser(_userId, _tempConnection);

            // ASSERT
            Assert.That(beforeEmailVarificationValues.isUserEmailVarifiedBefore, Is.EqualTo(0));
            Assert.That(beforeEmailVarificationValues.isUsedBefore, Is.EqualTo(0));

            Assert.That(response.IsSuccessStatusCode, Is.True);
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);

            if (isValidTokenUsed)
            {

                Assert.That(jsonResponse.Contains("<h3>Dashboard</h3>"), Is.True);
                
            }
            else
            {
                Assert.That(response.RequestMessage.RequestUri.AbsoluteUri, Is.EqualTo("http://localhost/Error/Error%2FInternalServerError"));
            }

            Assert.That(afterEmailVarificationValues.isUserEmailVarifiedAfter, Is.EqualTo(isValidTokenUsed ? 1 : 0));
            Assert.That(afterEmailVarificationValues.isUsedAfter, Is.EqualTo(isValidTokenUsed ? 1 : 0));

        }


        [Test]
        public async Task DashboradUnAuthenticatedRequest_ForAuthorizedActions_ReturnsUnAuthorizedOrBadRequest()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(false);

            var requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetUserCurrencyDetailsAndLicenseKey");
            var requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetUserCurrencyDetailsAndLicenseKey");

            var requestGetReportsDataPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetReportsData");
            var requestGetReportsDataPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetReportsData");

            // requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize.Content = JsonContent.Create(new { });

            // requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);    // Cookie is eleminated as null
            requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken.Content = JsonContent.Create(new { });


            // requestGetReportsDataPostInValidAuthorize
            requestGetReportsDataPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestGetReportsDataPostInValidAuthorize.Content = JsonContent.Create(new { });

            // requestGetReportsDataPostInValidAntiforgeryToken
            requestGetReportsDataPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);    // Cookie is eleminated as null
            requestGetReportsDataPostInValidAntiforgeryToken.Content = JsonContent.Create(new { });


            // ACT
            var responseGetUserCurrencyDetailsAndLicenseKeyInValidAuthorize = await _client.SendAsync(requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAuthorize);
            var responseGetUserCurrencyDetailsAndLicenseKeyInValidAntiforgeryToken = await _client.SendAsync(requestGetUserCurrencyDetailsAndLicenseKeyPostInValidAntiforgeryToken);

            var responseGetReportsDataPostInValidAuthorize = await _client.SendAsync(requestGetReportsDataPostInValidAuthorize);
            var responseGetReportsDataPostInValidAntiforgeryToken = await _client.SendAsync(requestGetReportsDataPostInValidAntiforgeryToken);


            // ASSERT - Verify the server successfully blocked the request with 401 Unauthorized
            Assert.That(responseGetUserCurrencyDetailsAndLicenseKeyInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseGetUserCurrencyDetailsAndLicenseKeyInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseGetReportsDataPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseGetReportsDataPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        }

        [Test]
        [TestCase("2026-12-31", "2026-01-01", 400, "DashboardParametersStartDate", "Start date must be earlier than end date!")]
        public async Task DashboardGetReportsData_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (DateOnly dashboardParametersStartDate, DateOnly dashboardParametersEndDate, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetReportsData?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            DashboardFilter invalidDashboardFilter = new DashboardFilter
            {

                DashboardParametersStartDate = dashboardParametersStartDate,
                DashboardParametersEndDate = dashboardParametersEndDate

            };

            request.Content = JsonContent.Create(invalidDashboardFilter);
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
        public async Task DashboradGetUserCurrencyDetailsAndLicenseKeyPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var requestGetUserCurrencyDetailsAndLicenseKey = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetUserCurrencyDetailsAndLicenseKey");
            
            requestGetUserCurrencyDetailsAndLicenseKey.Headers.Add("Request-Verification-Token", token);
            requestGetUserCurrencyDetailsAndLicenseKey.Headers.Add("Cookie", cookie);
            requestGetUserCurrencyDetailsAndLicenseKey.Content = JsonContent.Create(new {  });

            // ACT
            var responseGetUserCurrencyDetailsAndLicenseKey = await _client.SendAsync(requestGetUserCurrencyDetailsAndLicenseKey);
            var jsonResponseGetUserCurrencyDetailsAndLicenseKey = await responseGetUserCurrencyDetailsAndLicenseKey.Content.ReadAsStringAsync();


            // ASSERT
            Assert.That(responseGetUserCurrencyDetailsAndLicenseKey.IsSuccessStatusCode, Is.True);

            /*
             * Parse the JSON tree:
             * Using JsonDocument allows you to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             * or worry about deserialization exceptions if a property happens to be null (like reportTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponseGetUserCurrencyDetailsAndLicenseKey);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(root.TryGetProperty("userCurrencyDetails", out var userCurrencyDetailsElement), Is.True);
            Assert.That(root.TryGetProperty("licenseKey", out var licenseKeyElement), Is.True);

            // Assert on specific values inside pagination or reports
            var currencyCode = userCurrencyDetailsElement.GetProperty("currencyCode").GetString();
            var currencySymbol = userCurrencyDetailsElement.GetProperty("currencySymbol").GetString();
            
            Assert.That(currencyCode, Is.Not.Empty.Or.Null);
            Assert.That(currencySymbol, Is.Not.Empty.Or.Null);

            Assert.That(licenseKeyElement.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(licenseKeyElement.GetString().Length, Is.GreaterThan(0));

        }


        [Test]
        public async Task DashboradGetReportsDataPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var requestGetReportsDataPost = new HttpRequestMessage(HttpMethod.Post, "/Dashboard/GetReportsData?connString=pft_con_str_testenv");

            requestGetReportsDataPost.Headers.Add("Request-Verification-Token", token);
            requestGetReportsDataPost.Headers.Add("Cookie", cookie);
            requestGetReportsDataPost.Content = JsonContent.Create(new { _dashboardFilter });

            // ACT
            var responseGetReportsDataPost = await _client.SendAsync(requestGetReportsDataPost);
            var jsonResponseGetReportsDataPost = await responseGetReportsDataPost.Content.ReadAsStringAsync();


            // ASSERT
            Assert.That(responseGetReportsDataPost.IsSuccessStatusCode, Is.True);

            /*
             * Parse the JSON tree:
             * Using JsonDocument allows you to peek into the exact raw JSON structure returned by the endpoint without needing to instantiate full lists 
             * or worry about deserialization exceptions if a property happens to be null (like reportTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponseGetReportsDataPost);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(root.TryGetProperty("categoryWiseSummaryChartData", out var categoryWiseSummaryChartDataElement), Is.True);
            Assert.That(root.TryGetProperty("monthlyIncomeExpenseChartData", out var monthlyIncomeExpenseChartDataElement), Is.True);
            Assert.That(root.TryGetProperty("monthlyNetIncomeChartData", out var monthlyNetIncomeChartDataElement), Is.True);
            Assert.That(root.TryGetProperty("monthlyNetWorthChartData", out var monthlyNetWorthChartDataElement), Is.True);

            // Assert on specific values inside pagination or reports
            Assert.That(categoryWiseSummaryChartDataElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(categoryWiseSummaryChartDataElement.GetArrayLength, Is.GreaterThanOrEqualTo(0));

            Assert.That(monthlyIncomeExpenseChartDataElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(monthlyIncomeExpenseChartDataElement.GetArrayLength, Is.GreaterThanOrEqualTo(0));

            Assert.That(monthlyNetIncomeChartDataElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(monthlyNetIncomeChartDataElement.GetArrayLength, Is.GreaterThanOrEqualTo(0));

            Assert.That(monthlyNetWorthChartDataElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(monthlyNetWorthChartDataElement.GetArrayLength, Is.GreaterThanOrEqualTo(0));

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




