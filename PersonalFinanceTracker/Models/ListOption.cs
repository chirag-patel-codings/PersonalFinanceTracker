namespace PersonalFinanceTracker.Models
{
    // This class is generically useful to generate the type of s like: Savings = 1, Reduce Debt = 2, Investing = 3, Tax Planning = 4, Other = 5
    // and  intervals like: // Weekly - 1, BiWeekly - 2, Monthly – 3, Quarterly – 4,  Half - Yearly – 5 , Yearly - 6
    public class ListOption
    {
        public ListOption(sbyte listOptionId, string listOptionName)
        {
            ListOptionId = listOptionId;
            ListOptionName = listOptionName;
            
        }

        public sbyte ListOptionId { get; set; }
        public string ListOptionName { get; set; }

        
    }

}
