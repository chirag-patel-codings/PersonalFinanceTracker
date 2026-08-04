namespace PersonalFinanceTracker.Models
{
    public class AccountType
    {
        public int AccountTypeId { get; set; }
        public string AccountTypeClassification { get; set; }
        public string AccountTypeName {  get; set; }
        public string AccountTypeDescription { get; set; }
        public string AccountTypeNameAndDescription => $"{AccountTypeName}: {AccountTypeDescription}";
        
    }
}
