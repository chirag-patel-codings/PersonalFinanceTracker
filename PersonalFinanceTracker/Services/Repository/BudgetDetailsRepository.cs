using Dapper;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Mysqlx;

namespace PersonalFinanceTracker.Services.Repository
{
    public class BudgetDetailsRepository : IBudgetDetailsRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public BudgetDetailsRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        
        public IEnumerable<BudgetDetails>? GetBudgetDetails(ulong userId, DateOnly budgetDetailsStartDate, DateOnly budgetDetailsEndDate, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("start_date", budgetDetailsStartDate.ToString("yyyyMMdd"));
            parameters.Add("end_date", budgetDetailsEndDate.ToString("yyyyMMdd")); 

            return _conn.Query<BudgetDetails>("sp_pft_get_budget_details",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );
        }

        // Updates existing budget detail or creates a new entry!!!
        public bool SaveBudgetDetails(BudgetDetails budget, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("budget_id", ulong.Parse(budget.BudgetId));
            parameters.Add("budget_date_digits", budget.BudgetDateDigits);
            parameters.Add("budget_amount", budget.BudgetAmount);
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_budget_details",
                       parameters,
                       commandType: CommandType.StoredProcedure
                      );

            return parameters.Get<int>("total_effected_records") > 0 ? true : false;
        }

        public bool DeleteBudgetDetails(ulong userId, string budgetId, string budgetDateDigits, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("budget_id", ulong.Parse(budgetId));
            parameters.Add("budget_date_digits", budgetDateDigits);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_budget_detail",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;
        }

    }
}
