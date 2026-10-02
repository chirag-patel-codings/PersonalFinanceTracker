using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IReportRepository
    {
        public IEnumerable<Report> GetReport(ulong userId, string reportName, DateOnly startDate, DateOnly endDate, byte reportInterval, string? tagId, string connString);

    }
}
