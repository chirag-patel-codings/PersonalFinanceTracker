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
    public class BulkImportTemplateRepository : IBulkImportTemplateRepository
    {
        
        private readonly IDbConnectionFactory _factory;
        public BulkImportTemplateRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }


        // Gets Single BulkImport Record based on supplied importTemplateName
        public IEnumerable<BulkImportTemplate> GetBulkImportTemplate (ulong userId, string importTemplateName, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            var parameters = new DynamicParameters();
            parameters.Add("user_id", userId);
            parameters.Add("import_template_name", importTemplateName.Trim());

            return _conn.Query<BulkImportTemplate>("sp_pft_get_bulk_import_template",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

        }

        // Gets all the template names for the supplied userId
        public IEnumerable<string>? GetBulkImportTemplateNames(ulong userId, string? connString)
        {

            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            string sql = "SELECT i.import_template_name " +
                           "FROM pft_import_templates i " +
                          "WHERE i.user_id = @UserId";


            return _conn.Query<string>(sql, new { UserId = userId});

        }


        // Inserts/Updates a single Bulk Import Template record
        public bool SaveBulkImportTemplate(BulkImportTemplate bulkImportTemplate, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("import_template_id", bulkImportTemplate.ImportTemplateId == null ? 0 : int.Parse(bulkImportTemplate.ImportTemplateId));
            parameters.Add("user_id", bulkImportTemplate.UserId);
            parameters.Add("account_id", ulong.Parse(bulkImportTemplate.AccountId));
            parameters.Add("import_template_name", bulkImportTemplate.ImportTemplateName.Trim());
            parameters.Add("import_type", bulkImportTemplate.ImportType);
            parameters.Add("import_header_row_index", bulkImportTemplate.HeaderRowIndex);
            parameters.Add("import_template_date_field_index", bulkImportTemplate.DateFieldIndex);
            parameters.Add("import_template_description_field_index", bulkImportTemplate.DescriptionFieldIndex);
            parameters.Add("import_template_amount_field_index", bulkImportTemplate.AmountFieldIndex);
            parameters.Add("import_template_debit_field_index", bulkImportTemplate.DebitFieldIndex);
            parameters.Add("import_template_credit_field_index", bulkImportTemplate.CreditFieldIndex);
            parameters.Add("total_effected_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_save_bulk_import_template",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_effected_records") > 0 ? true : false;

        }

        // Deletes a BulkImport by supplied userId and importTemplateName
        public bool DeleteBulkImportTemplate(ulong userId, string importTemplateName, string? connString)
        {
            connString = connString ?? "pft_con_str";
            using IDbConnection _conn = _factory.GetDBConnection(connString);

            // Initialize DynamicParameters
            var parameters = new DynamicParameters();
            parameters.Add("import_template_name", importTemplateName.Trim());
            parameters.Add("user_id", userId);
            parameters.Add("total_deleted_records", direction: ParameterDirection.Output);

            _conn.Execute("sp_pft_delete_bulk_import_template",
                           parameters,
                           commandType: CommandType.StoredProcedure
                          );

            return parameters.Get<int>("total_deleted_records") > 0 ? true : false;

        }



    }
}
