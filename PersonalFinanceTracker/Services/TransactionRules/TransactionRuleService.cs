using Dapper;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using System.Data;

using TransactionDataModel = PersonalFinanceTracker.Models.Transaction;

/*
 * This functionality is best generated in database by use of stored procedure. But to demonstrate LINQ awareness,
 * it has been implemented as service in this project!
 */
namespace PersonalFinanceTracker.Services.TransactionRules
{
    public class TransactionRuleService : ITransactionRuleService
    {
        
        private readonly ITransactionRuleRepository _transactionRulesRepository;

        // For new Rule Created By User
        public TransactionRuleService(ITransactionRuleRepository transactionRulesRepository)
        {
            // _transactionRepository = transactionRepository;
            _transactionRulesRepository = transactionRulesRepository;
        }

        // Applies a single rule as per rule matching criteria!
        // If user creates/executes a single rule, then this function can be used.
        private (bool isRuleApplied, TransactionDataModel transaction) ApplyRuleValues(TransactionRule transactionRule, TransactionDataModel transaction)
        {
            
            bool applyRuleValues = false;

            switch (transactionRule.TransactionTextMatchComparision)
            {
                case 0:     // contains (Default) = 0,
                    applyRuleValues = transaction.TransactionDescription.Contains(transactionRule.TransactionTextToMatch, StringComparison.OrdinalIgnoreCase);
                    break;

                case 1:     // starts with = 1
                    applyRuleValues = transaction.TransactionDescription.StartsWith(transactionRule.TransactionTextToMatch, StringComparison.OrdinalIgnoreCase);
                    break;

                default:    // exact match = 2
                    applyRuleValues = transaction.TransactionDescription.Equals(transactionRule.TransactionTextToMatch, StringComparison.OrdinalIgnoreCase);
                    break;
            }

            // If first condition is met!
            if (applyRuleValues)
            {

                switch (transactionRule.TransactionAmountMatchComparision)
                {
                    case 0:     // Any Amount = 0   (NO NEED TO COMPARE)
                        break;

                    case 1:     // Money In = 1 (Credit)
                        applyRuleValues = transaction.TransactionAmount > 0;
                        break;

                    case 2:     // Money Out = 2    (Debit)
                        applyRuleValues = transaction.TransactionAmount < 0;
                        break;

                    default:    // Exact Amount = 3
                        applyRuleValues = transaction.TransactionAmount == transactionRule.TransactionExactAmount;
                        break;
                }

            }

            // Apply the rule values (If all rule conditions are met!!!)
            if (applyRuleValues)
            {

                transaction.CategoryId = transactionRule.CategoryId;
                transaction.GoalId = string.IsNullOrEmpty(transactionRule.GoalId) ? transaction.GoalId : transactionRule.GoalId;
                transaction.TransactionDescription = string.IsNullOrWhiteSpace(transactionRule.TransactionDescriptionOverride) ? transaction.TransactionDescription : transactionRule.TransactionDescriptionOverride;

            }

            return (applyRuleValues, transaction);
            
        }

        // Complete -- returns true if rule applied!!!
        // Applies the single rule to the single transaction record
        public (bool isRuleApplied, TransactionDataModel transaction) ApplyRuleToTransaction(ulong userId, TransactionDataModel transaction, TransactionRule transactionRule, string? connString)
        {

            (bool isRuleApplied, TransactionDataModel transaction) transactionForRuleApplication = (false, transaction);

            if (transactionRule.AccountId == null || transactionRule.AccountId == transaction.AccountId)    // Apply Rule if condition is "All Accounts" or "Matching Account Only"
            {
                transactionForRuleApplication = ApplyRuleValues(transactionRule, transaction);
            }

            return transactionForRuleApplication;

        }


        // Complete
        // Apply all the rules for their matching transactions records!
        // If there is any bulk upload, this function will be used!!
        public (bool isRuleApplied, TransactionDataModel transaction) ApplyAllRulesToTransaction(ulong userId, TransactionDataModel transaction, string? connString)
        {

            bool isAnyRuleApplied = false;
            (bool isRuleApplied, TransactionDataModel transaction) transactionForRuleApplication = (false, transaction);

            var _transactionRulesForCurrentUser = _transactionRulesRepository.GetTransactionRules(userId, 0, 0, connString);
            
            // Apply rules
            foreach (var transactionRule in _transactionRulesForCurrentUser)
            {

                transactionForRuleApplication = ApplyRuleToTransaction(userId, transactionForRuleApplication.transaction, transactionRule, connString);

                // If any rule has been applied!!!
                if(transactionForRuleApplication.isRuleApplied)
                {
                    isAnyRuleApplied = true;
                }

            }

            // If any rule has been applied, that means the transaction record has changed and must be updated in database!!!
            if (isAnyRuleApplied)
            {
                transactionForRuleApplication.isRuleApplied = true;
            }

            return transactionForRuleApplication;

        }

        

        /*
        // Complete.. deleted due to circular reference!
        // Applies the supplied rule (Single Rule) to the existing (CSV/Bank) transactions. Does not change the Manual Entries
        public void ApplyRuleToExistingTransactions(ulong userId, IEnumerable<TransactionDataModel> allTransactions, TransactionRule transactionRule, string? connString) 
        {
            // var allTransactions = _transactionRepository.GetCSVAndBankLinkTransactionsForUser(userId, connString);

            // Filter the transaction records as per Account!
            var transactions = transactionRule.AccountId == "" ? allTransactions : allTransactions.Where(t => t.AccountId == transactionRule.AccountId);

            foreach (var transaction in transactions)
            {
                var transactionWithRuleApplied = ApplyRuleValues(transactionRule, transaction);
                _transactionRepository.UpdateTransactionRecordFromRule(userId, transactionWithRuleApplied, connString);
            }

        }
        */



    }
}
