using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace PersonalFinanceTrackerTests
{

    // This class should help to work in database for TESTS only.
    // This class would not have it's own database connection object. It would be passed through the test function...
    static class DatabaseHelperForTests
    {

        // Deletes a newly created user in test by username and useremail
        public static void DeleteNewUserByUserNameAndEmail(string userName, string userEmail, IDbConnection _conn)
        {
            
            var parameters = new DynamicParameters();
            parameters.Add("user_name", userName);
            parameters.Add("user_email", userEmail);

            _conn.Execute("sp_pft_tests_delete_new_user_by_username_email",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }

        // gets the number of records in the database by username and useremail
        public static int GetUserCountsByUserNameAndEmail(string userName, string userEmail, IDbConnection _conn)
        {

            string sql = "SELECT COUNT(user_user_name) FROM pft_users WHERE user_user_name = @UserName AND user_email = @UserEmail";

            // Get this value and then delete the record!!!
            return _conn.ExecuteScalar<int>(sql, new
            {
                UserName = userName,
                UserEmail = userEmail
            });

        }

    }
}
