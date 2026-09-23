namespace PersonalFinanceTracker.Models
{
    public class Pagination
    {
        public int PageSize { get; set; } = 25;     // Total Number of records per page
        public int RecordStartNumber { get; set; } = 1;
        public int RecordEndNumber { get; set; } = 25;
        public int TotalNumberOfRecords { get; set; } = 0;  // Total Records in Database Table
    }
}
