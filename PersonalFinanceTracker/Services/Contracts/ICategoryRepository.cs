using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface ICategoryRepository
    {
        public int GetTotalNoOfRecords(ulong userId, string? connString);
        public Category? GetCategory(ulong userId, string categoryId, string? connString);
        public IEnumerable<Category>? GetCategories(ulong userId, int recStartNumber, int recEndNumber, string? connString);
        public bool SaveCategory(Category category, string? connString);
        public bool DeleteCategory(ulong userId, string categoryId, string? connString);
        public IEnumerable<StandardCategories> GetStandardCategories(ulong userId, string? connString);
    }
}
