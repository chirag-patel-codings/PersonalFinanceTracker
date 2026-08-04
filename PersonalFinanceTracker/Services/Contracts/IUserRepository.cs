using PersonalFinanceTracker.Models.Authentication;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IUserRepository
    {
        public SessionUser? GetUserDetailsForLogIn(string username, string connString);
    }
}
