using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface ITagRepository
    {
        public int GetTotalNoOfRecords(ulong userId, string? connString);
        public Tag? GetTag(ulong userId, string tagId, string? connString);
        public IEnumerable<Tag>? GetTags(ulong userId, int recStartNumber, int recEndNumber, string? connString);
        public bool SaveTag(Tag tag, string? connString);
        public bool DeleteTag(ulong userId, string tagId, string? connString);
    }
}
