using PersonalFinanceTracker.Models.Authentication;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IUserRegistrationRepository
    {
        public bool CreateNewUser(UserRegistration userRegistration, string connString);
        public bool UserWithEmailAlreadyExists(string email, string connString);
        public bool UserWithSameUserNameExists(string userName, string connString);
        public int UpdateUserEmailVarification(string userEmailTokenHash, string connString);
    }
}
