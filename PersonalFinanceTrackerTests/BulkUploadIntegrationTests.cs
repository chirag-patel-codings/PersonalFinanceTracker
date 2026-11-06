using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Utilities;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using Syncfusion.EJ2.FileManager.Base;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
    public class TransactionsBulkUploadControllerIntegrationTests : HttpTestBase
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private BulkImportTemplate? _bulkImportTemplateRecord = null;
        private bool _areDummyDataInsertedForCSVBulkUploadTest = false;
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

            var bulkImportTemplateRec = DatabaseHelperForTests.GetSingleRecordFromBulkImportTemplateTable(_userId, _tempConnection);
            if (bulkImportTemplateRec != null) 
            { 

                _bulkImportTemplateRecord = bulkImportTemplateRec;

            }
            else
            {
                DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("bulkimport", _userId, _tempConnection, null, null);
                _bulkImportTemplateRecord = DatabaseHelperForTests.GetSingleRecordFromBulkImportTemplateTable(_userId, _tempConnection, false);

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
        [TestCase("/Transaction/GetBulkImportTemplate")]
        [TestCase("/Transaction/GetBulkImportTemplateNames")]
        [TestCase("/Transaction/SaveBulkImportTemplate")]
        [TestCase("/Transaction/DeleteBulkImportTemplate")]
        [TestCase("/Transaction/UploadCsvFileAndGetContents")]
        [TestCase("/Transaction/ImportCsvFileData")]
        public async Task BulkImportAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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

                case "/Transaction/GetBulkImportTemplate":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { importTemplateName = "test_template" });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { importTemplateName = "test_template" });
                    break;

                case "/Transaction/GetBulkImportTemplateNames":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { });
                    break;

                case "/Transaction/SaveBulkImportTemplate":
                case "/Transaction/ImportCsvFileData":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _bulkImportTemplateRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _bulkImportTemplateRecord });
                    break;

                case "/Transaction/DeleteBulkImportTemplate":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { bulkImportTemplateId = 123 });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { bulkImportTemplateId = 123 });
                    break;

                case "/Transaction/UploadCsvFileAndGetContents":

                    MultipartFormDataContent formData = GetCSVFileAsHeaderContent("TEST_DATA_VALID.CSV");
                    requestPostInValidAuthorize.Content = formData;
                    requestPostInValidAntiforgeryToken.Content = formData;
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
        public async Task TransactionsGetBulkImportTemplateNamesPost_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/GetBulkImportTemplateNames?connString=pft_con_str_testenv");
            
            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);
            request.Content = JsonContent.Create(new { });

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
            Assert.That(root.TryGetProperty("bulkImportTemplateNames", out var bulkImportTemplateNamesElement), Is.True);
            
            // Check if tags & tag types are arrays and has data (if expected)
            Assert.That(bulkImportTemplateNamesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(bulkImportTemplateNamesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            
        }


        [Test]
        [TestCase("", "123", "", (byte) 1, (byte) 1, (sbyte) 1, (sbyte) 2, (sbyte) 3, (sbyte) -1, (sbyte) -1, 400, "ImportTemplateName", "Please provide import template name!")]
        [TestCase("", "123", "This Import Template Name Is More Than 100 Characters Long...blah...blah...blah...blah...blah...blah.", (byte)1, (byte)1, (sbyte)1, (sbyte)2, (sbyte)3, (sbyte)-1, (sbyte)-1, 400, "ImportTemplateName", "Import template name should not be more than 100 characters long!")]
        public async Task TransactionsSaveBulkImportTemplate_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? importTemplateId, string? accountId, string importTemplateName, byte importType, byte headerRowIndex, sbyte dateFieldIndex, sbyte descriptionFieldIndex, sbyte amountFieldIndex, sbyte debitFieldIndex, sbyte creditFieldIndex, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/SaveBulkImportTemplate?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            BulkImportTemplate invalidBulkUpload = new BulkImportTemplate
            {
                ImportTemplateId = importTemplateId,
                UserId = ulong.Parse(_userId),
                AccountId = accountId,
                ImportTemplateName = importTemplateName,
                ImportType = importType,    // CSV = 1 or Bank Linked: 2
                HeaderRowIndex = headerRowIndex,
                DateFieldIndex = dateFieldIndex,
                DescriptionFieldIndex = descriptionFieldIndex,
                AmountFieldIndex = amountFieldIndex,
                DebitFieldIndex = debitFieldIndex,
                CreditFieldIndex = creditFieldIndex
            };

            request.Content = JsonContent.Create(invalidBulkUpload);
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
            invalidBulkUpload = DatabaseHelperForTests.GetBulkImportTemplateRecordByName(invalidBulkUpload.ImportTemplateName, _userId, _tempConnection);
            Assert.That(invalidBulkUpload, Is.Null);

        }

        [Test]
        [TestCase("New Template Added Through Test 1")]
        public async Task TransactionsAddBulkImportTemplate_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord(string newTemplateName)
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/SaveBulkImportTemplate?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Delete any existing record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_import_templates", "import_template_id", _bulkImportTemplateRecord.ImportTemplateId, _tempConnection);

            _bulkImportTemplateRecord.ImportTemplateName = newTemplateName;    // To insert a new record!!!
            request.Content = JsonContent.Create( _bulkImportTemplateRecord );
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

            // Validate Newly saved record...Replace the existing '_bulkImportTemplateRecord' values
            _bulkImportTemplateRecord = DatabaseHelperForTests.GetBulkImportTemplateRecordByName(_bulkImportTemplateRecord.ImportTemplateName, _userId, _tempConnection);
            Assert.That(_bulkImportTemplateRecord.ImportTemplateId, Is.Not.Null);

        }


        [Test]
        public async Task TransactionsUpdateBulkImportTemplate_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/SaveBulkImportTemplate?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            string newAccountRecordId = DatabaseHelperForTests.GenerateAdditionalAccountRecordAndGetId(_userId, _tempConnection, "2");

            BulkImportTemplate updatedBulkImportTemplate = new BulkImportTemplate();
            updatedBulkImportTemplate.ImportTemplateId = _bulkImportTemplateRecord.ImportTemplateId;
            updatedBulkImportTemplate.AccountId = newAccountRecordId;
            updatedBulkImportTemplate.ImportTemplateName = _bulkImportTemplateRecord.ImportTemplateName;    // Templates are referenced by Name not ID. If different name then new record is created...
            updatedBulkImportTemplate.ImportType = (byte)2;
            updatedBulkImportTemplate.HeaderRowIndex = (byte)6;
            updatedBulkImportTemplate.DateFieldIndex = (sbyte)3;
            updatedBulkImportTemplate.DescriptionFieldIndex = (sbyte)4;
            updatedBulkImportTemplate.AmountFieldIndex = (sbyte)-1;
            updatedBulkImportTemplate.DebitFieldIndex = (sbyte)5;
            updatedBulkImportTemplate.CreditFieldIndex =  (sbyte)6;

            request.Content = JsonContent.Create(updatedBulkImportTemplate);
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

            // Validate Newly saved record...Replace the existing '_bulkImportTemplateRecord' values
            _bulkImportTemplateRecord = DatabaseHelperForTests.GetBulkImportTemplateRecordByName(updatedBulkImportTemplate.ImportTemplateName, _userId, _tempConnection);
            Assert.That(_bulkImportTemplateRecord.ImportTemplateId, Is.EqualTo(updatedBulkImportTemplate.ImportTemplateId));
            Assert.That(_bulkImportTemplateRecord.AccountId, Is.EqualTo(updatedBulkImportTemplate.AccountId));
            Assert.That(_bulkImportTemplateRecord.ImportTemplateName, Is.EqualTo(updatedBulkImportTemplate.ImportTemplateName));
            Assert.That(_bulkImportTemplateRecord.ImportType, Is.EqualTo(updatedBulkImportTemplate.ImportType));
            Assert.That(_bulkImportTemplateRecord.HeaderRowIndex, Is.EqualTo(updatedBulkImportTemplate.HeaderRowIndex));
            Assert.That(_bulkImportTemplateRecord.DateFieldIndex, Is.EqualTo(updatedBulkImportTemplate.DateFieldIndex));
            Assert.That(_bulkImportTemplateRecord.DescriptionFieldIndex, Is.EqualTo(updatedBulkImportTemplate.DescriptionFieldIndex));
            Assert.That(_bulkImportTemplateRecord.AmountFieldIndex, Is.EqualTo(updatedBulkImportTemplate.AmountFieldIndex));
            Assert.That(_bulkImportTemplateRecord.DebitFieldIndex, Is.EqualTo(updatedBulkImportTemplate.DebitFieldIndex));
            Assert.That(_bulkImportTemplateRecord.CreditFieldIndex, Is.EqualTo(updatedBulkImportTemplate.CreditFieldIndex));

        }

        [Test]
        public async Task BulkUploadsGetBulkImportTemplate_AuthenticatedRequest_ReturnsTransactionsBulkUploadRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/GetBulkImportTemplate?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { importTemplateName = _bulkImportTemplateRecord.ImportTemplateName });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("importTemplate", out var importTemplateElement), Is.True);

            
            Assert.That(importTemplateElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            Assert.That(_bulkImportTemplateRecord.ImportTemplateId, Is.EqualTo(importTemplateElement[0].GetProperty("importTemplateId").GetString()));
            Assert.That(_bulkImportTemplateRecord.AccountId, Is.EqualTo(importTemplateElement[0].GetProperty("accountId").GetString()));
            Assert.That(_bulkImportTemplateRecord.ImportTemplateName, Is.EqualTo(importTemplateElement[0].GetProperty("importTemplateName").GetString()));

            Assert.That(_bulkImportTemplateRecord.ImportType, Is.EqualTo(byte.Parse(importTemplateElement[0].GetProperty("importType").GetString())));
            Assert.That(_bulkImportTemplateRecord.HeaderRowIndex, Is.EqualTo(byte.Parse(importTemplateElement[0].GetProperty("headerRowIndex").GetString())));

            Assert.That(_bulkImportTemplateRecord.DateFieldIndex, Is.EqualTo(sbyte.Parse(importTemplateElement[0].GetProperty("dateFieldIndex").GetString())));
            Assert.That(_bulkImportTemplateRecord.DescriptionFieldIndex, Is.EqualTo(sbyte.Parse(importTemplateElement[0].GetProperty("descriptionFieldIndex").GetString())));
            Assert.That(_bulkImportTemplateRecord.AmountFieldIndex, Is.EqualTo(sbyte.Parse(importTemplateElement[0].GetProperty("amountFieldIndex").GetString())));
            Assert.That(_bulkImportTemplateRecord.DebitFieldIndex, Is.EqualTo(sbyte.Parse(importTemplateElement[0].GetProperty("debitFieldIndex").GetString())));
            Assert.That(_bulkImportTemplateRecord.CreditFieldIndex, Is.EqualTo(sbyte.Parse(importTemplateElement[0].GetProperty("creditFieldIndex").GetString())));
            
        }

        [Test]
        /*
         *  
         *  When a bulk import template is deleted:
         *  a single record from Import Templates is deleted!
         *  
         */
        public async Task BulkUploadsDeleteBulkImportTemplate_AuthenticatedRequest_DeletesTransactionsBulkUploadRecord()
        {

            // ARRANGE
            // Prepare request
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/DeleteBulkImportTemplate?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { bulkImportTemplateId = _bulkImportTemplateRecord.ImportTemplateName });    // Deleted by Name But 
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Retrieved By Name
            var nullTransactionsBulkUploadRecord = DatabaseHelperForTests.GetBulkImportTemplateRecordByName(_bulkImportTemplateRecord.ImportTemplateName, _userId, _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nullTransactionsBulkUploadRecord, Is.Null);

        }

        [Test]
        [TestCase("TEST_DATA_EMPTY.CSV", 400, "The uploaded file has no content!")]
        [TestCase("TEST_DATA_VALID.txt", 400, "Only .CSV files are allowed!")]
        [TestCase("TEST_DATA_ExtraFileds.CSV", 400, "File has more than 8 columns!")]
        [TestCase("TEST_DATA_FOR_INVALID_FILE_FORMAT.CSV", 400, "All records in this file are NOT in quoted string format!")]
        public async Task BulkUploadsUploadCsvFileAndGetContents_AuthenticatedRequest_ReturnsBadRequestForCSVFileContentsValidationErrors
            ( string csvFileNameThatContainsInvalidContents, int expectedStatusCode, string expectedErrorMessage)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/UploadCsvFileAndGetContents");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            MultipartFormDataContent formData = GetCSVFileAsHeaderContent(csvFileNameThatContainsInvalidContents);
            request.Content = formData;


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

            Assert.That(messageElement.GetString(), Is.EqualTo(expectedErrorMessage));

        }



        [Test]
        [TestCase("TEST_DATA_FOR_DATE_VALIDATION.CSV", 400, "TransactionDate", "Please provide transaction date!")]
        [TestCase("TEST_DATA_FOR_DESCRIPTION_VALIDATION_1.CSV", 400, "TransactionDescription", "Please provide transaction description!")]
        [TestCase("TEST_DATA_FOR_DESCRIPTION_VALIDATION_2.CSV", 400, "TransactionDescription", "Transaction description should not be more than 255 characters long!")]
        [TestCase("TEST_DATA_CATEGORY_ASSIGNMENT_VALIDATION_1.CSV", 400, "TransactionAmount", "For expense type of category, amount should be negative(-) otherwise positive(+).")]  
        [TestCase("TEST_DATA_CATEGORY_ASSIGNMENT_VALIDATION_2.CSV", 400, "TransactionAmount", "For expense type of category, amount should be negative(-) otherwise positive(+).")]  
        // Date format of "mm/dd/yyyy" is not checked for faster upload of data as client side performs this validations...
        public async Task BulkUploadsUploadCsvFileAndGetContents_AuthenticatedRequest_ReturnsBadRequestForCSVFileDataIntegrity
            (string csvFileNameThatContainsInvalidContents, int expectedStatusCode, string failedValidationPropertyName, string expectedErrorMessage)
        {

            // ARRANGE
            if (!_areDummyDataInsertedForCSVBulkUploadTest)
            {
                DatabaseHelperForTests.GenerateGoalAndRulesRecordsForCSVUploadTests(_userId, _bulkImportTemplateRecord.AccountId, _tempConnection);
                _areDummyDataInsertedForCSVBulkUploadTest = true;
            }

            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            // Save the file first!!!
            var requestUploadCSV = new HttpRequestMessage(HttpMethod.Post, "/Transaction/UploadCsvFileAndGetContents");   // As [FromBody] can only accept one parameter

            requestUploadCSV.Headers.Add("Request-Verification-Token", token);
            requestUploadCSV.Headers.Add("Cookie", cookie);

            MultipartFormDataContent formData = GetCSVFileAsHeaderContent(csvFileNameThatContainsInvalidContents);
            requestUploadCSV.Content = formData;

            var responseUploadCSV = await _client.SendAsync(requestUploadCSV);

            if (!responseUploadCSV.IsSuccessStatusCode)
            {
                Assert.That(true, Is.False);    // Fail the test as file not uploaded
                return;
            }

            // NOW SEND A REQUEST TO SAVE THE CSV FILE DATA TO DATABASE!
            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/ImportCsvFileData?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create( _bulkImportTemplateRecord ); 
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
            // 
            // Get validation error message
            Assert.That(root.TryGetProperty("message", out var messageElement), Is.True);
            Assert.That(messageElement[0].TryGetProperty("memberNames", out var memberNamesElement), Is.True);
            Assert.That(messageElement[0].TryGetProperty("errorMessage", out var errorMessageElement), Is.True);

            // MemberNames are not retrieved when the custom attributes have been applied to model member!!!
            if(memberNamesElement.GetArrayLength() > 0)
            {
                Assert.That(memberNamesElement[0].GetString(), Is.EqualTo(failedValidationPropertyName));
            }
            Assert.That(errorMessageElement.GetString(), Is.EqualTo(expectedErrorMessage));

        }


        // Check the uploaded data and template record...When valid data are supplied!!!
        [Test]
        [TestCase("TEST_DATA_VALID_1.CSV")]
        [TestCase("TEST_DATA_VALID_2.CSV")]
        [TestCase("TEST_DATA_VALID_3.CSV")] // Must be last test as cleans up the data!!!

        public async Task BulkUploadsUploadCsvFileAndGetContents_AuthenticatedRequest_ReturnsOkAndInsertsData
            (string csvFileName)
        {

            // ARRANGE
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("", _tempConnection, new string[] { "pft_transactions" });
            if (!_areDummyDataInsertedForCSVBulkUploadTest)
            {
                DatabaseHelperForTests.GenerateGoalAndRulesRecordsForCSVUploadTests(_userId, _bulkImportTemplateRecord.AccountId, _tempConnection);
                _areDummyDataInsertedForCSVBulkUploadTest = true;
            }


            var (token, cookie) = await GetAntiforgeryTokensAsync(_factory, _userId);

            // Save the file first!!!
            var requestUploadCSV = new HttpRequestMessage(HttpMethod.Post, "/Transaction/UploadCsvFileAndGetContents");   // As [FromBody] can only accept one parameter

            requestUploadCSV.Headers.Add("Request-Verification-Token", token);
            requestUploadCSV.Headers.Add("Cookie", cookie);

            MultipartFormDataContent formData = GetCSVFileAsHeaderContent(csvFileName);
            requestUploadCSV.Content = formData;

            var responseUploadCSV = await _client.SendAsync(requestUploadCSV);

            // NOW SEND A REQUEST TO SAVE THE CSV FILE DATA TO DATABASE!
            var request = new HttpRequestMessage(HttpMethod.Post, "/Transaction/ImportCsvFileData?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            _bulkImportTemplateRecord.HeaderRowIndex = 5;
            if(csvFileName == "TEST_DATA_VALID_2.CSV")
            {
                _bulkImportTemplateRecord.AmountFieldIndex = -1;
                _bulkImportTemplateRecord.CreditFieldIndex = 3;
                _bulkImportTemplateRecord.DebitFieldIndex = 4;
            }

            request.Content = JsonContent.Create(_bulkImportTemplateRecord);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            int noOfRecordsOfBusinessCategory = DatabaseHelperForTests.GetNoOfRecordsInsertedInTransactionTable(_userId, _bulkImportTemplateRecord.AccountId, "Business Income", _tempConnection);
            int noOfRecordsOfEducationCategory = DatabaseHelperForTests.GetNoOfRecordsInsertedInTransactionTable(_userId, _bulkImportTemplateRecord.AccountId, "Education", _tempConnection);
            int noOfRecordsOfUncategorizedCategory = DatabaseHelperForTests.GetNoOfRecordsInsertedInTransactionTable(_userId, _bulkImportTemplateRecord.AccountId, "Uncategorized", _tempConnection);

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Verify the success flag returned by the controller
            Assert.That(root.TryGetProperty("success", out var successElement), Is.True);
            Assert.That(successElement.GetBoolean(), Is.True);

            // Validate inserted records...
            Assert.That(noOfRecordsOfBusinessCategory, Is.EqualTo(1));
            Assert.That(noOfRecordsOfEducationCategory, Is.EqualTo(2));
            Assert.That(noOfRecordsOfUncategorizedCategory, Is.EqualTo(3));

            // Clean up dummy data in related tables
            if(csvFileName == "TEST_DATA_VALID_3.CSV")
            {
                DatabaseHelperForTests.CleanupDummyDataInRelatedTables("", _tempConnection, new string[] { "pft_transactions", "pft_transactions_rules", "pft_trans_import", "pft_import_templates", "pft_goals", "pft_accounts" });
            }

        }


        // Prepares the multipart form data request 
        private MultipartFormDataContent GetCSVFileAsHeaderContent(string fileName, string testFileContainerDirectoryName = "TestFiles")
        {

            string testDirectory = TestContext.CurrentContext.TestDirectory;
            testDirectory = testDirectory.Substring(0, testDirectory.IndexOf("PersonalFinanceTrackerTests") + 27);


            string filePath = Path.Combine(testDirectory, testFileContainerDirectoryName, fileName);

            var form = new MultipartFormDataContent();

            // Open a file stream for the test file
            var fileStream = File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

            // Add the file content. 
            // Note: The second parameter ("file") must match the IFormFile parameter name in your controller.
            form.Add(fileContent, "file", fileName);

            return form;

        }

    }

}




