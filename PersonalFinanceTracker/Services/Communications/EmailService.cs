using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1;
using PersonalFinanceTracker.Services.Contracts;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace PersonalFinanceTracker.Services.Communications
{
    public class EmailService : IEmailService
    {

        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public bool SendEmail(string recipientEmail, string userName, int emailFor, string verificationLink)
        {

            string smtpServer = _config["EmailSettings:SmtpServer"];
            string username = _config["EmailSettings:Username"];
            string secret = _config["EmailSettings:Secret"];
            (string emailSubject, string emailBody) emailSubjectAndMessage = GenerateEmailSubjectAndBodyMessage(emailFor);

            // Configure the SMTP client
            using (SmtpClient smtpClient = new SmtpClient(smtpServer))
            {

                smtpClient.Port = 587; // Common port for TLS
                smtpClient.Credentials = new NetworkCredential(username, secret);
                smtpClient.EnableSsl = true;

                // Create the email message
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress("chiragpatel.android@gmai.com", "Chirag");
                mail.To.Add(recipientEmail);
                mail.Subject = emailSubjectAndMessage.emailSubject;
                mail.Body = emailSubjectAndMessage.emailBody.Replace("{userName}", userName).Replace("{verificationLink}", verificationLink) + "<p>Personal Finance Tracker Team.</p>";
                mail.IsBodyHtml = true;

                try
                {
                    // Send the email
                    smtpClient.Send(mail);

                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.ToString());
                    return false;
                }

            }
            return true;

        }


        public (string emailSubject, string emailBody) GenerateEmailSubjectAndBodyMessage(int emailFor)
        {
            string emailSubject ="", emailBody="";

            switch (emailFor)
            {
                case 1: // Email Confirmation
                    emailSubject = "Personal Finance Tracker - Email Confirmation";
                    emailBody = "<h1>Hello {userName}!</h1><p>Please confirm your email address!</p><p>{verificationLink}</p>";
                    break;
                case 2: // Password Reset
                    emailSubject = "Personal Finance Tracker - Password Reset";
                    emailBody = "<h1>Dear {userName}!</h1><p>Please use the link below to reset your password before the mentioned expiration date!</p><p>{verificationLink}</p>";
                    break;
            }

            return (emailSubject, emailBody);
        }

        // GENERATES THE UNIQUE 255 BYTES & ITS FIXED-LENGTH REVERSE-PROOF HASH
        public (string OriginalBase64, string HashToStore) GenerateTokenAndHash()
        {

            byte[] randomBytes = new byte[255];
            RandomNumberGenerator.Fill(randomBytes);

            // Get raw base64
            string base64 = Convert.ToBase64String(randomBytes);

            // Make it URL safe for the user
            string publicTokenForUser = base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');

            // Hash the RAW BYTES
            byte[] hashBytes = SHA256.HashData(randomBytes);
            string hashToStoreInDb = Convert.ToBase64String(hashBytes);

            return (publicTokenForUser, hashToStoreInDb);


        }

        // COMPARES AND VERIFY THE RECEIVED BYTES AGAINST THE DATABASE VALUE
        public string GenerateHashFromToken(string receivedPublicToken)
        {
            // Reverse the URL replacements back to standard Base64
            string incomingBase64 = receivedPublicToken
                .Replace("-", "+")
                .Replace("_", "/");

            // Pad it back out so Convert.FromBase64String doesn't crash
            switch (incomingBase64.Length % 4)
            {
                case 2: 
                    incomingBase64 += "=="; 
                    break;
                case 3: 
                    incomingBase64 += "="; 
                    break;
            }

            // Decode back to the EXACT original 255 bytes
            byte[] receivedBytes = Convert.FromBase64String(incomingBase64);

            // Hash those exact bytes
            byte[] computedHashBytes = SHA256.HashData(receivedBytes);

            return Convert.ToBase64String(computedHashBytes);

        }

    }
}
