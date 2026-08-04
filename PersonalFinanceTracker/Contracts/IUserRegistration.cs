using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Contracts
{
    public interface IUserRegistration
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? UserName {  get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? PasswordHash { get; set; }
        public string? Email { get; set; }
        public bool AgreeToTermsAndPrivacyPolicy { get; set; }

    }
}
