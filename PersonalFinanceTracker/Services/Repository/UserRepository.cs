using Dapper;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;
using System.Text.RegularExpressions;

namespace PersonalFinanceTracker.Services.Repository
{
    public class UserRepository : IUserRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public UserRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// This function returns the user log-in and currency details with user_id based on supplied username or email
        /// </summary>
        /// <param name="userNameOrEmail"></param>
        /// <returns></returns>
        public SessionUser? GetUserDetailsForLogIn(string userNameOrEmail, string connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string whereClause = "pu.user_user_name = @UserNameOrEmail";

            if (Regex.IsMatch(userNameOrEmail, APP_CONSTANTS.EMAIL_ADDRESS_VALIDATION_REGEX_PATTERN))
            {
                whereClause = "pu.user_email = @UserNameOrEmail";
            }
            // Fetch user data by username using Dapper
            var sql = "SELECT pu.user_id AS UserId, " +
                      "pu.user_user_name AS UserNameOrEmail, " +
                      "pu.user_password_hash AS Password, " +
                      "pcc.CurrencyCode AS CurrencyCode, " +
                      "pcc.CurrencySymbol AS CurrencySymbol " +
                      "FROM pft_users pu " +
                      "JOIN pft_countries_and_currencies pcc " +
                      "ON pu.user_profile_currency_id =  pcc.Id " +
                      "WHERE " + whereClause;
            var userLogInDetails = _conn.QueryFirstOrDefault<SessionUser>(sql, new { UserNameOrEmail = userNameOrEmail });

            return userLogInDetails;
        }
    }
}
