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
    public class AccountControllerIntegrationTests
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private Account? _accountRecord = null;
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

            var accountRec = DatabaseHelperForTests.GetSingleRecordFromAccountsTable(_userId, _tempConnection);
            if (accountRec != null) { 

                _accountRecord = accountRec;

            }
            else
            {

                _accountRecord = new Account  {
                                                UserId = ulong.Parse(_userId),
                                                AccountId = "2222222222222222222",      // MAX BIGINT: 9223372036854775807
                                                AccountName = "Test Account Name",
                                                AccountInstitutionName = "Test Account Institution",
                                                AccountTypeId = 101,
                                                AccountDescription = "Test Account Description",
                                                IsALinkedAccount = (byte)0
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
        public async Task AccountsAllActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest()
        {

            // ARRANGE - Build an explicit request message to attach the test header
            var requestIndexGet = new HttpRequestMessage(HttpMethod.Get, "/accounts");

            var requestIndexPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/accounts");
            var requestIndexPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/accounts");

            var requestGetAccountPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Account/GetAccount");
            var requestGetAccountPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Account/GetAccount");

            var requestSaveAccountPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Account/SaveAccount");
            var requestSaveAccountPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Account/SaveAccount");

            var requestDeleteAccountPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, "/Account/DeleteAccount");
            var requestDeleteAccountPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, "/Account/DeleteAccount");

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

            // requestGetAccountPostInValidAuthorize
            requestGetAccountPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestGetAccountPostInValidAuthorize.Content = JsonContent.Create(new { accountId = _accountRecord == null ? "2222222222222222222" : _accountRecord.AccountId });

            // requestGetAccountPostInValidAntiforgeryToken
            requestGetAccountPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestGetAccountPostInValidAntiforgeryToken.Content = JsonContent.Create(new { accountId = _accountRecord == null ? "2222222222222222222" : _accountRecord.AccountId });

            // requestSaveAccountPostInValidAuthorize
            requestSaveAccountPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestSaveAccountPostInValidAuthorize.Content = JsonContent.Create(new { _accountRecord });

            // requestSaveAccountPostInValidAntiforgeryToken
            requestSaveAccountPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestSaveAccountPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _accountRecord });

            // requestDeleteAccountPostInValidAuthorize
            requestDeleteAccountPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");
            requestDeleteAccountPostInValidAuthorize.Content = JsonContent.Create(new { accountId = _accountRecord == null ? "2222222222222222222" : _accountRecord.AccountId });

            // requestDeleteAccountPostInValidAntiforgeryToken
            requestDeleteAccountPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);  // 'cookie' eliminated as null;
            requestDeleteAccountPostInValidAntiforgeryToken.Content = JsonContent.Create(new { accountId = _accountRecord == null ? "2222222222222222222" : _accountRecord.AccountId });


            // ACT - Send the GET request through the custom client
            var responseIndexGet = await _client.SendAsync(requestIndexGet);

            var responseIndexPostInValidAuthorize = await _client.SendAsync(requestIndexPostInValidAuthorize);
            var responseIndexPostInValidAntiforgeryToken = await _client.SendAsync(requestIndexPostInValidAntiforgeryToken);

            var responseGetAccountPostInValidAuthorize = await _client.SendAsync(requestGetAccountPostInValidAuthorize);
            var responseGetAccountPostInValidAntiforgeryToken = await _client.SendAsync(requestGetAccountPostInValidAntiforgeryToken);

            var responseSaveAccountPostInValidAuthorize = await _client.SendAsync(requestSaveAccountPostInValidAuthorize);
            var responseSaveAccountPostInValidAntiforgeryToken = await _client.SendAsync(requestSaveAccountPostInValidAntiforgeryToken);

            var responseDeleteAccountPostInValidAuthorize = await _client.SendAsync(requestDeleteAccountPostInValidAuthorize);
            var responseDeleteAccountPostInValidAntiforgeryToken = await _client.SendAsync(requestDeleteAccountPostInValidAntiforgeryToken);


            // ASSERT - Verify the server successfully blocked the request with 401 Unauthorized
            Assert.That(responseIndexGet.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            Assert.That(responseIndexPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseIndexPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseGetAccountPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseGetAccountPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseSaveAccountPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseSaveAccountPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(responseDeleteAccountPostInValidAuthorize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responseDeleteAccountPostInValidAntiforgeryToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        }

        [Test]
        public async Task AccountsIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {
            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/accounts");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>Accounts "), Is.True);   // Contents of account view

        }

        [Test]
        public async Task AccountsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/accounts?connString=pft_con_str_testenv");
            var consecutiveRequests = new HttpRequestMessage(HttpMethod.Post, "/accounts?connString=pft_con_str_testenv");


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
             * or worry about deserialization exceptions if a property happens to be null (like accountTypes when firstRequest is false).
             */
            using var docFirstRequest = JsonDocument.Parse(jsonResponseFirstRequest);
            var rootFirstRequest = docFirstRequest.RootElement;

            using var docConsecutiveRequests = JsonDocument.Parse(jsonResponseConsecutiveRequests);
            var rootConsecutiveRequests = docConsecutiveRequests.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(rootFirstRequest.TryGetProperty("accounts", out var accountsElementFirstRequest), Is.True);
            Assert.That(rootFirstRequest.TryGetProperty("pagination", out var paginationElementFirstRequest), Is.True);
            Assert.That(rootFirstRequest.TryGetProperty("accountTypes", out var accountTypesElementFirstRequest), Is.True);

            // Verify all three root properties exist in the consecutive request's response
            Assert.That(rootConsecutiveRequests.TryGetProperty("accounts", out var accountsElementConsecutiveRequests), Is.True);
            Assert.That(rootConsecutiveRequests.TryGetProperty("pagination", out var paginationElementConsecutiveRequests), Is.True);
            Assert.That(rootConsecutiveRequests.TryGetProperty("accountTypes", out var accountTypesElementConsecutiveRequests), Is.True);

            // Assert on specific values inside pagination or accounts
            var totalRecordsFirstRequest = int.Parse(paginationElementFirstRequest.GetProperty("totalNumberOfRecords").GetString());
            var totalRecordsConsecutiveRequest = int.Parse(paginationElementConsecutiveRequests.GetProperty("totalNumberOfRecords").GetString());


            Assert.That(totalRecordsFirstRequest, Is.GreaterThanOrEqualTo(0));  // there is a possibility that accounts has no data...
            Assert.That(totalRecordsConsecutiveRequest, Is.GreaterThanOrEqualTo(0));  // there is a possibility that accounts has no data...


            // Check if accounts & account types are arrays and has data (if expected)
            Assert.That(accountsElementFirstRequest.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(accountsElementFirstRequest.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            Assert.That(accountTypesElementFirstRequest.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(accountTypesElementFirstRequest.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            // Check if accounts is an array and has data (if expected) & account types is null
            Assert.That(accountsElementConsecutiveRequests.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(accountsElementConsecutiveRequests.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            Assert.That(accountTypesElementConsecutiveRequests.ValueKind, Is.EqualTo(JsonValueKind.Null));

        }

        [Test]
        
        // AccountName
        [TestCase("", null, "", "From_Test_Account_Institution_Name", 101, "From_Test_Account_Description", 0, 400, "AccountName", "Account name is required!")]
        [TestCase(null, null, "", "From_Test_Account_Institution_Name", 101, "From_Test_Account_Description", 0, 400, "AccountName", "Account name is required!")]
        [TestCase(null, null, "   ", "From_Test_Account_Institution_Name", 101, "From_Test_Account_Description", 0, 400, "AccountName", "Account name is required!")]
        [TestCase("", null, "This Account Name is more than 100 characters long to test the max length for the account name value...", "From_Test_Account_Institution_Name", 101, "From_Test_Account_Description", 0, 400, "AccountName", "Account name should not be more than 100 characters long!")]
        [TestCase(null, null, "This Account Name is more than 100 characters long to test the max length for the account name value...", "From_Test_Account_Institution_Name", 101, "From_Test_Account_Description", 0, 400, "AccountName", "Account name should not be more than 100 characters long!")]

        // AccountInstitutionName
        [TestCase("", null, "From_Test_Account_Name", "", 101, "From_Test_Account_Description", 0, 400, "AccountInstitutionName", "Account institution name is required!")]
        [TestCase(null, null, "From_Test_Account_Name", "", 101, "From_Test_Account_Description", 0, 400, "AccountInstitutionName", "Account institution name is required!")]
        [TestCase("", null, "From_Test_Account_Name", "This Account Institution Name is more than 100 characters long to test the max length for the account institution name...", 101, "From_Test_Account_Description", 0, 400, "AccountInstitutionName", "Account institution name should not be more than 100 characters long!")]
        [TestCase(null, null, "From_Test_Account_Name", "This Account Institution Name is more than 100 characters long to test the max length for the account institution name...", 101, "From_Test_Account_Description", 0, 400, "AccountInstitutionName", "Account institution name should not be more than 100 characters long!")]

        // AccountDescription
        [TestCase("", null, "From_Test_Account_Name", "From_Test_Account_Institution_Name", 101, "This Account description is more than 255 characters long - blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", 0, 400, "AccountDescription", "Account description should not be more than 255 characters long!")]
        [TestCase(null, null, "From_Test_Account_Name", "From_Test_Account_Institution_Name", 101, "This Account description is more than 255 characters long - blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...", 0, 400, "AccountDescription", "Account description should not be more than 255 characters long!")]
        
        public async Task AccountsSaveAccount_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? accountId, ulong? userId, string accountName, string accountInstitutionName, int accountTypeId, string? accountDescription, byte isALinkedAccount, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Account/SaveAccount?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Account invalidAccount = new Account
            {
                AccountId = accountId,
                UserId = userId,
                AccountName = accountName,
                AccountInstitutionName = accountInstitutionName,
                AccountTypeId = accountTypeId,
                AccountDescription = accountDescription,
                IsALinkedAccount = isALinkedAccount,
            };

            request.Content = JsonContent.Create(invalidAccount);
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

            // Validate Newly saved record...Replace the existing '_accountRecord' values
            invalidAccount = DatabaseHelperForTests.GetAccountRecordByName(invalidAccount.AccountName, _userId, _tempConnection);
            Assert.That(invalidAccount, Is.Null);

        }

        [Test]
        public async Task AccountsAddAccount_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Account/SaveAccount?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Delete any existing record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_accounts", "account_id", _accountRecord.AccountId, _tempConnection);

            _accountRecord.AccountId = "";    // To insert a new record!!!
            request.Content = JsonContent.Create( _accountRecord );
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

            // Validate Newly saved record...Replace the existing '_accountRecord' values
            _accountRecord = DatabaseHelperForTests.GetAccountRecordByName(_accountRecord.AccountName, _userId, _tempConnection);
            Assert.That(_accountRecord.AccountId, Is.Not.Null);

        }


        [Test]
        public async Task AccountsUpdateAccount_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Account/SaveAccount?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Account updatedAccount = new Account();
            updatedAccount.AccountId = _accountRecord.AccountId;
            updatedAccount.AccountName = _accountRecord.AccountName + " - Updated";
            updatedAccount.AccountDescription = _accountRecord.AccountDescription + " - Updated";
            updatedAccount.AccountInstitutionName = _accountRecord.AccountInstitutionName + " - Updated";
            updatedAccount.AccountTypeId = 501;
            updatedAccount.IsALinkedAccount = (byte)1;

            request.Content = JsonContent.Create(updatedAccount);
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

            // Validate Newly saved record...Replace the existing '_accountRecord' values
            _accountRecord = DatabaseHelperForTests.GetAccountRecordByName(updatedAccount.AccountName, _userId, _tempConnection);
            Assert.That(_accountRecord.AccountName, Is.EqualTo(updatedAccount.AccountName));
            Assert.That(_accountRecord.AccountDescription, Is.EqualTo(updatedAccount.AccountDescription));
            Assert.That(_accountRecord.AccountInstitutionName, Is.EqualTo(updatedAccount.AccountInstitutionName));
            Assert.That(_accountRecord.AccountTypeId, Is.EqualTo(updatedAccount.AccountTypeId));
            Assert.That(_accountRecord.IsALinkedAccount, Is.EqualTo(updatedAccount.IsALinkedAccount));


        }

        [Test]
        public async Task AccountsGetAccount_AuthenticatedRequest_ReturnsAccountRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Account/GetAccount?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { accountId = _accountRecord.AccountId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Deserialize the JsonElement into your Account class
            Account? accountFromRoot = root.Deserialize<Account>(new JsonSerializerOptions
                                                {
                                                    PropertyNameCaseInsensitive = true, // Recommended if JSON keys are camelCase and C# properties are PascalCase
                                                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                                                });

            Assert.That(accountFromRoot, Is.Not.Null);
            Assert.That(accountFromRoot.AccountName, Is.EqualTo(_accountRecord.AccountName));
            Assert.That(accountFromRoot.AccountDescription, Is.EqualTo(_accountRecord.AccountDescription));
            Assert.That(accountFromRoot.AccountInstitutionName, Is.EqualTo(_accountRecord.AccountInstitutionName));
            Assert.That(accountFromRoot.AccountTypeId, Is.EqualTo(_accountRecord.AccountTypeId));
            Assert.That(accountFromRoot.IsALinkedAccount, Is.EqualTo(_accountRecord.IsALinkedAccount));

        }

        [Test]
        /*
         * 
         * When an Account is deleted:
         * pft_transactions: Records are deleted!
         * pft_import_templates: Records are deleted!
         * pft_recurring_schedules: Records are deleted!
         * pft_trasactions_repeats: Records are deleted!
         * pft_transactions_rules: Records are deleted!
         * pft_accounts: Records are deleted! 
         * 
         */
        public async Task AccountsDeleteAccount_AuthenticatedRequest_DeletesAccountRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Account/DeleteAccount?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { accountId = _accountRecord.AccountId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // Generate dummy data in related tables...pft_transactions, pft_recurring_schedules
            DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("accounts", _userId, _tempConnection, _accountRecord.AccountId, null);

            int transactionsRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "account_id", _accountRecord.AccountId, _tempConnection);
            int importTemplatesRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_import_templates", "account_id", _accountRecord.AccountId, _tempConnection);
            int recurringSchedulesRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "account_id", _accountRecord.AccountId, _tempConnection);
            int transactionsRepeatRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_trasactions_repeats", "account_id", _accountRecord.AccountId, _tempConnection);
            int transactionsRuleRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "account_id", _accountRecord.AccountId, _tempConnection);
            

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nullAccountRecord = DatabaseHelperForTests.GetAccountRecordByName(_accountRecord.AccountName, _userId, _tempConnection);

            int transactionsRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "account_id", _accountRecord.AccountId, _tempConnection);
            int importTemplatesRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_import_templates", "account_id", _accountRecord.AccountId, _tempConnection);
            int recurringSchedulesRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "account_id", _accountRecord.AccountId, _tempConnection);
            int transactionsRepeatRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_trasactions_repeats", "account_id", _accountRecord.AccountId, _tempConnection);
            int transactionsRuleRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "account_id", _accountRecord.AccountId, _tempConnection);


            // ASSERT

            Assert.That(transactionsRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(importTemplatesRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(recurringSchedulesRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(transactionsRepeatRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(transactionsRuleRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));

            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nullAccountRecord, Is.Null);

            Assert.That(transactionsRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(importTemplatesRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(recurringSchedulesRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(transactionsRepeatRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(transactionsRuleRecordsCountsAfterDelete, Is.EqualTo(0));

            // No need to clean up...All the records are deleted!

        }

        // For Server to authenticate. (Part of client request)
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




