using Dapper;
using Mysqlx;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Handler;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace PersonalFinanceTrackerTests
{

    // This class should help to work in database for TESTS only.
    // This class would not have it's own database connection object. It would be passed through the test function...
    static class DatabaseHelperForTests
    {

        // Import 'DateOnlyTypeHandler' from 'Development' Project - Start

        [ModuleInitializer]
        public static void Initialize()
        {
            // This registers the handler automatically when the test project starts
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        }

        // Import 'DateOnlyTypeHandler' from 'Development' Project - End

        // Deletes a record from a table by recordId
        public static void DeleteRecordFromTableById(string tableName, string fieldName, string recordId, IDbConnection _conn, string? childTableName = null, string? childFieldName = null)
        {
            string sql = "";
            if (tableName == "pft_goals")
            {
                sql = $"SET FOREIGN_KEY_CHECKS = 0; DELETE FROM {childTableName} WHERE {childFieldName} = @RecordId; DELETE FROM {tableName} WHERE {fieldName} = @RecordId; SET FOREIGN_KEY_CHECKS = 1;";
            }
            else
            {
                sql = $"SET FOREIGN_KEY_CHECKS = 0; DELETE FROM {tableName} WHERE {fieldName} = @RecordId; SET FOREIGN_KEY_CHECKS = 1;";
            }
            

            _conn.Execute(sql, new
            {
                RecordId = ulong.Parse(recordId)
            });
        }

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

        // Gets the user_id of a user in test by username and useremail
        public static ulong GetUserIdByUserNameAndEmail(string userName, string userEmail, IDbConnection _conn)
        {
            string sql = "SELECT user_id FROM pft_users WHERE user_user_name = @UserName AND user_email = @UserEmail";

            // Get this value and then delete the record!!!
            return _conn.ExecuteScalar<ulong>(sql, new
            {
                UserName = userName,
                UserEmail = userEmail
            });
        }

        // INSERT DUMMY RECORD IN RELATED TABLES - START
        // Accounts, Category, Goal, Tags
        public static void GenerateDummyDataInRelatedTablesForUser(string forTestName, string userId, IDbConnection _conn, string? testRecIdInBaseTable = null, ulong? standardCategoryId = null)
        {

            string newAccountId = null;
            string newCategoryId = null;
            string? newTagId = null;
            string? newGoalId = null;
            string? newBudgetId = null;
            string newTransactionId; 
            string newTransactionRepeatId;
            string newRecurringScheduleId;
            string newTransactionRuleId;
            string newImportTemplateId;
            string sql = "";

            
            switch (forTestName.ToLower())
            {

                case "accounts":

                    newAccountId = testRecIdInBaseTable;
                    newCategoryId = GetRecIdByName("pft_categories", "category_id", "category_name", "Business Income", userId, _conn).ToString();

                    sql = GetInsertDummyTransctionRuleRecordAndRetrieveIdSQL(userId, newAccountId, newCategoryId, newGoalId);
                    newTransactionRuleId = ExecuteSQLAndReturnId(sql, _conn).ToString();
                    
                    sql = GetInsertDummyImportTemplateRecordAndRetrieveIdSQL(userId, newAccountId);
                    newImportTemplateId = ExecuteSQLAndReturnId(sql, _conn).ToString();

                    break;

                case "categories":
                    
                    sql = GetInsertDummyAccountRecordAndRetrieveIdSQL(userId);
                    newAccountId = ExecuteSQLAndReturnId(sql, _conn).ToString();

                    newCategoryId = testRecIdInBaseTable;
                    
                    sql = GetInsertDummyTransctionRuleRecordAndRetrieveIdSQL(userId, newAccountId, newCategoryId, newGoalId);
                    newTransactionRuleId = ExecuteSQLAndReturnId(sql, _conn).ToString();

                    // pft_budgets would already have a new record added when a new category is generated...
                    newBudgetId = GetRecIdByName("pft_budgets", "budget_id", "category_id", testRecIdInBaseTable, userId, _conn).ToString();
                    
                    // Insert a new record in pft_budget_details
                    sql = GetInsertDummyBudgetDetailsRecordAndRetrieveIdSQL(newBudgetId);
                    newBudgetId = ExecuteSQLAndReturnId(sql, _conn).ToString();

                    break;

                case "tags":

                    sql = GetInsertDummyAccountRecordAndRetrieveIdSQL(userId);
                    newAccountId = ExecuteSQLAndReturnId(sql, _conn).ToString();

                    newCategoryId = GetRecIdByName("pft_categories", "category_id", "category_name", "Business Income", userId, _conn).ToString();
                    newTagId = testRecIdInBaseTable;
                    break;

                case "goals":

                    sql = GetInsertDummyAccountRecordAndRetrieveIdSQL(userId);
                    newAccountId = ExecuteSQLAndReturnId(sql, _conn).ToString();

                    newCategoryId = GetRecIdByName("pft_categories", "category_id", "category_name", "Business Income", userId, _conn).ToString();
                    newGoalId = testRecIdInBaseTable;

                    sql = GetInsertDummyTransctionRuleRecordAndRetrieveIdSQL(userId, newAccountId, newCategoryId, newGoalId);
                    newTransactionRuleId = ExecuteSQLAndReturnId(sql, _conn).ToString();

                    break;

            }

            // Common tables...

            sql = GetInsertDummyTransactionRecordAndRetrieveIdSQL(userId, newAccountId, newCategoryId, newGoalId, newTagId);
            newTransactionId = ExecuteSQLAndReturnId(sql, _conn).ToString();

            sql = GetInsertDummyTransactionRepeatsRecordAndRetrieveIdSQL(userId, newTransactionId);
            newTransactionRepeatId = ExecuteSQLAndReturnId(sql, _conn).ToString();

            sql = GetInsertDummyRecurringScheuduleRecordAndRetrieveIdSQL(newTransactionRepeatId, 250, newAccountId, newCategoryId, newGoalId, newTagId);
            newRecurringScheduleId = ExecuteSQLAndReturnId(sql, _conn).ToString();

        }

        // Returns the no of records count for the specified value of the specified the specified field in the specified table...
        public static int GetRecordsCountForQueriedFieldInTable(string userId, string tableName, string searchFieldName, string searchValue, IDbConnection _conn)
        {
            string sql = "";

            switch (tableName)
            {

                case "pft_accounts":
                case "pft_categories":
                case "pft_tags":
                case "pft_goals":
                case "pft_transactions":
                case "pft_transactions_rules":
                case "pft_budgets":
                case "pft_import_templates":

                    sql = $"SELECT COUNT(*) FROM {tableName} WHERE user_id = '{userId}' AND {searchFieldName} = '{searchValue}'";
                    break;

                case "pft_budgets_details":

                    sql = $"SELECT COUNT(*) FROM {tableName} bd JOIN pft_budgets b on b.budget_id = bd.budget_id WHERE b.user_id = '{userId}' AND {searchFieldName} = '{searchValue}'";
                    break;

                case "pft_goal_details":

                    sql = $"SELECT COUNT(*) FROM {tableName} gd JOIN pft_goals g ON g.goal_id = gd.goal_id WHERE g.user_id = '{userId}' AND gd.{searchFieldName} = '{searchValue}'";
                    break;

                case "pft_recurring_schedules":

                    sql = $"SELECT COUNT(*) FROM {tableName} rs JOIN pft_trasactions_repeats tr ON tr.transaction_repeat_id = rs.transaction_repeat_id WHERE tr.user_id = '{userId}' AND rs.{searchFieldName} = '{searchValue}'";
                    break;

                case "pft_trasactions_repeats":

                    sql = $"SELECT COUNT(*) FROM {tableName} tr JOIN pft_recurring_schedules rs ON tr.transaction_repeat_id = rs.transaction_repeat_id WHERE tr.user_id = '{userId}' AND rs.{searchFieldName} = '{searchValue}'";
                    break;

            }           

            return _conn.ExecuteScalar<int>(sql);
            
        }

        // Prepares a delete statement for multiple tables
        private static string GetDeleteStatementsForSuppliedTables(params string[] tableNames)
        {

            string sql = "";
            foreach (var tableName in tableNames)
            {
                sql = $"{sql}DELETE FROM {tableName}; ";
            }

            return sql;

        }

        // Performs straight off delete without any conditional checking!
        public static void CleanupDummyDataInRelatedTables(string testName, IDbConnection _conn)
        {
            string sql = "";
            switch (testName)
            {
                case "tags":

                    sql = GetDeleteStatementsForSuppliedTables("pft_transactions", "pft_recurring_schedules", "pft_trasactions_repeats", "pft_accounts");
                    break;

                case "goals":

                    sql = GetDeleteStatementsForSuppliedTables("pft_transactions", "pft_recurring_schedules", "pft_trasactions_repeats", "pft_transactions_rules", "pft_accounts");
                    break;

                case "categories":

                    sql = GetDeleteStatementsForSuppliedTables("pft_transactions", "pft_recurring_schedules", "pft_trasactions_repeats", "pft_transactions_rules", "pft_budgets_details", "pft_budgets", "pft_accounts");
                    break;
            }

            try
            {
                _conn.Execute(sql);
            }
            catch(Exception ex)
            {
                throw new Exception(ex.ToString());
            }

        }

        private static ulong ExecuteSQLAndReturnId(string sql, IDbConnection _conn)
        {
            
            return _conn.ExecuteScalar<ulong>(sql);
        }

        // Accounts Table Dummy Record Insert SQL
        private static string GetInsertDummyAccountRecordAndRetrieveIdSQL(string userId)
        {
            return "INSERT INTO pft_accounts (account_id, user_id, account_name, account_institution_name, account_type_id, account_description, is_a_linked_account) " +
                                $"VALUES (GeneratePrefixedId(2), {userId}, 'Dummy Account Name', 'Dummy Account Institute Name', '102', 'Dummy Account Description', '0');" + 
                            " SELECT account_id FROM pft_accounts WHERE account_name =  'Dummy Account Name'";       
        }

        // Categories Table Dummy Record Insert SQL
        private static string GetInsertDummyCategoryRecordAndRetrieveIdSQL(string userId, string standardCategoryId)
        {
            return $"SELECT category_id FROM pft_categories WHERE user_id = {userId} AND category_name = 'Business Income' INTO @standardCategoryId;" +
                        "INSERT INTO pft_categories (category_id, user_id, standard_category_id, category_type, category_name, category_description, category_color, category_display_order) " +
                                $"VALUES (GeneratePrefixedId(9), '{userId}', @standardCategoryId, '1', 'Dummy Category Name', 'Dummy Category Description', '#123456', '350');" + 
                            " SELECT category_id FROM pft_categories WHERE category_name =  'Dummy Category Name'"; 
        }

        // Goals Table Dummy Record Insert SQL
        private static string GetInsertDummyGoalRecordAndRetrieveIdSQL(string userId)
        {
            return "INSERT INTO pft_goals (goal_id, user_id, goal_name, goal_description, goal_type) " +
                                $"VALUES (GeneratePrefixedId(7), '{userId}', 'Dummy Goal Name', 'Dummy Goal Description', '1');" + 
                            " SELECT goal_id FROM pft_goals WHERE goal_name =  'Dummy Goal Name'"; 
        }

        // GoalDetails Table Dummy Record Insert SQL
        // For DELETE USE THE GREATER THAN OR EQUALS OF  "MIN(rec_id)"
        private static string GetInsertDummyGoalDetailsRecordAndRetrieveIdSQL(string goalId)
        {
            return "INSERT INTO pft_goal_details (goal_id, goal_interval, goal_amount, goal_period_start_date, goal_period_end_date) " +
                                $"VALUES ('{goalId}', '6', '500', '{(new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1)).ToString("yyyy-MM-dd")}', '{(new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(3)).ToString("yyyy-MM-dd")}');" + 
                            $" SELECT MIN(rec_id) FROM pft_goal_details WHERE goal_id = {goalId}"; 
        }
        
        // Tags Table Dummy Record Insert SQL
        private static string GetInsertDummyTagRecordAndRetrieveIdSQL(string userId)
        {

            return "INSERT INTO pft_tags (tag_id, user_id, tag_name, tag_description, tag_color, is_tag_active) " +
                                $"VALUES (GeneratePrefixedId(8), '{userId}', 'Dummy Tag Name', 'Dummy Tag Description', '#123456', '1');" + 
                            " SELECT tag_id FROM pft_tags WHERE tag_name =  'Dummy Tag Name'";

        }

        // Transactions Table Dummy Record Insert SQL
        // Dummy Record Idenfier query field name = transaction_description
        private static string GetInsertDummyTransactionRecordAndRetrieveIdSQL(string userId, string accountId, string categoryId, string? goalId, string? tagId)
        {
            
            return "INSERT INTO pft_transactions (transaction_id, user_id, transaction_date, transaction_description, transaction_amount, account_id, category_id, goal_id, tag_id, transaction_categorization, transaction_type) " +
                                $"VALUES (GeneratePrefixedId(3), '{userId}', '{(DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"))}', 'Dummy Transaction Description', '123.45', '{accountId}', '{categoryId}', {(goalId ?? "null")}, {(tagId ?? "null")}, '1', '1');" + 
                            " SELECT transaction_id FROM pft_transactions WHERE transaction_description =  'Dummy Transaction Description'";

        }

        // TransactionRepeats Table Dummy Record Insert SQL
        private static string GetInsertDummyTransactionRepeatsRecordAndRetrieveIdSQL(string userId, string transactionId)
        {
            return "INSERT INTO pft_trasactions_repeats (transaction_id, user_id, transaction_repeat_interval, transaction_repeat_end_date, is_transaction_repeat_active) " +
                                $"VALUES ('{transactionId}', '{userId}', '4', '{(DateOnly.FromDateTime(DateTime.Now.AddMonths(2)).ToString("yyyy-MM-dd"))}', '1');" + 
                            $" SELECT transaction_repeat_id FROM pft_trasactions_repeats WHERE transaction_id = {transactionId} AND user_id = {userId}";
        }

        // RecurringScheudule (Transactions) Table Dummy Record Insert SQL
        private static string GetInsertDummyRecurringScheuduleRecordAndRetrieveIdSQL(string transactionRepeatId, double recurringAmount, string accountId, string categoryId, string? goalId, string? tagId)
        {
            // recurring_next_date: is assigned a random value here!!!
            return "INSERT INTO pft_recurring_schedules (transaction_recurring_id, transaction_repeat_id, recurring_description, recurring_next_date, recurring_amount, account_id, category_id, goal_id, tag_id, transaction_categorization) " +
                                $"VALUES (GeneratePrefixedId(3), '{transactionRepeatId}', 'Dummy Transaction Recurring Description', '{(DateOnly.FromDateTime(DateTime.Now.AddMonths(3)).ToString("yyyy-MM-dd"))}', '{recurringAmount}', '{accountId}', '{categoryId}', {goalId ?? "null"}, {tagId ?? "null"}, '1');" + 
                            $" SELECT transaction_recurring_id FROM pft_recurring_schedules WHERE recurring_description = 'Dummy Transaction Recurring Description'";
        }

        // TransctionRule Table Dummy Record Insert SQL
        private static string GetInsertDummyTransctionRuleRecordAndRetrieveIdSQL(string userId, string accountId, string categoryId, string? goalId)
        {
            return "INSERT INTO pft_transactions_rules (transctions_rule_id, user_id, account_id, category_id, goal_id, tr_text_to_match, tr_text_match_comparison, tr_amount_match_comparison, tr_exact_amount, tr_description_override) " +
                                $"VALUES (GeneratePrefixedId(4), '{userId}', '{accountId}', '{categoryId}', {goalId ?? "null"}, 'Dummy Transaction Rule Match Text', '0', '0', null, null);" + 
                            $" SELECT transctions_rule_id FROM pft_transactions_rules WHERE tr_text_to_match = 'Dummy Transaction Rule Match Text' AND user_id = {userId}";
        }

        // Budget Table Dummy Record Insert SQL
        private static string GetInsertDummyBudgetRecordAndRetrieveIdSQL(string userId, string categoryId, string budgetBaseAmount)
        {
            return "INSERT INTO pft_budgets (budget_id, user_id, category_id, budget_base_amount, br_to_be_rolled_over, br_if_under_setting, br_if_over_setting) " +
                                $"VALUES (GeneratePrefixedId(6), '{userId}', '{categoryId}', '{budgetBaseAmount}',  '0', '0', '0');" + 
                            $" SELECT budget_id FROM pft_budgets WHERE user_id = {userId} AND category_id = {categoryId}";
        }

        // Budget Details Table Dummy Record Insert SQL
        private static string GetInsertDummyBudgetDetailsRecordAndRetrieveIdSQL(string budgetId)
        {
            return "INSERT INTO pft_budgets_details (budget_id, budget_amount, budget_for_month_year) " +
                                $"VALUES ('{budgetId}', '250', '{(new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1)).ToString("yyyy-MM-dd")}');" + 
                            $" SELECT budget_id FROM pft_budgets_details WHERE budget_id = '{budgetId}'";
        }

        private static string GetInsertDummyImportTemplateRecordAndRetrieveIdSQL(string userId, string accountId)
        {
            return "INSERT INTO pft_import_templates (user_id, account_id, import_template_name, import_type, import_header_row_index, import_template_date_field_index, import_template_description_field_index, import_template_amount_field_index, import_template_debit_field_index, import_template_credit_field_index) " +
                                $"VALUES ('{userId}', '{accountId}', 'Dummy Import Template Name', '1', '0', '1', '2', '3', '4','5');" + 
                            $" SELECT import_template_id FROM pft_import_templates WHERE import_template_name = '{"Dummy Import Template Name"}'";
        }

        // INSERT DUMMY RECORD IN RELATED TABLES - END
        
        // User's email varification - Start

        // Token Hash: "UrBHEdT5W+xAgWbcyy9VUXnOJVmeI3kADb2ITqkhuhA="
        // Token: "LneO3S8w325fcNYNei0hkgEw0md-vR1YKOL4UlP2Ay1Pckp5oU5whtNCwhD6hDrUyEyOQAMfNosdsWunkLj60ZwaGUTqmYYZrDZSLAqBLGaPdUZvCNfV46f89OTZbDVq48vwUg-77wyt9c7Sp-c12bt-vA1xwkyjaQX4vDl8zxFnblK_3USlROAO6zGfsxRdDfhNe5cAHBg0eC66oconm7tVUs06pjkzs1KijnPo7PVaDbx2TYJ7dAi-LQxF6mAmM28FkmUa6xvEcTckDamTR5Y1Y8C0YBhRk8vxudCxwg2nqOhKxTHxY7250gWTwtzLdI8Zs2AsY38Z2Kqrou75"
        // 
        // RESET fields related to email varification in "pft_user_tokens" and "pft_users" tables. 
        // token_hash is updated to a matched Token Value so that test can send the Token which will be matched with token hash...
        public static void ResetEmailVarificationFieldValuesForUser(string userId, IDbConnection _conn)
        {

            string sql = $"UPDATE pft_user_tokens ut JOIN pft_users u ON u.user_id = ut.user_id SET u.is_user_email_verified = '0', ut.token_hash = 'UrBHEdT5W+xAgWbcyy9VUXnOJVmeI3kADb2ITqkhuhA=', ut.is_used = '0' WHERE u.user_id = '{userId}';";

            _conn.Execute(sql);

        }

        // Gets the flag values for the Email Varification Related Database Fields
        public static (int isUserEmailVarified, int isUsed) GetEmailVarificationFieldValuesForUser(string userId, IDbConnection _conn)
        {
            string sql = $"SELECT u.is_user_email_verified AS IsUserEmailVarified, ut.is_used AS IsUsed FROM pft_user_tokens ut JOIN pft_users u ON u.user_id = ut.user_id WHERE u.user_id = '{userId}';";

            return  _conn.QueryFirst<(int isUserEmailVarified, int isUsed)>(sql);

        }

        // User's email varification - End

        // Gets the record_id of a record in test by table name, field name, search field name, search value and user_id
        public static ulong GetRecIdByName(string queryTableName, string queryFieldName, string searchFieldName, string searchValue, string userId, IDbConnection _conn)
        {
            string sql = $"SELECT {queryFieldName} FROM {queryTableName} WHERE {searchFieldName} = @SearchValue AND user_id = @UserId";

            // Get this value and then delete the record!!!
            return _conn.ExecuteScalar<ulong>(sql, new
            {
                SearchValue = searchValue,
                UserId = ulong.Parse(userId)
            });
        }

        // Authentication Tests - Start
        private static string GetUsersTableSelectStatementWithFieldNames()
        {

            return "SELECT user_first_name AS FirstName, " +
                    "user_last_name AS LastName, " +
                    "user_user_name AS UserName, " +
                    "user_email AS Email, " +
                    "'PassWord43@^' AS Password, " +
                    "'PassWord43@^' AS ConfirmPassword, " +
                    "user_password_hash AS PasswordHash, " +
                    "true AS AgreeToTermsAndPrivacyPolicy " +
                    "FROM pft_users ";
        
        }
        
        // Get the any one record from a table for a user_id
        public static UserRegistration? GetSingleRecordFromUsersTable(string userId, IDbConnection _conn)
        {
            string sql = $"{GetUsersTableSelectStatementWithFieldNames()} WHERE user_id = @UserId LIMIT 1";
            
            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<UserRegistration?>(sql, new
            {
                UserId = ulong.Parse(userId)
            });
        }

        
        // Get the specific record from a table for a user_id using a search field name and value
        public static UserRegistration? GetUserRecordByName(string userName, string userId, IDbConnection _conn)
        {
            string sql = $"{GetUsersTableSelectStatementWithFieldNames()} WHERE user_user_name = @UserName AND user_id = @UserId";

            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<UserRegistration?>(sql, new
            {

                UserName = userName,
                UserId = ulong.Parse(userId)

            });
        }

        // Authentication Tests - End


        // Accounts Tests - Start
        private static string GetAccountsTableSelectStatementWithFieldNames()
        {
            return "SELECT account_id AS AccountId, " +
                                "user_id AS UserId, " +
                                "account_name AS AccountName, " +
                                "account_institution_name AS AccountInstitutionName, " +
                                "account_type_id AS AccountTypeId, " +
                                "account_description AS AccountDescription, " +
                                "is_a_linked_account AS IsALinkedAccount " + 
                                "FROM pft_accounts ";
        }
        
        // Get the any one record from a table for a user_id
        public static Account? GetSingleRecordFromAccountsTable(string userId, IDbConnection _conn)
        {
            string sql = $"{GetAccountsTableSelectStatementWithFieldNames()} WHERE user_id = @UserId LIMIT 1";
            
            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<Account?>(sql, new
            {
                UserId = ulong.Parse(userId)
            });
        }

        
        // Get the specific record from a table for a user_id using a search field name and value
        public static Account? GetAccountRecordByName(string accountName, string userId, IDbConnection _conn)
        {
            string sql = $"{GetAccountsTableSelectStatementWithFieldNames()} WHERE account_name = @AccountName AND user_id = @UserId";

            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<Account?>(sql, new
            {
                AccountName = accountName,
                UserId = ulong.Parse(userId)
            });
        }

        // Accounts Tests - End


        // Tags Tests - Start
        private static string GetTagsTableSelectStatementWithFieldNames()
        {
            return "SELECT tag_id AS TagId, " +
                            "user_id AS UserId, " +
                            "tag_name AS TagName, " +
                            "tag_description AS TagDescription, " +
                            "tag_color AS TagColor, " +
                            "is_tag_active AS IsTagActive " +
                            "FROM pft_tags ";
        }
        // Get the any one record from a table for a user_id
        public static Tag? GetSingleRecordFromTagsTable(string userId, IDbConnection _conn)
        {
            string sql = $"{GetTagsTableSelectStatementWithFieldNames()} WHERE user_id = @UserId LIMIT 1";
            
            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<Tag?>(sql, new
            {
                UserId = ulong.Parse(userId)
            });
        }

        // Get the specific record from a table for a user_id using a search field name and value
        public static Tag? GetTagRecordByName(string tagName, string userId, IDbConnection _conn)
        {
            string sql = $"{GetTagsTableSelectStatementWithFieldNames()} WHERE tag_name = @TagName AND user_id = @UserId";

            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<Tag?>(sql, new
            {
                TagName = tagName,
                UserId = ulong.Parse(userId)
            });
        }

        // Tags Tests - End

        // Categories Tests - Start

        // Get the uncategorized category_id for a supplied user_id
        public static ulong GetCategoryIdForUser(string userId, string categoryName, IDbConnection _conn)
        {

            string sql = "SELECT category_id FROM pft_categories WHERE user_id = @UserId AND category_name = @CategoryName LIMIT 1";

            // Get this value and then delete the record!!!
            return _conn.ExecuteScalar<ulong>(sql, new
            {
                UserId = ulong.Parse(userId),
                CategoryName =  categoryName 
            });

        }

        // Select statement for categories table with field names that maps to the Category model class
        private static string GetCategoriesTableSelectStatementWithFieldNames()
        {
            return "SELECT category_id AS CategoryId, " +
                            "user_id AS UserId, " +
                            "standard_category_id AS StandardCategoryId, " +
                            "category_type AS CategoryType, " +
                            "category_name AS CategoryName, " +
                            "category_description AS CategoryDescription, " +
                            "category_color AS CategoryColor, " +
                            "category_display_order AS CategoryDisplayOrder " +
                            "FROM pft_categories ";
        }

        // Get the any one record from a table for a user_id
        public static Category? GetSingleRecordFromCategoriesTable(string userId, IDbConnection _conn)
        {
            string sql = $"{GetCategoriesTableSelectStatementWithFieldNames()} WHERE user_id = @UserId AND category_id <> standard_category_id LIMIT 1";
            
            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<Category?>(sql, new
            {
                UserId = ulong.Parse(userId)
            });
        }

        // Get the specific record from a table for a user_id using a search field name and value
        public static Category? GetCategoryRecordByName(string categoryName, string userId, IDbConnection _conn)
        {
            string sql = $"{GetCategoriesTableSelectStatementWithFieldNames()} WHERE category_name = @CategoryName AND user_id = @UserId";

            // Get this value and then delete the record!!!
            return _conn.QuerySingleOrDefault<Category?>(sql, new
            {
                CategoryName = categoryName,
                UserId = ulong.Parse(userId)
            });
        }

        // Categories Tests - End

        // Goals Tests - Start

        // Select statement for goals table with field names that maps to the Category model class
        private static string GetGoalsTableSelectStatementWithFieldNames()
        {
            return "SELECT goal_id AS GoalId, " +
                            "user_id AS UserId, " +
                            "goal_name AS GoalName, " +
                            "goal_description AS GoalDescription, " +
                            "goal_type AS GoalType " +
                            "FROM pft_goals ";
        }

        // Select statement for goals table with field names that maps to the Category model class
        private static string GetGoalDetailsTableSelectStatementWithFieldNames()
        {
            return "SELECT gd.goal_id AS GoalId, " +
                            "gd.goal_interval AS GoalInterval, " +
                            "gd.goal_amount AS GoalAmount, " +
                            "gd.goal_period_start_date AS GoalPeriodStartDate, " +
                            "gd.goal_period_end_date AS GoalPeriodEndDate " +
                            "FROM pft_goal_details gd";
        }

        // Return GoalDetails of supplied GoalId
        private static IEnumerable<GoalDetails>? GetGoalDetailsById (string goalId, IDbConnection _conn)
        {

            string sql = $"{GetGoalDetailsTableSelectStatementWithFieldNames()} WHERE goal_id = @GoalId";

            return _conn.Query<GoalDetails>(sql, new
            {
                GoalId = ulong.Parse(goalId)
            });

        }
        
        // Get the any one record from a table for a user_id
        public static Goal? GetSingleRecordFromGoalsTable(string userId, IDbConnection _conn)
        {
            
            string sql = $"{GetGoalsTableSelectStatementWithFieldNames()} WHERE user_id = @UserId LIMIT 1";
            
            Goal? goal = _conn.QuerySingleOrDefault<Goal?>(sql, new
            {
                UserId = ulong.Parse(userId)
            });

            if(goal != null)
            {
                
                goal.GoalDetails = GetGoalDetailsById(goal.GoalId, _conn);

            }

            return goal;
        }

        // Get the specific record from a table for a user using a search value
        public static Goal? GetGoalRecordByName(string goalName, string userId, IDbConnection _conn)
        {

            string sql = $"{GetGoalsTableSelectStatementWithFieldNames()} WHERE goal_name = @GoalName AND user_id = @UserId LIMIT 1";   // GOAL CAN HAVE MORE THAN ONE RECORD WITH SAME NAME

            // Get this value and then delete the record!!!
            Goal? goal = _conn.QuerySingleOrDefault<Goal?> (sql, new
            {
                GoalName = goalName,
                UserId = ulong.Parse(userId)
            });

            return goal;

        }

        public static IEnumerable<GoalDetails>? GetGoalDetailsByGoalName(string goalName, string userId, IDbConnection _conn)
        {

            string sql = $"{GetGoalDetailsTableSelectStatementWithFieldNames()} JOIN pft_goals g ON g.goal_id = gd.goal_id WHERE g.goal_name = @GoalName AND g.user_id = @UserId LIMIT 1";   // GOAL CAN HAVE MORE THAN ONE RECORD WITH SAME NAME

            // Get this value and then delete the record!!!
            IEnumerable<GoalDetails>? goalDetails = _conn.Query<GoalDetails>(sql, new
            {
                GoalName = goalName,
                UserId = ulong.Parse(userId)
            });

            return goalDetails;

        }

        // Goals Tests - End

    }
}
