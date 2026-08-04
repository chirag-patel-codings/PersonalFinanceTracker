using Microsoft.AspNetCore.Connections;
using MySql.Data.MySqlClient;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;

namespace PersonalFinanceTracker.Services.Repository
{
    public class DbConnectionFactory : IDbConnectionFactory, IDisposable
    {
        private readonly IConfiguration _config;
        private IDbConnection _activeConnection;

        public DbConnectionFactory(IConfiguration config)
        {
            _config = config;
        }

        public IDbConnection GetDBConnection(string connectionName = "pft_con_str")
        {

            // Return existing connection if already created in this request
            if (_activeConnection != null && _activeConnection.State == ConnectionState.Open) return _activeConnection;

            var connStr = _config.GetConnectionString(connectionName);
            _activeConnection = new MySqlConnection(connStr);
            _activeConnection.Open();
            return _activeConnection;
        }

        public void Dispose()
        {
            if (_activeConnection != null && _activeConnection.State == ConnectionState.Open)
            {
                _activeConnection.Close();
            }
            // Called automatically by .NET DI when the Scoped lifetime ends
            _activeConnection?.Dispose();
        }
    }
}
