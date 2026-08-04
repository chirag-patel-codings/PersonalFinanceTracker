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
    public class GoalController : Controller
    {
        private readonly IGoalRepository _goalRepository;
        private readonly ulong _currentUserId;
        public GoalController(IHttpContextAccessor httpContextAccessor, IGoalRepository goalRepository) 
        { 
            _goalRepository = goalRepository;
            _currentUserId = ulong.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
        }


        [Authorize]
        [HttpGet("/goals")]
        // Index page!!!
        public IActionResult Index(string? connString)
        {
            return View("Goal");
        }


        [Authorize]
        [HttpPost("goals")]
        [ValidateAntiForgeryToken]
        // Returns the records between the RecordStartNumber and RecordEndNumber from a 'Post' request...Subsequent after Index page!!1
        public IActionResult Index([FromBody] JsonObject payload, string? connString)
        {
            Pagination pagination;
            IEnumerable<Goal> goals;
            IEnumerable<GoalListOption>? goalTypes = null;
            IEnumerable<GoalListOption> goalIntervalTypes = null;

            if (payload["firstRequest"]?.ToString() == "true")
            {
                pagination = new Pagination();
                pagination.TotalNumberOfRecords = _goalRepository.GetTotalNoOfRecords(_currentUserId, connString);

                // Savings = 1, Reduce Debt = 2, Investing = 3, Tax Planning = 4, Other = 5
                goalTypes = new List<GoalListOption>()
                                        {
                                            new GoalListOption(1, "Savings"),
                                            new GoalListOption(2, "Reduce Debt"),
                                            new GoalListOption(3, "Investing"),
                                            new GoalListOption(4, "Tax Planning"),
                                            new GoalListOption(5, "Other")
                                         };

                // Weekly - 1, BiWeekly - 2, Monthly – 3, Quarterly – 4,  Half - Yearly – 5 , Yearly - 6
                goalIntervalTypes = new List<GoalListOption>()
                                        {
                                            new GoalListOption(1, "Weekly"),
                                            new GoalListOption(2, "BiWeekly"),
                                            new GoalListOption(3, "Monthly"),
                                            new GoalListOption(4, "Quarterly"),
                                            new GoalListOption(5, "Half - Yearly"),
                                            new GoalListOption(6, "Yearly")
                                        };
            }
            else
            {

                // Get the inner string from the JSON node, or default to an empty object string
                string jsonString = payload["pagination"]?.ToJsonString() ?? "{}";

                // Deserialize the string into your strongly-typed Pagination object
                pagination = JsonSerializer.Deserialize<Pagination>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            }

            goals = _goalRepository.GetGoals(_currentUserId, pagination.RecordStartNumber, pagination.RecordEndNumber, connString);

            return Ok(new { goals, pagination, goalTypes, goalIntervalTypes });
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Gets the specified goal record for display on screen!!!
        public IActionResult GetGoal([FromBody] dynamic getGoalDataFor, string? connString = null)
        {

            string goalId = getGoalDataFor.GetProperty("goalId").GetString();
            var goal = _goalRepository.GetGoalWithDetails(_currentUserId, goalId, connString);
            
            return Ok(goal);
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Update or Add New Goal With Details!!!
        public IActionResult SaveGoal([FromBody] Goal goal, string? connString = null)
        {
            // PAGINATION  TO BE  DONE  FROM UI AFTER INSERT/SAVE SUCCESS!!! NOT BY THIS CONTROLLER ACTION!!!
            
            goal.UserId = _currentUserId;
            
            if (ModelState.IsValid)
            {
                if(_goalRepository.SaveGoalWithDetails(goal, connString))
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
        public IActionResult DeleteGoal([FromBody] dynamic deleteGoalDataFor, string? connString = null)
        {
            string goalId = deleteGoalDataFor.GetProperty("goalId").GetString();

            if (_goalRepository.DeleteGoal(_currentUserId, goalId, connString))
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
