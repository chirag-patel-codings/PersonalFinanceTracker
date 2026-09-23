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
    public class CategoryRepository : ICategoryRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public CategoryRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }


        // Gets the total number of category records
        public int GetTotalNoOfRecords(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT COUNT(category_id) FROM pft_categories WHERE user_id = @UserId";

            return _conn.ExecuteScalar<int>(sql, new { UserId = userId });
        }


        // Gets Single Category Record based on supplied categoryId
        public Category? GetCategory(ulong userId, string categoryId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT CAST(c.category_id AS CHAR) AS CategoryId, " +
                                "c.standard_category_id AS StandardCategoryId, " +
                                "c.category_type AS CategoryType, " +
                                "c.category_name AS CategoryName, " +
                                "c.category_description AS CategoryDescription, " +
                                "c.category_color AS CategoryColor, " +
                                "c.category_display_order AS CategoryDisplayOrder " +
                          "FROM pft_categories c " +
                         "WHERE c.user_id = @UserId" +
                         "  AND c.category_id = @CategoryId;";

            return _conn.QuerySingle<Category>(sql, new { UserId = userId, CategoryId = ulong.Parse(categoryId) });
        }

        // Gets the records between a sequence of supplied 'recStartNumber' and 'recEndNumber'
        public IEnumerable<Category>? GetCategories(ulong userId, int recStartNumber, int  recEndNumber, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);

            return _conn.Query<Category>("sp_pft_get_categories",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }
        
        // Saves a single category record
        public bool SaveCategory(Category category, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("category_id", category.CategoryId == "" ? 0 : ulong.Parse(category.CategoryId));
            parameters.Add("user_id", category.UserId);
            parameters.Add("standard_category_id", ulong.Parse(category.StandardCategoryId));
            parameters.Add("category_type", category.CategoryType);
            parameters.Add("category_name", category.CategoryName.Trim());
            parameters.Add("category_description", string.IsNullOrEmpty(category.CategoryDescription) ? category.CategoryDescription : category.CategoryDescription.Trim());
            parameters.Add("category_color", category.CategoryColor);
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_category",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_effected_records") > 0 ? true : false;

        }

        // Deletes a Category by supplied userId and categoryId
        public bool DeleteCategory(ulong userId, string categoryId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("category_id", ulong.Parse(categoryId));
            parameters.Add("user_id", userId);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_category",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;

        }

        // Returns a list of all standardize Categories
        public IEnumerable<StandardCategories> GetStandardCategories(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT CONVERT(c.category_id, CHAR) AS CategoryId, c.category_name AS CategoryName FROM pft_categories c WHERE c.user_id = @UserId AND c.category_id = c.standard_category_id;";

            return _conn.Query<StandardCategories>(sql, new { UserId = userId });

        }

    }
}
