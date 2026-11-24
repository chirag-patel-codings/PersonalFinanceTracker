using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Crypto.Signers;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.Authentication;


namespace PersonalFinanceTracker.Services.Contracts
{
    public interface IPlaidService
    {
        public bool SaveLinkedAccountsFromExternalInstitute(ulong userId, List<Account> accounts, string? dataMode, string? connString);
        public string? GetPlaidPermanentAccessToken(ulong userId, string? accountId, string? connString);
        public void PlaidAccountReconsilation(ulong userId, string plaidItemId, string plaidInstitutionId, string plaidAccountId, string plaidAccountName, string plaidAccountMask, string accountTypeText, string plaidAccountSubtype, string? connString);
        public Task PerformPlaidAccountReconsilation(ulong userId, IEnumerable<Account?> linkedAccounts, string? connString);
        public string? GetPlaidNextCursorValue(ulong userId, string plaidItemId, string? connString);
        public IEnumerable<Account?> GetPlaidActiveAccountsForUser(ulong userId, string? connString);
        public Task<IActionResult> CreatePlaidLinkTokenAsync(ulong userId, string? existingAccessToken = null);
        public Task<IActionResult> SaveAccountDetailsFromPlaid(ulong userId, string plaidPublicTemporaryTokenValue, string dataMode, string? connString = null);
        public Task ImportAllLinkedAccountsDataForUser(ulong userId, IEnumerable<Account?> linkedAccounts, string? connString);
        public int GetAccountTypeId(string accountTypeName, string? connString);

    }
}