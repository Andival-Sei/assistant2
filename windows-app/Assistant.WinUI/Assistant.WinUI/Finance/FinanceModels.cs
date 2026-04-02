using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Assistant.WinUI.Finance
{
    internal sealed class FinanceOverview
    {
        [JsonPropertyName("onboardingCompleted")]
        public bool OnboardingCompleted { get; set; }

        [JsonPropertyName("defaultCurrency")]
        public string? DefaultCurrency { get; set; }

        [JsonPropertyName("overviewCards")]
        public List<string> OverviewCards { get; set; } = new();

        [JsonPropertyName("totalBalanceMinor")]
        public long TotalBalanceMinor { get; set; }

        [JsonPropertyName("cardBalanceMinor")]
        public long CardBalanceMinor { get; set; }

        [JsonPropertyName("cashBalanceMinor")]
        public long CashBalanceMinor { get; set; }

        [JsonPropertyName("creditDebtMinor")]
        public long CreditDebtMinor { get; set; }

        [JsonPropertyName("creditAvailableMinor")]
        public long CreditAvailableMinor { get; set; }

        [JsonPropertyName("creditLimitMinor")]
        public long CreditLimitMinor { get; set; }

        [JsonPropertyName("creditSpendMinor")]
        public long CreditSpendMinor { get; set; }

        [JsonPropertyName("monthIncomeMinor")]
        public long MonthIncomeMinor { get; set; }

        [JsonPropertyName("monthExpenseMinor")]
        public long MonthExpenseMinor { get; set; }

        [JsonPropertyName("monthNetMinor")]
        public long MonthNetMinor { get; set; }

        [JsonPropertyName("accounts")]
        public List<FinanceAccount> Accounts { get; set; } = new();

        [JsonPropertyName("recentTransactions")]
        public List<FinanceTransaction> RecentTransactions { get; set; } = new();

        [JsonPropertyName("categoriesCount")]
        public int CategoriesCount { get; set; }
    }

    internal sealed class FinanceAccount
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("bankName")]
        public string? BankName { get; set; }

        [JsonPropertyName("providerCode")]
        public string ProviderCode { get; set; } = string.Empty;

        [JsonPropertyName("cardType")]
        public string? CardType { get; set; }

        [JsonPropertyName("lastFourDigits")]
        public string? LastFourDigits { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "RUB";

        [JsonPropertyName("balanceMinor")]
        public long BalanceMinor { get; set; }

        [JsonPropertyName("creditLimitMinor")]
        public long? CreditLimitMinor { get; set; }

        [JsonPropertyName("creditDebtMinor")]
        public long? CreditDebtMinor { get; set; }

        [JsonPropertyName("creditAvailableMinor")]
        public long? CreditAvailableMinor { get; set; }

        [JsonPropertyName("creditRequiredPaymentMinor")]
        public long? CreditRequiredPaymentMinor { get; set; }

        [JsonPropertyName("creditPaymentDueDate")]
        public DateTimeOffset? CreditPaymentDueDate { get; set; }

        [JsonPropertyName("creditGracePeriodEndDate")]
        public DateTimeOffset? CreditGracePeriodEndDate { get; set; }

        [JsonPropertyName("isPrimary")]
        public bool IsPrimary { get; set; }

        [JsonPropertyName("transactionCount")]
        public int TransactionCount { get; set; }

        [JsonPropertyName("balanceEditable")]
        public bool BalanceEditable { get; set; }
    }

    internal sealed class FinanceTransaction
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("accountId")]
        public Guid AccountId { get; set; }

        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = string.Empty;

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("transactionKind")]
        public string TransactionKind { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("merchantName")]
        public string? MerchantName { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("amountMinor")]
        public long AmountMinor { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "RUB";

        [JsonPropertyName("happenedAt")]
        public DateTimeOffset HappenedAt { get; set; }

        [JsonPropertyName("destinationAccountId")]
        public Guid? DestinationAccountId { get; set; }

        [JsonPropertyName("destinationAccountName")]
        public string? DestinationAccountName { get; set; }

        [JsonPropertyName("categoryId")]
        public Guid? CategoryId { get; set; }

        [JsonPropertyName("categoryName")]
        public string? CategoryName { get; set; }

        [JsonPropertyName("itemCount")]
        public int ItemCount { get; set; }

        [JsonPropertyName("sourceType")]
        public string SourceType { get; set; } = "manual";

        [JsonPropertyName("receiptStoragePath")]
        public string? ReceiptStoragePath { get; set; }

        [JsonPropertyName("items")]
        public List<FinanceTransactionItem> Items { get; set; } = new();
    }

    internal sealed class FinanceTransactionItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("amountMinor")]
        public long AmountMinor { get; set; }

        [JsonPropertyName("categoryId")]
        public Guid? CategoryId { get; set; }

        [JsonPropertyName("categoryName")]
        public string? CategoryName { get; set; }

        [JsonPropertyName("categoryCode")]
        public string? CategoryCode { get; set; }

        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }
    }

    internal sealed class FinanceTransactionItemDraft
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("amountMinor")]
        public long AmountMinor { get; set; }

        [JsonPropertyName("categoryId")]
        public Guid? CategoryId { get; set; }
    }

    internal sealed class FinanceCreateTransactionRequest
    {
        [JsonPropertyName("clientRequestId")]
        public Guid ClientRequestId { get; set; }

        [JsonPropertyName("accountId")]
        public Guid AccountId { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("amountMinor")]
        public long? AmountMinor { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "RUB";

        [JsonPropertyName("happenedAt")]
        public DateTimeOffset? HappenedAt { get; set; }

        [JsonPropertyName("categoryId")]
        public Guid? CategoryId { get; set; }

        [JsonPropertyName("items")]
        public List<FinanceTransactionItemDraft> Items { get; set; } = new();

        [JsonPropertyName("destinationAccountId")]
        public Guid? DestinationAccountId { get; set; }

        [JsonPropertyName("sourceType")]
        public string SourceType { get; set; } = "manual";

        [JsonPropertyName("merchantName")]
        public string? MerchantName { get; set; }
    }

    internal sealed class FinanceCategory
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("parentId")]
        public Guid? ParentId { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }
    }

    internal sealed class FinanceTransactionsMonth
    {
        [JsonPropertyName("month")]
        public string Month { get; set; } = string.Empty;

        [JsonPropertyName("availableMonths")]
        public List<string> AvailableMonths { get; set; } = new();

        [JsonPropertyName("transactions")]
        public List<FinanceTransaction> Transactions { get; set; } = new();
    }

    internal sealed class FinanceImportDraftItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("amountMinor")]
        public long AmountMinor { get; set; }

        [JsonPropertyName("suggestedCategoryCode")]
        public string? SuggestedCategoryCode { get; set; }

        [JsonPropertyName("suggestedCategoryId")]
        public Guid? SuggestedCategoryId { get; set; }

        [JsonPropertyName("suggestedCategoryName")]
        public string? SuggestedCategoryName { get; set; }

        [JsonPropertyName("suggestedCategoryPath")]
        public string? SuggestedCategoryPath { get; set; }
    }

    internal sealed class FinanceImportDraft
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("merchantName")]
        public string? MerchantName { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "expense";

        [JsonPropertyName("transactionKind")]
        public string TransactionKind { get; set; } = "single";

        [JsonPropertyName("amountMinor")]
        public long AmountMinor { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "RUB";

        [JsonPropertyName("happenedAt")]
        public string? HappenedAt { get; set; }

        [JsonPropertyName("sourceType")]
        public string SourceType { get; set; } = "file";

        [JsonPropertyName("documentKind")]
        public string DocumentKind { get; set; } = "image";

        [JsonPropertyName("items")]
        public List<FinanceImportDraftItem> Items { get; set; } = new();
    }

    internal sealed class FinanceImportResult
    {
        [JsonPropertyName("drafts")]
        public List<FinanceImportDraft> Drafts { get; set; } = new();

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();

        [JsonPropertyName("documentKind")]
        public string DocumentKind { get; set; } = "image";

        [JsonPropertyName("sourceType")]
        public string SourceType { get; set; } = "file";

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("storagePath")]
        public string StoragePath { get; set; } = string.Empty;
    }
}
