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

        [JsonPropertyName("totalBalanceMinor")]
        public long TotalBalanceMinor { get; set; }

        [JsonPropertyName("accounts")]
        public List<FinanceAccount> Accounts { get; set; } = new();

        [JsonPropertyName("recentTransactions")]
        public List<FinanceTransaction> RecentTransactions { get; set; } = new();
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

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "RUB";

        [JsonPropertyName("balanceMinor")]
        public long BalanceMinor { get; set; }

        [JsonPropertyName("isPrimary")]
        public bool IsPrimary { get; set; }
    }

    internal sealed class FinanceTransaction
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("accountId")]
        public Guid AccountId { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("amountMinor")]
        public long AmountMinor { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "RUB";

        [JsonPropertyName("happenedAt")]
        public DateTimeOffset HappenedAt { get; set; }
    }
}
