using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models.Authentication
{
    // This class will be used to handle user registrations.
    public class UserRegistration : IUserRegistration
    {
        
        [StringLength(100, MinimumLength = 1, ErrorMessage = "First Name must be between 1 and 100 characters long!")]
        [Required(ErrorMessage = "First Name is required!")]
        public string? FirstName { get; set; }


        [StringLength(100, MinimumLength = 1, ErrorMessage = "Last Name must be between 1 and 100 characters long!")]
        [Required(ErrorMessage = "Last Name is required!")]
        public string? LastName { get; set; }


        [StringLength(75, MinimumLength = 12, ErrorMessage = "Username must be between 12 and 75 characters long!")]
        [Remote(action: "UserWithSameUserNameExists", controller: "Authentication", ErrorMessage = "User with this Username already exists!")]
        [Required(ErrorMessage = "Username is required!")]
        public string? UserName { get; set; }


        [RegularExpression(APP_CONSTANTS.EMAIL_ADDRESS_VALIDATION_REGEX_PATTERN, ErrorMessage = "Please enter a valid email address!")]
        [Remote(action: "UserWithEmailAlreadyExists", controller: "Authentication", ErrorMessage = "User with this Email already exists!")]
        [Required(ErrorMessage = "Email is required!")]
        public string? Email { get; set; }

        // RegularExpression are sent for the client end validations!!!
        [RegularExpression(APP_CONSTANTS.PASSWORD_VALIDATION_REGEX_PATTERN,
                            ErrorMessage = "Password must contain at least:\n\t2 uppercase letters,\n\t2 lowercase letters,\n\t2 numbers,\n\t2 allowed special characters - %$@!^&#().\n\tNo spaces, no other symbols!")]
        [MinLength(12, ErrorMessage = "Password must be atleast 12 characters long!")]
        [Required(ErrorMessage = "Password is required!")]
        public string? Password { get; set; }


        [Compare("Password", ErrorMessage = "The password and confirmation password do not match!")]
        [Required(ErrorMessage = "Please confirm your password!")]
        public string? ConfirmPassword { get; set; } = string.Empty;


        // Only generate/re-generate when saving to the database... return "" in returning view when displaying user errors!!
        public string? PasswordHash { get; set; }

        
        [MustBeTrue(ErrorMessage = "Please accept the terms and conditions!")]
        public bool AgreeToTermsAndPrivacyPolicy { get; set; }

        // Required to save the email confirmation data in the database.
        public string? userEmailTokenHash { get; set; }

    }

    public class MustBeTrueAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            return value is bool b && b;
        }

    }
}
