namespace PersonalFinanceTracker.Models
{
    public static class APP_CONSTANTS
    {
        public const string EMAIL_ADDRESS_VALIDATION_REGEX_PATTERN = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
        public const string PASSWORD_VALIDATION_REGEX_PATTERN = @"^(?=(?:.*[A-Z]){2,})(?=(?:.*[a-z]){2,})(?=(?:.*[%$@!^&#()][^%$@!^&#()]*){2,})(?=(?:.*\d){2,})[A-Za-z\d%$@!^&#()]+$";
    }
}
