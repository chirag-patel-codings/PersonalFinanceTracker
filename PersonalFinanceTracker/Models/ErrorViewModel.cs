namespace PersonalFinanceTracker.Models
{
    public class ErrorViewModel
    {
        // Process ID / Trace Identifier Details on the server 
        public string? RequestId { get; set; }

        // Display RequestId to user
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        // For HttpStatusCode
        public int StatusCode { get; set; }

        // For Error Message
        public string? ErrorMessage { get; set; }
    }
}
