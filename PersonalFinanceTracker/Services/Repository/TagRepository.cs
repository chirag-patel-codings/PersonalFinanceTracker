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
    public class TagRepository : ITagRepository
    {
        private readonly IDbConnectionFactory _factory;
        public TagRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        // Returns the total number of tag records in 'pft_tags' table for the current logged-in user
        public int GetTotalNoOfRecords(ulong userId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT COUNT(tag_id) FROM pft_tags WHERE user_id = @UserId";

            return _conn.ExecuteScalar<int>(sql, new { UserId = userId });
        }


        // Gets Single Tag Record based on supplied tagId
        public Tag? GetTag(ulong userId, string tagId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT CAST(t.tag_id AS CHAR) AS TagId, " +
                                "t.tag_name AS TagName, " +
                                "t.tag_description AS TagDescription, " +
                                "t.tag_color AS TagColor, " +
                                "t.is_tag_active AS IsTagActive " +
                          "FROM pft_tags t " +
                         "WHERE t.user_id = @UserId" +
                         "  AND t.tag_id = @TagId;";

            return _conn.QuerySingle<Tag>(sql, new { UserId = userId, TagId = ulong.Parse(tagId) });
        }


        // Gets the records between a sequence of supplied 'recStartNumber' and 'recEndNumber'
        public IEnumerable<Tag>? GetTags(ulong userId, int recStartNumber, int recEndNumber, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("rec_start_number", recStartNumber);
            parameters.Add("rec_end_number", recEndNumber);

            return _conn.Query<Tag>("sp_pft_get_tags",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );
        }


        // Saves a single tag record
        public bool SaveTag(Tag tag, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("tag_id", tag.TagId == "" ? 0 : ulong.Parse(tag.TagId));
            parameters.Add("user_id", tag.UserId);
            parameters.Add("tag_name", tag.TagName);
            parameters.Add("tag_description", tag.TagDescription);
            parameters.Add("tag_color", tag.TagColor);
            parameters.Add("is_tag_active", tag.IsTagActive);
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_tag",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_effected_records") > 0 ? true : false;
        }

        // Deletes a Tag by supplied userId and tagId
        public bool DeleteTag(ulong userId, string tagId, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("tag_id", ulong.Parse(tagId));
            parameters.Add("user_id", userId);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_tag",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;
        }

        
    }
}
