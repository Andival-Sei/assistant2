using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Assistant.WinUI
{
    public sealed partial class MainWindow
    {
        private sealed class OverviewCardOption : INotifyPropertyChanged
        {
            private bool _isSelected;

            public required string CardId { get; init; }

            public required string Label { get; init; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value)
                    {
                        return;
                    }

                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private sealed class FinanceAnalyticsSnapshot
        {
            public string FromMonth { get; init; } = string.Empty;

            public string ToMonth { get; init; } = string.Empty;

            public List<string> Months { get; init; } = new();

            public long OwnIncomeMinor { get; init; }

            public long OwnExpenseMinor { get; init; }

            public long CreditExpenseMinor { get; init; }

            public long NetOwnFlowMinor { get; init; }

            public int IncomeCount { get; init; }

            public int ExpenseCount { get; init; }

            public int CreditExpenseCount { get; init; }

            public int TransferCount { get; init; }

            public int TotalTransactionsCount { get; init; }

            public long AveragePurchaseMinor { get; init; }

            public long LargestPurchaseMinor { get; init; }

            public string? LargestPurchaseTitle { get; init; }

            public string? LargestPurchaseContext { get; init; }

            public List<FinanceAnalyticsSlice> ExpenseCategories { get; init; } = new();

            public List<FinanceAnalyticsSlice> CreditCategories { get; init; } = new();

            public List<FinanceAnalyticsSlice> Accounts { get; init; } = new();

            public List<FinanceAnalyticsSlice> Sources { get; init; } = new();

            public List<FinanceAnalyticsMonthSummary> MonthSummaries { get; init; } = new();
        }

        private sealed class FinanceAnalyticsSlice
        {
            public string Label { get; init; } = string.Empty;

            public long AmountMinor { get; init; }

            public int Count { get; init; }

            public double Share { get; set; }

            public string? Subtitle { get; init; }
        }

        private sealed class FinanceAnalyticsMonthSummary
        {
            public string Month { get; set; } = string.Empty;

            public long OwnIncomeMinor { get; set; }

            public long OwnExpenseMinor { get; set; }

            public long CreditExpenseMinor { get; set; }

            public int Count { get; set; }
        }

        private sealed class TransactionDraftItemState
        {
            public string Id { get; } = Guid.NewGuid().ToString();
            public string Title { get; set; } = string.Empty;
            public string Amount { get; set; } = string.Empty;
            public string? CategoryId { get; set; }
        }

        private sealed class TransactionDraftState
        {
            public string Id { get; } = Guid.NewGuid().ToString();
            public string SourceType { get; set; } = "manual";
            public string DocumentKind { get; set; } = "manual";
            public string Direction { get; set; } = "expense";
            public string Title { get; set; } = string.Empty;
            public string MerchantName { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
            public string AccountId { get; set; } = string.Empty;
            public string DestinationAccountId { get; set; } = string.Empty;
            public string Currency { get; set; } = "RUB";
            public string HappenedAt { get; set; } = DateTimeOffset.Now.ToString("O");
            public string TransferAmount { get; set; } = string.Empty;
            public List<TransactionDraftItemState> Items { get; set; } = new() { new TransactionDraftItemState() };
        }

        private sealed record FinanceImportIssue(
            string Error,
            List<string> Warnings);

        private enum DashboardSection
        {
            Home,
            Finance,
            Health,
            Tasks,
            Chat,
            Settings
        }

        private enum FinanceTab
        {
            Overview,
            Accounts,
            Transactions,
            Categories,
            Analytics
        }
    }
}
