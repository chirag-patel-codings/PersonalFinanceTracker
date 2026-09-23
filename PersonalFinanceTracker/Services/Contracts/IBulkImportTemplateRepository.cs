using System.Xml.Serialization;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IBulkImportTemplateRepository
    {
        public IEnumerable<string>? GetBulkImportTemplateNames(ulong userId, string? connString);

        public IEnumerable<BulkImportTemplate> GetBulkImportTemplate(ulong userId, string importTemplateName, string? connString);

        public bool SaveBulkImportTemplate(BulkImportTemplate bulkImportTemplate, string? connString);
        public bool DeleteBulkImportTemplate(ulong userId, string importTemplateName, string? connString);

    }
}
