using Dapper;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.Data;

namespace PersonalFinanceTracker.Services.Repository
{
    
    public class UserRegistrationRepository : IUserRegistrationRepository
    {
        private readonly IDbConnection _conn;
        private readonly IDbConnectionFactory _factory;
        private readonly IEmailService _emailService;
        public UserRegistrationRepository (IDbConnectionFactory factory, IEmailService emailService)
        {
            _factory = factory;
            _emailService = emailService;
        }


        /// <summary>
        /// Adds the new user registration to the database and generates default records for 'categories' and 'budget_rollover_settings'
        /// and returns the no of records insertion count.
        /// </summary>
        /// <param name="userRegistration"></param>
        public bool CreateNewUser(UserRegistration userRegistration, string connString)
        {
            
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);
            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_first_name", userRegistration.FirstName.Trim());
            parameters.Add("user_last_name", userRegistration.LastName.Trim());
            parameters.Add("user_user_name", userRegistration.UserName.Trim());
            parameters.Add("user_password_hash", userRegistration.PasswordHash);
            parameters.Add("user_email", userRegistration.Email.Trim());
            parameters.Add("user_email_verification_token_hash", userRegistration.userEmailTokenHash);
            parameters.Add("total_inserted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_create_new_user",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_inserted_records") > 0;

        }

        /// <summary>
        /// This function checks if the user with the same email alredy exists.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public bool UserWithEmailAlreadyExists(string email, string connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT COUNT(user_email) FROM pft_users WHERE user_email = @user_email";
            int existingEmailCount = _conn.ExecuteScalar<int>(sql, new { user_email = email });

            return existingEmailCount > 0 ? true : false;
        }

        /// <summary>
        /// This function checks if the user with the same username alredy exists.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public bool UserWithSameUserNameExists(string userName, string connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);
            string sql = "SELECT COUNT(user_user_name) FROM pft_users WHERE user_user_name = @user_name";
            int existingEmailCount = _conn.ExecuteScalar<int>(sql, new { user_name = userName });

            return existingEmailCount > 0 ? true : false;
        }


        public int UpdateUserEmailVarification(string userEmailTokenHash, string connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            var parameters = new DynamicParameters();
            parameters.Add("user_email_token_hash", _emailService.GenerateHashFromToken(userEmailTokenHash));
            parameters.Add("total_updated_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_update_user_email_verification",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_updated_records");
        }

        
    }
}
