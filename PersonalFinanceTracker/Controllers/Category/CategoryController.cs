using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PersonalFinanceTracker.Controllers
{
    public class CategoryController : Controller
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ulong _currentUserId;
        public CategoryController(IHttpContextAccessor httpContextAccessor, ICategoryRepository categoryRepository) 
        { 
            _categoryRepository = categoryRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
        }


        [Authorize]
        [HttpGet("/categories")]
        // Index page!!!
        public IActionResult Index(string? connString)
        {
            return View("Category");
        }


        [Authorize]
        [HttpPost("categories")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...Subsequent after Index page!!1
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {
            Pagination pagination;
            IEnumerable<Category> categories;
            IEnumerable<StandardCategories>? standardCategories = null;
            IEnumerable<CategoryType>? categoryTypes = null;

            if (payload["firstRequest"]?.ToString() == "true")
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _categoryRepository.GetTotalNoOfRecords(_currentUserId, connString);
                
                standardCategories = _categoryRepository.GetStandardCategories(_currentUserId, connString);
                categoryTypes = new List<CategoryType>()
                                        {
                                            new CategoryType(1, "Income"),
                                            new CategoryType(-1, "Expense"),
                                            new CategoryType(0, "Permanent")
                                         };
            }
            else
            {

                // Get the inner string from the JSON node, or default to an empty object string
                string jsonString = payload["pagination"]?.ToJsonString() ?? "{}";

                // Deserialize the string into your strongly-typed Pagination object
                pagination = JsonSerializer.Deserialize<Pagination>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            }

            categories = _categoryRepository.GetCategories(_currentUserId, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);

            return Ok(new { categories, pagination, standardCategories, categoryTypes });
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified category record for display on screen!!!
        public IActionResult GetCategory([FromBody] dynamic getCategoryDataFor, string? connString = null)
        {

            string categoryId = getCategoryDataFor.GetProperty("categoryId").GetString();
            var category = _categoryRepository.GetCategory(_currentUserId, categoryId, connString);
            
            return Ok(category);
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Category!!!
        public IActionResult SaveCategory([FromBody] Category category, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!
            
            category.UserId = _currentUserId;
            
            if (ModelState.IsValid)
            {
                if(_categoryRepository.SaveCategory(category, connString))
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
                return BadRequest(new { success = false, message = "Data Validation Error." });
            }

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory([FromBody] dynamic deleteCategoryDataFor, string? connString = null)
        {
            string categoryId = deleteCategoryDataFor.GetProperty("categoryId").GetString();

            if (_categoryRepository.DeleteCategory(_currentUserId, categoryId, connString))
            {
                return Ok(new { success = true });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Database error occurred." });  // record may not be found!!!
            }

        }
    }
}
