using PersonalFinanceTracker.Models;
using System.Net;
using System.Net.Mail;
using TransactionDataModel = PersonalFinanceTracker.Models.Transaction;

namespace PersonalFinanceTracker.Services.Contracts
{
    public interface ITransactionRuleService
    {
        public (bool isRuleApplied, TransactionDataModel transaction) ApplyAllRulesToTransaction(ulong userId, TransactionDataModel transaction, string? connString);
        public (bool isRuleApplied, TransactionDataModel transaction) ApplyRuleToTransaction(ulong userId, TransactionDataModel transaction, TransactionRule transactionRule, string? connString);

    }
}