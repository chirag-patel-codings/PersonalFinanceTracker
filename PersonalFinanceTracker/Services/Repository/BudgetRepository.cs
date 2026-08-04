using Dapper;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Mysqlx;

namespace PersonalFinanceTracker.Services.Repository
{
    public class BudgetRepository : IBudgetRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public BudgetRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        
        public IEnumerable<Budget>? GetBudgetSettings(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            
            return _conn.Query<Budget>("sp_pft_get_budget_settings",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );
        }

        // Updates existing budget detail or creates a new entry!!!
        public bool SaveBudgetSettings(Budget budget, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", budget.UserId);
            parameters.Add("category_id", ulong.Parse(budget.CategoryId));
            parameters.Add("br_to_be_rolled_over", budget.BudgetToBeRolledOver);
            parameters.Add("br_if_under_setting", budget.BudgetIfUnderSettings);
            parameters.Add("br_if_over_setting", budget.BudgetIfOverSettings);
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_budget_setting",
                       parameters,
                       commandType: CommandType.StoredProcedure
                      );

            return parameters.Get<int>("total_effected_records") > 0 ? true : false;
        }

    }
}
