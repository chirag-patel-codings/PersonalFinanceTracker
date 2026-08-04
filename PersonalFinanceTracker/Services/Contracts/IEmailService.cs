using System.Net;
using System.Net.Mail;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IEmailService
    {
        public (string emailSubject, string emailBody) GenerateEmailSubjectAndBodyMessage(int emailFor);
        public bool SendEmail(string recipientEmail, string userName, int emailFor, string verificationLink);
        public (string OriginalBase64, string HashToStore) GenerateTokenAndHash();
        public string GenerateHashFromToken(string receivedBase64Token);
    }
}