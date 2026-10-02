using Dapper;
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
    public class ReportRepository : IReportRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public ReportRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        // Gets the IncomeExpense, NetWorth, Goal Report records
        public IEnumerable<Report> GetReport(ulong userId, string reportName, DateOnly startDate, DateOnly endDate, byte reportInterval, string? tagId, string connString)
        {

            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string stored_proc_name = "";
            reportName = reportName.ToUpper();

            // IncomeExpense, NetWorth, Goal
            if (reportName == "INCOMEEXPENSE")
            {
                stored_proc_name = reportInterval == 1 ? "sp_pft_report_monthly_income_vs_expense_actual_vs_budget" : "sp_pft_report_total_income_vs_expense_actual_vs_budget";
            }
            else if (reportName == "NETWORTH")
            {
                stored_proc_name = reportInterval == 1 ? "sp_pft_report_monthly_net_worth" : "sp_pft_report_total_net_worth";
            }
            else if (reportName == "GOAL")
            {
                stored_proc_name = reportInterval == 1 ? "sp_pft_report_monthly_goal_vs_actual" : "sp_pft_report_total_goal_vs_actual";
            }

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("start_date", startDate);
            parameters.Add("end_date", endDate);
            if (reportName == "INCOMEEXPENSE")
            {
                parameters.Add("tag_id", tagId);
            }

            return _conn.Query<Report>(stored_proc_name,
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );


        }
    }
}
