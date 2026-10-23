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
    public class CategoryControllerIntegrationTests
    {
        private CustomWithConfigurationWebApplicationFactory _factory;
        private HttpClient _client;
        private string _userId;
        private string? _uncategorizedCategoryId = null, _businessIncomeCategoryId = null, _educationCategoryId = null;
        private Category? _categoryRecord = null;
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

            _uncategorizedCategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Uncategorized", _tempConnection).ToString();
            _businessIncomeCategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Business Income", _tempConnection).ToString();
            _educationCategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Education", _tempConnection).ToString();

            var categoryRec = DatabaseHelperForTests.GetSingleRecordFromCategoriesTable(_userId, _tempConnection);
            if (categoryRec != null) { 

                _categoryRecord = categoryRec;

            }
            else
            {

                _categoryRecord = new Category
                                {

                                    UserId = ulong.Parse(_userId),
                                    CategoryId = "9223372036854775807",      // MAX BIGINT: 9223372036854775807
                                    StandardCategoryId = _businessIncomeCategoryId,
                                    CategoryType = 1,
                                    CategoryName = "Test Category Name",
                                    CategoryDescription = "Test Category Description",
                                    CategoryColor = "#ffffff",
                                    CategoryDisplayOrder = 150

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
        [TestCase("/categories")]
        public async Task CategoriesAllGetActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
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
        [TestCase("/categories")]
        [TestCase("/Category/GetCategory")]
        [TestCase("/Category/SaveCategory")]
        [TestCase("/Category/DeleteCategory")]
        public async Task CategoriesAllPostActions_UnAuthenticatedRequests_ReturnsUnauthorizedOrBadRequest
            (string actionName)
        {

            var requestPostInValidAuthorize = new HttpRequestMessage(HttpMethod.Post, actionName);
            var requestPostInValidAntiforgeryToken = new HttpRequestMessage(HttpMethod.Post, actionName);

            // requestIndexPostInValidAuthorize
            requestPostInValidAuthorize.Headers.Add("X-Test-Unauthorized", "true");

            // InValidAntiforgeryToken
            var (token, cookie) = await GetAntiforgeryTokensAsync(false);
            requestPostInValidAntiforgeryToken.Headers.Add("Request-Verification-Token", token);   // 'cookie' eliminated as null;

            switch (actionName)
            {

                case "/categories":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { firstRequest = "true", pagination = new { } });
                    break;

                case "/Category/GetCategory":
                case "/Category/DeleteCategory":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { categoryId = _categoryRecord == null ? "9999999999999999999" : _categoryRecord.CategoryId });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { categoryId = _categoryRecord == null ? "9999999999999999999" : _categoryRecord.CategoryId });
                    break;

                case "/Category/SaveCategory":
                    requestPostInValidAuthorize.Content = JsonContent.Create(new { _categoryRecord });
                    requestPostInValidAntiforgeryToken.Content = JsonContent.Create(new { _categoryRecord });
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
        public async Task CategorysIndexGet_AuthenticatedRequest_ReturnsOkAndValidPayload()
        {

            // Act - Sends a real HTTP GET request through Program.cs, middleware, and controllers
            var response = await _client.GetAsync("/categories");

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status code: " + response.StatusCode);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty);
            Assert.That(jsonResponse.Contains("<h3>Categories "), Is.True);   // Contents of category view

        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task CategorysIndexPost_AuthenticatedRequest_ReturnsOkAndValidPayload
            (bool isFirstRequest)
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/categories?connString=pft_con_str_testenv");

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
             * or worry about deserialization exceptions if a property happens to be null (like categoryTypes when firstRequest is false).
             */
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;


            // Verify all three root properties exist in the first request's response   (categories, pagination, standardCategories, categoryTypes)
            Assert.That(root.TryGetProperty("categories", out var categoriesElement), Is.True);
            Assert.That(root.TryGetProperty("pagination", out var paginationElement), Is.True);
            Assert.That(root.TryGetProperty("standardCategories", out var standardCategoriesElement), Is.True);
            Assert.That(root.TryGetProperty("categoryTypes", out var categoryTypesElement), Is.True);
            

            // Assert on specific values inside pagination or categories
            var totalRecords = int.Parse(paginationElement.GetProperty("totalNumberOfRecords").GetString());

            Assert.That(totalRecords, Is.GreaterThanOrEqualTo(0));  // there is a possibility that categories has no data...

            Assert.That(categoriesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(categoriesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));

            if (isFirstRequest)
            {
                // First Request: Check if categories & category types are arrays and has data (if expected)
                Assert.That(standardCategoriesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(standardCategoriesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
                Assert.That(categoryTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(categoryTypesElement.GetArrayLength(), Is.GreaterThanOrEqualTo(0));
            }
            else
            {
                // Consecutive Requests: Check if categories is an array and has data (if expected)
                Assert.That(standardCategoriesElement.ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(categoryTypesElement.ValueKind, Is.EqualTo(JsonValueKind.Null));
            }


        }


        [Test]
        
        // StandardCategoryId
        [TestCase("", null, null, 1, "From_Test_Category_Name", "From_Test_Category_Description", "#ffffff", 201, 400, "StandardCategoryId", "Please provide standard category!")]
        [TestCase(null, null, null, 1, "From_Test_Category_Name", "From_Test_Category_Description", "#ffffff", 201, 400, "StandardCategoryId", "Please provide standard category!")]
        [TestCase("", null, "", 1, "From_Test_Category_Name", "From_Test_Category_Description", "#ffffff", 201, 400, "StandardCategoryId", "Please provide standard category!")]
        [TestCase(null, null, "", 1, "From_Test_Category_Name", "From_Test_Category_Description", "#ffffff", 201, 400, "StandardCategoryId", "Please provide standard category!")]
        
        // CategoryName
        [TestCase("", null, "123", 1, "", "From_Test_Category_Description", "#ffffff", 201, 400, "CategoryName", "Category name is required!")]
        [TestCase(null, null, "123", 1, "", "From_Test_Category_Description", "#ffffff", 201, 400, "CategoryName", "Category name is required!")]
        [TestCase("", null, "123", 1, "     ", "From_Test_Category_Description", "#ffffff", 201, 400, "CategoryName", "Category name is required!")]
        [TestCase(null, null, "123", 1, "     ", "From_Test_Category_Description", "#ffffff", 201, 400, "CategoryName", "Category name is required!")]
        [TestCase("", null, "123", 1, "This Category Name is more than 100 characters long to test the max length for the category name value.", "From_Test_Category_Description", "#ffffff", 201, 400, "CategoryName", "Category name should not be more than 100 characters long!")]
        [TestCase(null, null, "123", 1, "This Category Name is more than 100 characters long to test the max length for the category name value.", "From_Test_Category_Description", "#ffffff", 201, 400, "CategoryName", "Category name should not be more than 100 characters long!")]

        // CategoryDescription
        [TestCase("", null, "123", 1, "From_Test_Category_Name", "This Category Description is more than 255 characters long to test the max length for the category description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah... blah.....", "#ffffff", 201, 400, "CategoryDescription", "Category description should not be more than 255 characters long!")]
        [TestCase(null, null, "123", 1, "From_Test_Category_Name", "This Category Description is more than 255 characters long to test the max length for the category description...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah...blah... blah.....", "#ffffff", 201, 400, "CategoryDescription", "Category description should not be more than 255 characters long!")]

        // CategoryColor
        [TestCase("", null, "123", 1, "From_Test_Category_Name", "From_Test_Category_Description", "#fffffffffffffffffffffffffffffffffffffffffffffffffff", 201, 400, "CategoryColor", "Category color should not be more than 50 characters long!")]
        [TestCase(null, null, "123", 1, "From_Test_Category_Name", "From_Test_Category_Description", "#fffffffffffffffffffffffffffffffffffffffffffffffffff", 201, 400, "CategoryColor", "Category color should not be more than 50 characters long!")]
        
        public async Task CategorysSaveCategory_AuthenticatedRequest_ReturnsBadRequestForModelValidationErrors
            (string? categoryId, ulong? userId, string? standardCategoryId, sbyte categoryType, string categoryName, string? categoryDescription, string? categoryColor, int categoryDisplayOrder, int expectedStatusCode, string failedValidationPropertyName, string? expectedErrorMessage)
        {
            
            // ARRANGE
            standardCategoryId = standardCategoryId == "123" ? _businessIncomeCategoryId : standardCategoryId;  // Default to a valid standard category ID if "123" is passed in the test case

            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Category/SaveCategory?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Category invalidCategory = new Category
            {
                CategoryId = categoryId,
                UserId = userId,
                StandardCategoryId = standardCategoryId,
                CategoryType = categoryType,
                CategoryName = categoryName,
                CategoryDescription = categoryDescription,
                CategoryColor = categoryColor,
                CategoryDisplayOrder = categoryDisplayOrder,
            };

            request.Content = JsonContent.Create(invalidCategory);
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

            // Validate Newly saved record...Replace the existing '_categoryRecord' values
            invalidCategory = DatabaseHelperForTests.GetCategoryRecordByName(invalidCategory.CategoryName, _userId, _tempConnection);
            Assert.That(invalidCategory, Is.Null);

        }

        [Test]
        public async Task CategorysAddCategory_AuthenticatedRequest_ReturnsOkAndCreatesNewRecord()
        {
            
            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Category/SaveCategory?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            // Delete any existing record
            DatabaseHelperForTests.DeleteRecordFromTableById("pft_categories", "category_id", _categoryRecord?.CategoryId, _tempConnection);

            _categoryRecord.CategoryId = "";    // To insert a new record!!!
            _categoryRecord.CategoryName = "Test Category Name";
            _categoryRecord.CategoryDescription = "Test Category Description";
            // _categoryRecord.CategoryDisplayOrder = 150;      // Automatically generated at save...
            request.Content = JsonContent.Create( _categoryRecord );
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

            // Validate Newly saved record...Replace the existing '_categoryRecord' values
            _categoryRecord = DatabaseHelperForTests.GetCategoryRecordByName(_categoryRecord.CategoryName, _userId, _tempConnection);
            Assert.That(_categoryRecord?.CategoryId, Is.Not.Null);

        }


        [Test]
        public async Task CategorysUpdateCategory_AuthenticatedRequest_ReturnsOkAndUpdatesRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Category/SaveCategory?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            Category updatedCategory = new Category();
            updatedCategory.CategoryId = _categoryRecord.CategoryId;
            updatedCategory.StandardCategoryId = _educationCategoryId;
            updatedCategory.CategoryType = -1;
            updatedCategory.CategoryName = _categoryRecord.CategoryName + " - Updated";
            updatedCategory.CategoryDescription = _categoryRecord.CategoryDescription + " - Updated";
            updatedCategory.CategoryColor = "#000000";
            // updatedCategory.CategoryDisplayOrder = 999;  // Automatically generated at save.
            
            request.Content = JsonContent.Create(updatedCategory);
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

            // Validate Newly saved record...Replace the existing '_categoryRecord' values
            _categoryRecord = DatabaseHelperForTests.GetCategoryRecordByName(updatedCategory.CategoryName, _userId, _tempConnection);
            Assert.That(_categoryRecord?.StandardCategoryId, Is.EqualTo(updatedCategory.StandardCategoryId));
            Assert.That(_categoryRecord.CategoryType, Is.EqualTo(updatedCategory.CategoryType));
            Assert.That(_categoryRecord.CategoryName, Is.EqualTo(updatedCategory.CategoryName));
            Assert.That(_categoryRecord?.CategoryDescription, Is.EqualTo(updatedCategory.CategoryDescription));
            Assert.That(_categoryRecord?.CategoryColor, Is.EqualTo(updatedCategory.CategoryColor));
            Assert.That(_categoryRecord.CategoryDisplayOrder, Is.Not.EqualTo(updatedCategory.CategoryDisplayOrder));


        }

        [Test]
        public async Task CategorysGetCategory_AuthenticatedRequest_ReturnsCategoryRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Category/GetCategory?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { categoryId = _categoryRecord?.CategoryId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);

            // Parse the JSON response: return Ok(new { success = true });
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Deserialize the JsonElement into your Category class
            Category? categoryFromRoot = root.Deserialize<Category>(new JsonSerializerOptions
                                                {
                                                    PropertyNameCaseInsensitive = true, // Recommended if JSON keys are camelCase and C# properties are PascalCase
                                                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                                                });

            Assert.That(categoryFromRoot, Is.Not.Null);
            Assert.That(categoryFromRoot.StandardCategoryId, Is.EqualTo(_categoryRecord.StandardCategoryId));
            Assert.That(categoryFromRoot.CategoryType, Is.EqualTo(_categoryRecord.CategoryType));
            Assert.That(categoryFromRoot.CategoryName, Is.EqualTo(_categoryRecord.CategoryName));
            Assert.That(categoryFromRoot.CategoryDescription, Is.EqualTo(_categoryRecord.CategoryDescription));
            Assert.That(categoryFromRoot.CategoryColor, Is.EqualTo(_categoryRecord.CategoryColor));
            Assert.That(categoryFromRoot.CategoryDisplayOrder, Is.EqualTo(_categoryRecord.CategoryDisplayOrder));

        }

        [Test]
        /*
         *  
         *  When a category is deleted:
         *  pft_transactions: category_id = uncategorized_category_id
         *  pft_recurring_schedules: category_id = uncategorized_category_id
         *  pft_transactions_rules: category_id = uncategorized_category_id
         *  pft_budgets_details: records are deleted!
         *  pft_budgets: records are deleted!
         *  pft_categories: records are deleted!
         *  
         */
        public async Task CategorysDeleteCategory_AuthenticatedRequest_DeletesCategoryRecord()
        {

            // ARRANGE
            var (token, cookie) = await GetAntiforgeryTokensAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Category/DeleteCategory?connString=pft_con_str_testenv");   // As [FromBody] can only accept one parameter

            request.Headers.Add("Request-Verification-Token", token);
            request.Headers.Add("Cookie", cookie);

            request.Content = JsonContent.Create(new { categoryId = _categoryRecord.CategoryId });
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // Cleanup Dummy Data in Related Tables:
            DatabaseHelperForTests.CleanupDummyDataInRelatedTables("categories", _tempConnection);

            // Generate dummy data in related tables...pft_transactions, pft_recurring_schedules
            DatabaseHelperForTests.GenerateDummyDataInRelatedTablesForUser("categories", _userId, _tempConnection, _categoryRecord.CategoryId, null);

            int transactionsRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int recurringSchedulesRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int transactionsRepeatRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_trasactions_repeats", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int transactionsRuleRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int budgetRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_budgets", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int budgetDetailsRecordsCountsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_budgets_details", "category_id", _categoryRecord.CategoryId, _tempConnection);
            
            string uncategorizedCategoryId = DatabaseHelperForTests.GetCategoryIdForUser(_userId, "Uncategorized", _tempConnection).ToString();
            int uncategorizedCategoryRecordsCountsInTransactionsBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "category_id", uncategorizedCategoryId, _tempConnection);
            int uncategorizedCategoryRecordsCountsInRecurringSchedulesBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "category_id", uncategorizedCategoryId, _tempConnection);
            int uncategorizedCategoryRecordsCountsInTransactionsRuleBeforeDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "category_id", uncategorizedCategoryId, _tempConnection);


            // ACT
            var response = await _client.SendAsync(request);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var nullCategoryRecord = DatabaseHelperForTests.GetCategoryRecordByName(_categoryRecord.CategoryName, _userId, _tempConnection);

            int transactionsRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int recurringSchedulesRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int transactionsRepeatRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_trasactions_repeats", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int transactionsRuleRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int budgetRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_budgets", "category_id", _categoryRecord.CategoryId, _tempConnection);
            int budgetDetailsRecordsCountsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_budgets_details", "category_id", _categoryRecord.CategoryId, _tempConnection);

            int uncategorizedCategoryRecordsCountsInTransactionsAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions", "category_id", uncategorizedCategoryId, _tempConnection);
            int uncategorizedCategoryRecordsCountsInRecurringSchedulesAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_recurring_schedules", "category_id", uncategorizedCategoryId, _tempConnection);
            int uncategorizedCategoryRecordsCountsInTransactionsRuleAfterDelete = DatabaseHelperForTests.GetRecordsCountForQueriedFieldInTable(_userId, "pft_transactions_rules", "category_id", uncategorizedCategoryId, _tempConnection);


            // ASSERT
            Assert.That(transactionsRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(recurringSchedulesRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(transactionsRepeatRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(transactionsRuleRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(budgetRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(budgetDetailsRecordsCountsBeforeDelete, Is.GreaterThanOrEqualTo(1));

            Assert.That(uncategorizedCategoryRecordsCountsInTransactionsBeforeDelete, Is.EqualTo(0));
            Assert.That(uncategorizedCategoryRecordsCountsInRecurringSchedulesBeforeDelete, Is.EqualTo(0));
            Assert.That(uncategorizedCategoryRecordsCountsInTransactionsRuleBeforeDelete, Is.EqualTo(0));

            Assert.That(response.IsSuccessStatusCode, Is.True, "The request failed with status: " + response.StatusCode);
            Assert.That(nullCategoryRecord, Is.Null);

            Assert.That(transactionsRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(recurringSchedulesRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(transactionsRepeatRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(transactionsRuleRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(budgetRecordsCountsAfterDelete, Is.EqualTo(0));
            Assert.That(budgetDetailsRecordsCountsAfterDelete, Is.EqualTo(0));

            Assert.That(uncategorizedCategoryRecordsCountsInTransactionsAfterDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(uncategorizedCategoryRecordsCountsInRecurringSchedulesAfterDelete, Is.GreaterThanOrEqualTo(1));
            Assert.That(uncategorizedCategoryRecordsCountsInTransactionsRuleAfterDelete, Is.GreaterThanOrEqualTo(1));

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




