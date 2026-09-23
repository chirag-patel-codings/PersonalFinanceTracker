using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.CSVUpload;
using PersonalFinanceTracker.Services.Repository;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Transactions;

using TransactionDataModel = PersonalFinanceTracker.Models.Transaction;

namespace PersonalFinanceTracker.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ITransactionRepository _transactionRepository;

        private readonly IBulkImportTemplateRepository _bulkImportTemplateRepository;
        private readonly ulong _currentUserId;
        private readonly string _userCurrencyCode, _userCurrencySymbol;
        public TransactionController(IHttpContextAccessor httpContextAccessor, ITransactionRepository transactionRepository, IBulkImportTemplateRepository bulkImportTemplateRepository)
        {

            _transactionRepository = transactionRepository;
            _bulkImportTemplateRepository = bulkImportTemplateRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
            _userCurrencyCode = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencyCode");
            _userCurrencySymbol = httpContextAccessor.HttpContext?.User.FindFirstValue("CurrencySymbol");

        }

#region TRANSACTIONS

        [Authorize]
        [HttpGet("/transactions")]
        // Index page!!!
        public IActionResult Index(string? connString)
        {

            return View("Transaction");

        }


        [Authorize]
        [HttpPost("transactions")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {

            Pagination pagination;
            IEnumerable<TransactionDataModel> transactions;
            IEnumerable<ListOptionStringId>? categoriesList = null;
            IEnumerable<ListOptionStringId>? accountsList = null;
            IEnumerable<ListOptionStringId>? tagsList = null;
            IEnumerable<ListOptionStringId>? goalsList = null;
            IEnumerable<ListOption>? transactionCategorizationList = null;
            IEnumerable<ListOption>? transactionTypeList = null;
            IEnumerable<ListOption>? transactionRepeatIntervalList = null;

            // Get the inner string from the JSON node, or default to an empty object string
            string transactionFilterString = payload["transactionFilter"]?.ToJsonString() ?? "{}";
            TransactionFilter transactionFilter = JsonSerializer.Deserialize<TransactionFilter>(transactionFilterString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var userCurrencyDetails = new { CurrencyCode = _userCurrencyCode, CurrencySymbol = _userCurrencySymbol };

            bool isFirstRequest = payload["firstRequest"]?.ToString() == "true";

            if (isFirstRequest)
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _transactionRepository.GetTotalNoOfRecords(_currentUserId, transactionFilter, connString);

                categoriesList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "categories", connString);
                accountsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "accounts", connString);
                tagsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "tags", connString);
                goalsList = _transactionRepository.GetSelectOptionsDataFor(_currentUserId, "goals", connString);

                transactionCategorizationList = new List<ListOption>()
                                                {
                                                    new ListOption(0, "All"),
                                                    new ListOption(1, "Manual"),    // 1 = manual, 2 = CSV upload, 3 = auto (Bank)
                                                    new ListOption(3, "Auto"),
                                                };
                transactionTypeList = new List<ListOption>()
                                                {
                                                    new ListOption(0, "All"),
                                                    new ListOption(2, "Recurring")
                                                };
                transactionRepeatIntervalList = new List<ListOption>()
                                                {
                                                    new ListOption(1, "Weekly"),
                                                    new ListOption(2, "BiWeekly"),
                                                    new ListOption(3, "Monthly"),
                                                    new ListOption(4, "BiMonthly"),
                                                    new ListOption(5, "Quarterly"),
                                                    new ListOption(6, "Half-Yearly"),
                                                    new ListOption(7, "Yearly")
                                                };

            }
            else
            {

                // Get the inner string from the JSON node, or default to an empty object string
                string jsonString = payload["pagination"]?.ToJsonString() ?? "{}";

                // Deserialize the string into your strongly-typed Pagination object
                pagination = JsonSerializer.Deserialize<Pagination>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            }

            transactions = _transactionRepository.GetTransactions(_currentUserId, transactionFilter, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);


            if (isFirstRequest)
            {
                return Ok(new { transactions, pagination, categoriesList, accountsList, tagsList, goalsList, transactionCategorizationList, transactionTypeList, transactionRepeatIntervalList, userCurrencyDetails });
            }
            else
            {
                return Ok(new { transactions, pagination });
            }

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified transaction record for display on screen!!!
        public IActionResult GetTransaction([FromBody] dynamic getTransactionDataFor, string? connString = null)
        {

            string transactionId = getTransactionDataFor.GetProperty("transactionId").GetString();
            var transaction = _transactionRepository.GetTransaction(_currentUserId, transactionId, connString);

            return Ok(transaction);

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Transaction!!!
        public IActionResult SaveTransaction([FromBody] TransactionDataModel transaction, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!

            transaction.UserId = _currentUserId;

            if (ModelState.IsValid)
            {
                if (_transactionRepository.SaveTransaction(transaction, connString))
                {
                    return Ok(new { success = true });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Database error occurred." });
                }
            }
            else
            {
                return BadRequest(new { success = false, message = ModelState.Where(ms => ms.Value.Errors.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()) } );
            }

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteTransaction([FromBody] dynamic deleteTransactionDataFor, string? connString = null)
        {

            string transactionId = deleteTransactionDataFor.GetProperty("transactionId").GetString();

            if (_transactionRepository.DeleteTransaction(_currentUserId, transactionId, connString))
            {
                return Ok(new { success = true });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Database error occurred." });  // record may not be found!!!
            }

        }

#endregion TRANSACTIONS


#region BULKUPLOAD

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Saves the csv file to the server and retruns the data to client for validations!
        public IActionResult UploadCsvFileAndGetContents(IFormFile file)
        {

            if (file == null || file.Length == 0)
                return StatusCode(400, new { success = false, message = "The uploaded file has no content!" });


            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return StatusCode(400, new { success = false, message = "Only .CSV files are allowed!" });


            CSVFileService.UploadCSVFile(_currentUserId, file);

            var csvContents = CSVFileService.GetCSVFileContent(_currentUserId);

            if (csvContents.contents == null)
            {
                return StatusCode(400, new { success = false, message = csvContents.errorMessage });
            }
            else
            {
                return Ok(csvContents.contents);
            }

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Saves the csv file to the server and retruns the data to client for validations!
        public IActionResult ImportCsvFileData([FromBody] BulkImportTemplate bulkImportTemplate, string? connString = null)
        {
            
            string transctionAmount = "";
            
            var csvContents = CSVFileService.GetCSVFileContent(_currentUserId); // Returns arrays of string for each line..
            var defaultCategoryIdForUser = _transactionRepository.GetDefaultCateogryId(_currentUserId, connString).ToString();
            var validationErrorsOrTransactions =  _transactionRepository.GenerateTransactionRecordsWithRulesApplied(_currentUserId, bulkImportTemplate, csvContents.contents, defaultCategoryIdForUser, 2, connString);

            if (validationErrorsOrTransactions.transactions != null)    // If the data returned
            {
                bool result = _transactionRepository.BulkUploadTransactions(_currentUserId, (IEnumerable<TransactionDataModel>)validationErrorsOrTransactions.transactions, connString);
                return Ok(new { success = result });
            }
            else
            {
                // Extract Record Details...
                int validationErrorsOrTransactionsLastItemIndex = validationErrorsOrTransactions.modelValidationResults.Count - 1;
                var recordDetails = validationErrorsOrTransactions.modelValidationResults.ElementAt(validationErrorsOrTransactionsLastItemIndex);
                validationErrorsOrTransactions.modelValidationResults.RemoveAt(validationErrorsOrTransactionsLastItemIndex);


                return BadRequest(new { success = false, recordDetails = recordDetails.ErrorMessage, message = validationErrorsOrTransactions.modelValidationResults });
            }
    
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified bulk import template record for display on screen!!!
        public IActionResult GetBulkImportTemplate([FromBody] dynamic getBulkImportTemplateDataFor, string? connString = null)
        {

            string importTemplateName = getBulkImportTemplateDataFor.GetProperty("importTemplateName").GetString();
            var importTemplate = _bulkImportTemplateRepository.GetBulkImportTemplate(_currentUserId, importTemplateName, connString);

            return Ok(new { importTemplate });

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets all the Bulk Import Templates for the current user from a 'Post' request...
        public IActionResult GetBulkImportTemplateNames(string? connString)
        {

            var bulkImportTemplateNames = _bulkImportTemplateRepository.GetBulkImportTemplateNames(_currentUserId, connString);

            // Wrap it in an object so it serializes as { "bulkImportTemplates": [...] }
            return Ok(new { bulkImportTemplateNames });

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Transaction!!!
        public IActionResult SaveBulkImportTemplate([FromBody] BulkImportTemplate bulkImportTemplate, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!

            bulkImportTemplate.UserId = _currentUserId;

            if (ModelState.IsValid)
            {
                if (_bulkImportTemplateRepository.SaveBulkImportTemplate(bulkImportTemplate, connString))
                {
                    return Ok(new { success = true });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Database error occurred." });
                }
            }
            else
            {
                return BadRequest(new { success = false, message = ModelState.Where(ms => ms.Value.Errors.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()) });
            }

        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteBulkImportTemplate([FromBody] dynamic deleteBulkImportTemplateDataFor, string? connString = null)
        {

            string bulkImportTemplateId = deleteBulkImportTemplateDataFor.GetProperty("bulkImportTemplateId").GetString();

            if (_bulkImportTemplateRepository.DeleteBulkImportTemplate(_currentUserId, bulkImportTemplateId, connString))
            {
                return Ok(new { success = true });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Database error occurred." });  // record may not be found!!!
            }

        }


#endregion BULKUPLOAD

    }
}
