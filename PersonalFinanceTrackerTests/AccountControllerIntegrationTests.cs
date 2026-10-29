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
    public class AccountControllerIntegrationTests : HttpTestBase
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

            // Otherwise tokens are generated for the _userId = 0
            if (_userId == "0")
                return;

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
        [TestCase("/accounts")]
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
        [TestCase("/accounts")]
        [TestCase("/Account/GetAccount")]
        [TestCase("/Account/SaveAccount")]
        [TestCase("/Account/DeleteAccount")]
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
                
                case "/accounts":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    break;

                case "/Account/GetAccount":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { accountId = _accountRecord.AccountId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { accountId = _accountRecord.AccountId });
                    break;

                case "/Account/SaveAccount":
                    requestPostInValidAuthorize.Content =  JsonContent.Create(new { _accountRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _accountRecord });
                    break;

                case "/Account/DeleteAccount":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { accountId = _accountRecord.AccountId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { accountId = _accountRecord.AccountId });
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
        [TestCase(true)]
        [TestCase(false)]
        public async Task AccountsIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload
            (bool isFirstRequest)
        {
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/accounts?connString=pft_con_str_testenv");

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
             * or worry about deserialization exceptions if a property happens to be null (like accountTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify all three root properties exist in the first request's response
            Assert.That(root.TryGetProperty("accounts", out var accountsElement), Is.True);
            Assert.That(root.TryGetProperty("pagination", out var paginationElement), Is.True);
            Assert.That(root.TryGetProperty("accountTypes", out var accountTypesElement), Is.True);

            // Assert on specific values inside pagination or accounts
            var totalRecords = int.Parse(paginationElement.GetProperty("totalNumberOfRecords").GetString());

            Assert.That(totalRecords, Is.GreaterThanOrEqualTo(0));  // there is a possibility that accounts has no data...

            // Check if accounts & account types are arrays and has data (if expected)
            Assert.That(accountsElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(accountsElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            if (isFirstRequest)
            {
                Assert.That(accountTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(accountTypesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            }
            else
            {
                Assert.That(accountTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Null));
            }

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
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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

            // Validate if record is saved... 
            invalidAccount = DatabaseHelperForTests.GetAccountRecordByName(invalidAccount.AccountName, _userId, _tempConnection);
            Assert.That(invalidAccount, Is.Null);

        }


        [Test]
        public async Task AccountsAddAccount_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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
         *  When an Account is deleted:
         *      pft_transactions: Records are deleted!
         *      pft_import_templates: Records are deleted!
         *      pft_recurring_schedules: Records are deleted!
         *      pft_trasactions_repeats: Records are deleted!
         *      pft_transactions_rules: Records are deleted!
         *      pft_accounts: Records are deleted! 
         * 
         */
        public async Task AccountsDeleteAccount_AuthenticatedRequest_DeletesAccountRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

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

        }

    }

}




