using Dapper;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace PersonalFinanceTracker.Services.Repository
{
    public class GoalRepository : IGoalRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public GoalRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        // Gets the total number of goal records
        public int GetTotalNoOfRecords(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT COUNT(goal_id) FROM pft_goals WHERE user_id = @UserId";

            return _conn.ExecuteScalar<int>(sql, new { UserId = userId });
        }

        // Gets Single Goal Record based on supplied goalId
        public Goal? GetGoalWithDetails(ulong userId, string goalId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);
            ulong ulongGoalId = ulong.Parse(goalId);

            string sql = "SELECT CAST(g.goal_id AS CHAR) AS GoalId, " +
                                "g.goal_type AS GoalType, " +
                                "g.goal_name AS GoalName, " +
                                "g.goal_description AS GoalDescription " +
                                "FROM pft_goals g " +
                                "WHERE g.user_id = @UserId" +
                                "  AND g.goal_id = @GoalId;";

            var goalWithDetails = _conn.QuerySingle<Goal>(sql, new { UserId = userId, GoalId = ulongGoalId });

            sql = "SELECT d.goal_interval AS GoalInterval, " +
                         "d.goal_amount AS GoalAmount, " +
                         "d.goal_period_start_date AS GoalPeriodStartDate, " +
                         "d.goal_period_end_date AS GoalPeriodEndDate " +
                         "FROM pft_goal_details d " +
                         "WHERE d.goal_id = @GoalId " +
                         "ORDER BY d.rec_id;";

            goalWithDetails.GoalDetails = _conn.Query<GoalDetails>(sql, new { GoalId = ulongGoalId });

            return goalWithDetails;
        }

        // Gets the records between a sequence of supplied 'recStartNumber' and 'recEndNumber' (On main page).
        // WOULD HAVE DETAILS AS NULL.
        public IEnumerable<Goal>? GetGoals(ulong userId, int recStartNumber, int  recEndNumber, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);

            return _conn.Query<Goal>("sp_pft_get_goals",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }

        // Saves a goal with all of it's details
        public bool SaveGoalWithDetails(Goal goal, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            if(goal.GoalId == null || goal.GoalId.Trim() == "")
            {
                goal.GoalId = _conn.ExecuteScalar("SELECT CAST(GeneratePrefixedId(7) AS CHAR)").ToString();
            }
            ulong goalId = ulong.Parse(goal.GoalId);
            int total_effected_records = 0;

            // Initialize DynamicParameters for PARENT TABLE...
            var goalParameters = new DynamicParameters();
            goalParameters.Add("goal_id", goalId);
            goalParameters.Add("user_id", goal.UserId);
            goalParameters.Add("goal_type", goal.GoalType);
            goalParameters.Add("goal_name", goal.GoalName);
            goalParameters.Add("goal_description", goal.GoalDescription);
            goalParameters.Add("total_effected_records", direction: ParameterDirection.Output);

            using (var transaction = _conn.BeginTransaction())
            {
                try
                {
                    // Save parent record first!!!
                    _conn.Execute("sp_pft_save_goal",
                                   goalParameters,
                                   commandType: CommandType.StoredProcedure,
                                   transaction: transaction         // MUST BE PROVIDED FOR ALL THE TRANSACTION STATEMENTS!!!
                                  );
                    total_effected_records = goalParameters.Get<int>("total_effected_records");

                    // Delete all the existing records in the details table first!!!
                    if(goal.GoalId != null)
                    {
                        _conn.Execute("DELETE FROM pft_goal_details WHERE goal_id = @GoalId", new { GoalId = goalId }, transaction: transaction);
                    }

                    // Save detail record(s)
                    foreach (var goalDetail in goal.GoalDetails)
                    {
                        // Initialize DynamicParameters for CHILD TABLE...
                        var goalDetailsParameters = new DynamicParameters();
                        goalDetailsParameters.Add("goal_id", goalId);
                        goalDetailsParameters.Add("goal_interval", goalDetail.GoalInterval);
                        goalDetailsParameters.Add("goal_amount", goalDetail.GoalAmount);
                        goalDetailsParameters.Add("goal_period_start_date", goalDetail.GoalPeriodStartDate);
                        goalDetailsParameters.Add("goal_period_end_date", goalDetail.GoalPeriodEndDate);
                        goalDetailsParameters.Add("total_effected_records", direction: ParameterDirection.Output);

                        _conn.Execute("sp_pft_save_goal_details",
                                   goalDetailsParameters,
                                   commandType: CommandType.StoredProcedure,
                                   transaction: transaction         // MUST BE PROVIDED FOR ALL THE TRANSACTION STATEMENTS!!!
                                  );
                        total_effected_records = total_effected_records + goalDetailsParameters.Get<int>("total_effected_records");

                    }

                    transaction.Commit();   // Everything succeeded
                }
                catch (Exception ex)
                {
                    transaction.Rollback(); // Undo all changes
                    total_effected_records = 0;
                }
            }

            return total_effected_records > 0 ? true : false;
        }

        
        // Deletes a Goal by supplied userId and goalId
        public bool DeleteGoal(ulong userId, string goalId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("goal_id", ulong.Parse(goalId));
            parameters.Add("user_id", userId);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_goal",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;

        }


    }
}
