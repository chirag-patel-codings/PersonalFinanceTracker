namespace PersonalFinanceTracker.Models
{
    // This class is generically useful to generate the type of goals like: Savings = 1, Reduce Debt = 2, Investing = 3, Tax Planning = 4, Other = 5
    // and goal intervals like: // Weekly - 1, BiWeekly - 2, Monthly – 3, Quarterly – 4,  Half - Yearly – 5 , Yearly - 6
    public class GoalListOption
    {
        public GoalListOption(sbyte goalListOptionId, string goalListOptionName)
        {
            GoalListOptionId = goalListOptionId;
            GoalListOptionName = goalListOptionName;
        }

        public sbyte GoalListOptionId { get; set; }
        public string GoalListOptionName { get; set; }
    }

}
