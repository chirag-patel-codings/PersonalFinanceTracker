using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Org.BouncyCastle.Asn1.Mozilla;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Services.Contracts;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PersonalFinanceTracker.Models.Authentication
{
    public class SessionUser : ISessionUser
    {
        public ulong? UserId { get; set; }      // is used to retrieve from database

        [Required(ErrorMessage = "Username or Email is required!")]
        public string? UserNameOrEmail { get; set; }

        [Required(ErrorMessage = "Password is required!")]
        public string? Password { get; set; }

        public string? CurrencyCode { get; set; }
        public string? CurrencySymbol { get; set; }

    }
}
