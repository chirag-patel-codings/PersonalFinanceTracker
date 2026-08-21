using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IRecurringRepository
    {
        public int GetTotalNoOfRecords(ulong userId, string? connString);
        public Recurring? GetRecurring(ulong userId, string transactionRecurringId, string? connString);
        public IEnumerable<Recurring>? GetRecurrings(ulong userId, int recStartNumber, int recEndNumber, string? connString);
        public bool SaveRecurring(Recurring recurring, string? connString);
        public bool DeleteRecurring(ulong userId, string transactionRecurringId, bool deleteSeries, string? connString);
        // public IEnumerable<ListOptionStringId> GetSelectOptionsDataFor(ulong userId, string selectFor, string? connString);


    }
}
