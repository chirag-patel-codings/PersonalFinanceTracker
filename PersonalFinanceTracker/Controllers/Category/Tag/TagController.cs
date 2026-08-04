using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PersonalFinanceTracker.Controllers
{
    
    public class TagController : Controller
    {
        private readonly ITagRepository _tagRepository;
        private readonly ulong _currentUserId;
        public TagController(IHttpContextAccessor httpContextAccessor, ITagRepository tagRepository)
        {
            _tagRepository = tagRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
        }


        // Index page!!!
        [Authorize]
        [HttpGet("/categories/tags")]
        public ActionResult Index(string? connString)
        {
            return View("~/Views/Category/Tag/tag.cshtml");
        }


        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...Subsequent after Index page!!
        [Authorize]
        [HttpPost("/categories/tags")]
        [ValidateAntiForgeryToken]
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {
            Pagination pagination;
            IEnumerable<Tag> tags;
            
            if (payload["firstRequest"]?.ToString() == "true")
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _tagRepository.GetTotalNoOfRecords(_currentUserId, connString);
            }
            else
            {
                // Get the inner string from the JSON node, or default to an empty object string
                string jsonString = payload["pagination"]?.ToJsonString() ?? "{}";
                // Deserialize the string into your strongly-typed Pagination object
                pagination = JsonSerializer.Deserialize<Pagination>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            tags = _tagRepository.GetTags(_currentUserId, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);

            return Ok(new { tags, pagination });
        }


        // Gets the specified tag record for display on screen!!!
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetTag([FromBody] dynamic getTagDataFor, string? connString = null)
        {

            string tagId = getTagDataFor.GetProperty("tagId").GetString();
            var tag = _tagRepository.GetTag(_currentUserId, tagId, connString);

            return Ok(tag);
        }

        // Update or Add New Tag!!!
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveTag([FromBody] Tag tag, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!

            tag.UserId = _currentUserId;

            if (ModelState.IsValid)
            {
                if (_tagRepository.SaveTag(tag, connString))
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
        public IActionResult DeleteTag([FromBody] dynamic getTagDataFor, string? connString = null)
        {

            string tagId = getTagDataFor.GetProperty("tagId").GetString();

            if (_tagRepository.DeleteTag(_currentUserId, tagId, connString))
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
