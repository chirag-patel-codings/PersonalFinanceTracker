using PersonalFinanceTracker.Models.Authentication;

namespace PersonalFinanceTracker.Contracts
{
    public interface ISessionUser
    {
        public ulong? UserId { get; set; }
        public string? UserNameOrEmail { get; set; }
        public string? Password { get; set; }
        public string? CurrencyCode { get; set; }
        public string? CurrencySymbol {  get; set; }
    }
}
