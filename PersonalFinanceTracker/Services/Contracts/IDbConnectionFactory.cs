using System.Data;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IDbConnectionFactory
    {
        public IDbConnection GetDBConnection(string connectionName = "pft_con_str");
    }
}
