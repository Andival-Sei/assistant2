using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Assistant.WinUI.Auth;
using Assistant.WinUI.Finance;
using Assistant.WinUI.Settings;
using Assistant.WinUI.Storage;
using AuthInputValidator = Assistant.WinUI.Application.Auth.AuthInputValidator;
using AppLocalizationService = Assistant.WinUI.Application.Abstractions.ILocalizationService;
using AppDashboardSection = Assistant.WinUI.Application.Shell.DashboardSection;
using AppShellNavItem = Assistant.WinUI.Application.Shell.ShellNavItem;
using AppShellSectionConfig = Assistant.WinUI.Application.Shell.ShellSectionConfig;
using AppShellViewModel = Assistant.WinUI.Application.Shell.ShellViewModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.System;
using WinRT.Interop;

namespace Assistant.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private void RenderFinanceAccounts(FinanceOverview overview)
        {
            FinanceAccountsPanel.Children.Clear();

            if (overview.Accounts.Count == 0)
            {
                FinanceAccountsPanel.Children.Add(CreateMutedText(_isRussian ? "Счета пока не добавлены." : "No accounts yet."));
                return;
            }

            foreach (var account in overview.Accounts)
            {
                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(24),
                    Padding = new Thickness(18, 16, 18, 16)
                };

                var layout = new Grid { ColumnSpacing = 16 };
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var primaryStack = new StackPanel { Spacing = 12 };

                var header = new Grid { ColumnSpacing = 12 };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var iconSurface = new Border
                {
                    Width = 42,
                    Height = 42,
                    CornerRadius = new CornerRadius(14),
                    Background = new SolidColorBrush(Color.FromArgb(
                        44,
                        GetFinanceAccountAccent(account).R,
                        GetFinanceAccountAccent(account).G,
                        GetFinanceAccountAccent(account).B)),
                    Child = new FontIcon
                    {
                        Glyph = GetFinanceAccountGlyph(account),
                        FontSize = 16,
                        FontFamily = new FontFamily("Segoe Fluent Icons"),
                        Foreground = new SolidColorBrush(GetFinanceAccountAccent(account)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                header.Children.Add(iconSurface);

                var titleStack = new StackPanel
                {
                    Spacing = 4,
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleStack.Children.Add(new TextBlock
                {
                    Text = GetFinanceAccountTitle(account),
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable Text Semibold"),
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
                titleStack.Children.Add(CreateMutedText(GetFinanceAccountSubtitle(account)));
                Grid.SetColumn(titleStack, 1);
                header.Children.Add(titleStack);

                var actionsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (IsLoanAccount(account))
                {
                    var payButton = CreateFinanceMiniButton(_isRussian ? "Платёж" : "Payment");
                    payButton.Click += async (_, _) => await ShowLoanPaymentDialogAsync(account.Id);
                    actionsPanel.Children.Add(payButton);
                }

                var editButton = CreateFinanceMiniButton(_isRussian ? "Изменить" : "Edit");
                editButton.Click += async (_, _) => await ShowAccountDialogAsync(account);
                actionsPanel.Children.Add(editButton);
                Grid.SetColumn(actionsPanel, 2);
                header.Children.Add(actionsPanel);

                primaryStack.Children.Add(header);
                var badges = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };
                badges.Children.Add(CreateFinanceBadge(
                    account.Kind == "cash"
                        ? (_isRussian ? "Наличные" : "Cash")
                        : IsLoanAccount(account)
                            ? GetLoanKindLabel()
                        : GetFinanceCardTypeLabel(account.CardType)));
                if (!string.IsNullOrWhiteSpace(account.LastFourDigits))
                {
                    badges.Children.Add(CreateFinanceBadge($"•••• {account.LastFourDigits}"));
                }
                badges.Children.Add(CreateFinanceBadge(_isRussian
                    ? $"Операций: {account.TransactionCount}"
                    : $"Transactions: {account.TransactionCount}"));
                if (account.IsPrimary)
                {
                    badges.Children.Add(CreateFinanceBadge(_isRussian ? "Основная" : "Primary", true));
                }
                primaryStack.Children.Add(badges);

                var detailsRow = new Grid { ColumnSpacing = 10 };
                detailsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                detailsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textColumn = new StackPanel { Spacing = 6 };
                textColumn.Children.Add(CreateMutedText(GetFinanceAccountSummaryText(account)));
                var auxiliaryText = GetFinanceAccountAuxiliaryText(account);
                if (!string.IsNullOrWhiteSpace(auxiliaryText))
                {
                    textColumn.Children.Add(CreateMutedText(auxiliaryText));
                }
                detailsRow.Children.Add(textColumn);

                var balanceBlock = new StackPanel
                {
                    Spacing = 2,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                balanceBlock.Children.Add(new TextBlock
                {
                    Text = GetFinanceAccountPrimaryCaption(account),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                });
                balanceBlock.Children.Add(new TextBlock
                {
                    Text = GetFinanceAccountPrimaryAmount(account),
                    FontSize = 24,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
                balanceBlock.Children.Add(new TextBlock
                {
                    Text = account.Currency,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                });
                Grid.SetColumn(balanceBlock, 1);
                detailsRow.Children.Add(balanceBlock);

                primaryStack.Children.Add(detailsRow);
                layout.Children.Add(primaryStack);
                card.Child = layout;
                FinanceAccountsPanel.Children.Add(card);
            }
        }

        private string BuildCategoriesSummary()
        {
            if (_financeCategories.Count == 0)
            {
                return _isRussian
                    ? "Категории пока не загружены или ещё не созданы."
                    : "Categories are not loaded yet or have not been created.";
            }

            var expenseRoots = _financeCategories.Count(item => string.Equals(item.Direction, "expense", StringComparison.OrdinalIgnoreCase) && item.ParentId == null);
            var incomeRoots = _financeCategories.Count(item => string.Equals(item.Direction, "income", StringComparison.OrdinalIgnoreCase) && item.ParentId == null);
            return _isRussian
                ? $"Расходы: {expenseRoots} корневых категорий. Доходы: {incomeRoots} корневых категорий."
                : $"Expenses: {expenseRoots} root categories. Income: {incomeRoots} root categories.";
        }

        private void RenderFinanceTransactions(FinanceTransactionsMonth month)
        {
            FinanceTransactionsPanel.Children.Clear();
            FinanceTransactionsTitle.Text = _isRussian
                ? (_financeTab == FinanceTab.Overview ? "Последние транзакции" : "Транзакции")
                : (_financeTab == FinanceTab.Overview ? "Recent transactions" : "Transactions");

            if (_financeTab == FinanceTab.Transactions)
            {
                var selectedMonth = _financeSelectedTransactionsMonth ?? month.Month;
                var availableMonths = month.AvailableMonths
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(item => item, StringComparer.Ordinal)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(selectedMonth) &&
                    availableMonths.All(item => !string.Equals(item, selectedMonth, StringComparison.OrdinalIgnoreCase)))
                {
                    availableMonths.Insert(0, selectedMonth);
                }

                var filterCard = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(20),
                    Padding = new Thickness(16, 14, 16, 14)
                };

                var filterStack = new StackPanel { Spacing = 10 };
                var filterHeader = new Grid { ColumnSpacing = 12 };
                filterHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                filterHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var filterLabelStack = new StackPanel { Spacing = 4 };
                filterLabelStack.Children.Add(new TextBlock
                {
                    Text = _isRussian ? "Период" : "Period",
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                });
                filterLabelStack.Children.Add(new TextBlock
                {
                    Text = FormatFinanceMonthLabel(selectedMonth),
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
                filterHeader.Children.Add(filterLabelStack);

                var monthMeta = CreateMutedText(_financeTransactionsMonthLoading
                    ? (_isRussian ? "Обновляем список транзакций…" : "Refreshing the transaction list…")
                    : (_isRussian
                        ? $"Доступно месяцев: {Math.Max(availableMonths.Count, 1)}"
                        : $"Available months: {Math.Max(availableMonths.Count, 1)}"));
                monthMeta.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(monthMeta, 1);
                filterHeader.Children.Add(monthMeta);
                filterStack.Children.Add(filterHeader);

                if (availableMonths.Count > 1)
                {
                    var monthCombo = new ComboBox
                    {
                        PlaceholderText = _isRussian ? "Выберите месяц" : "Choose month",
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        IsEnabled = !_financeTransactionsMonthLoading
                    };

                    foreach (var availableMonth in availableMonths)
                    {
                        monthCombo.Items.Add(new ComboBoxItem
                        {
                            Tag = availableMonth,
                            Content = FormatFinanceMonthLabel(availableMonth)
                        });
                    }

                    for (var i = 0; i < monthCombo.Items.Count; i++)
                    {
                        if (monthCombo.Items[i] is ComboBoxItem item &&
                            string.Equals(item.Tag as string, selectedMonth, StringComparison.OrdinalIgnoreCase))
                        {
                            monthCombo.SelectedIndex = i;
                            break;
                        }
                    }

                    monthCombo.SelectionChanged += async (_, _) =>
                    {
                        if (_financeTransactionsMonthLoading || monthCombo.SelectedItem is not ComboBoxItem selectedItem)
                        {
                            return;
                        }

                        var nextMonth = selectedItem.Tag as string;
                        if (string.Equals(nextMonth, _financeSelectedTransactionsMonth, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        await LoadFinanceTransactionsMonthAsync(nextMonth);
                    };

                    filterStack.Children.Add(monthCombo);
                }
                else
                {
                    filterStack.Children.Add(CreateMutedText(_isRussian
                        ? "Пока доступен только один месяц с транзакциями."
                        : "Only one month with transactions is available so far."));
                }

                filterCard.Child = filterStack;
                FinanceTransactionsPanel.Children.Add(filterCard);
            }

            if (month.Transactions.Count == 0)
            {
                FinanceTransactionsPanel.Children.Add(CreateMutedText(_isRussian ? "Транзакций пока нет. Следующим этапом подключим ввод операций." : "No transactions yet. Transaction input comes next."));
                return;
            }

            foreach (var transaction in month.Transactions)
            {
                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(22),
                    Padding = new Thickness(16, 14, 16, 14)
                };

                var stack = new StackPanel { Spacing = 12 };

                var header = new Grid { ColumnSpacing = 12 };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var iconSurface = new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromArgb(
                        44,
                        GetTransactionAccent(transaction).R,
                        GetTransactionAccent(transaction).G,
                        GetTransactionAccent(transaction).B)),
                    Child = new FontIcon
                    {
                        Glyph = GetTransactionGlyph(transaction),
                        FontFamily = new FontFamily("Segoe Fluent Icons"),
                        FontSize = 14,
                        Foreground = new SolidColorBrush(GetTransactionAccent(transaction)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                header.Children.Add(iconSurface);

                var titleStack = new StackPanel { Spacing = 2 };
                titleStack.Children.Add(new TextBlock
                {
                    Text = transaction.Title,
                    FontSize = 15,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable Text Semibold"),
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
                titleStack.Children.Add(new TextBlock
                {
                    Text = GetTransactionSecondaryLine(transaction),
                    FontSize = 12,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                    TextWrapping = TextWrapping.Wrap
                });
                Grid.SetColumn(titleStack, 1);
                header.Children.Add(titleStack);

                var amountPrefix = string.Equals(transaction.Direction, "expense", StringComparison.OrdinalIgnoreCase) ? "-" : "+";
                var amountStack = new StackPanel
                {
                    Spacing = 2,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                amountStack.Children.Add(new TextBlock
                {
                    Text = $"{amountPrefix}{FormatMoney(transaction.AmountMinor, transaction.Currency)}",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(GetTransactionAccent(transaction)),
                    HorizontalAlignment = HorizontalAlignment.Right
                });
                amountStack.Children.Add(new TextBlock
                {
                    Text = $"{transaction.HappenedAt:dd.MM.yyyy HH:mm}",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                });
                Grid.SetColumn(amountStack, 2);
                header.Children.Add(amountStack);
                stack.Children.Add(header);

                var badgesRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };

                if (!string.IsNullOrWhiteSpace(transaction.CategoryName))
                {
                    badgesRow.Children.Add(CreateFinanceBadge(transaction.CategoryName!));
                }

                if (!string.IsNullOrWhiteSpace(transaction.AccountName))
                {
                    badgesRow.Children.Add(CreateFinanceBadge(transaction.AccountName));
                }

                badgesRow.Children.Add(CreateFinanceBadge(GetTransactionSourceLabel(transaction.SourceType)));

                Border? positionsPanel = null;
                if (transaction.ItemCount > 0 || transaction.Items.Count > 0)
                {
                    var positionsLabel = _isRussian
                        ? $"Позиции: {Math.Max(transaction.ItemCount, transaction.Items.Count)}"
                        : $"Items: {Math.Max(transaction.ItemCount, transaction.Items.Count)}";
                    var positionsButton = CreateFinanceBadgeButton(positionsLabel, true);
                    positionsButton.Click += (_, _) =>
                    {
                        if (positionsPanel != null)
                        {
                            ToggleTransactionPositionsPanel(positionsPanel);
                        }
                    };
                    badgesRow.Children.Add(positionsButton);
                }

                stack.Children.Add(badgesRow);

                if (!string.IsNullOrWhiteSpace(transaction.Note))
                {
                    stack.Children.Add(new Border
                    {
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(12, 10, 12, 10),
                        Child = new TextBlock
                        {
                            Text = transaction.Note,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                        }
                    });
                }

                if (transaction.ItemCount > 0 || transaction.Items.Count > 0)
                {
                    positionsPanel = new Border
                    {
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                        BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(16),
                        Padding = new Thickness(12, 12, 12, 12),
                        Visibility = Visibility.Collapsed
                    };

                    var itemsStack = new StackPanel { Spacing = 8 };
                    if (transaction.Items.Count == 0)
                    {
                        itemsStack.Children.Add(CreateMutedText(_isRussian
                            ? "Позиции для этой транзакции сейчас недоступны."
                            : "Item details are not available for this transaction yet."));
                    }
                    else
                    {
                        foreach (var item in transaction.Items.OrderBy(item => item.DisplayOrder))
                        {
                            var itemRow = new Border
                            {
                                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                                CornerRadius = new CornerRadius(14),
                                Padding = new Thickness(12, 10, 12, 10)
                            };

                            var itemGrid = new Grid { ColumnSpacing = 12 };
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            var itemTextStack = new StackPanel { Spacing = 2 };
                            itemTextStack.Children.Add(new TextBlock
                            {
                                Text = string.IsNullOrWhiteSpace(item.Title) ? (_isRussian ? "Позиция" : "Item") : item.Title,
                                FontSize = 13,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                            });
                            if (!string.IsNullOrWhiteSpace(item.CategoryName))
                            {
                                itemTextStack.Children.Add(new TextBlock
                                {
                                    Text = item.CategoryName,
                                    FontSize = 12,
                                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                                });
                            }
                            itemGrid.Children.Add(itemTextStack);

                            var itemAmount = new TextBlock
                            {
                                Text = FormatMoney(item.AmountMinor, transaction.Currency),
                                FontSize = 13,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Grid.SetColumn(itemAmount, 1);
                            itemGrid.Children.Add(itemAmount);

                            itemRow.Child = itemGrid;
                            itemsStack.Children.Add(itemRow);
                        }
                    }

                    positionsPanel.Child = itemsStack;
                    stack.Children.Add(positionsPanel);
                }

                card.Child = stack;
                FinanceTransactionsPanel.Children.Add(card);
            }
        }


        private string GetOverviewCardGlyph(string cardId) => cardId switch
        {
            "total_balance" => "\uE8C7",
            "card_balance" => "\uE8C7",
            "cash_balance" => "\uEAFD",
            "credit_debt" => "\uE8C7",
            "loan_debt" => "\uE8C7",
            "loan_full_debt" => "\uE8C7",
            "credit_spend" => "\uE8AB",
            "month_income" => "\uE74A",
            "month_expense" => "\uE74B",
            "month_result" => "\uE9D9",
            _ => "\uE9D2"
        };

        private UIElement CreateOverviewCardIcon(string cardId)
        {
            var accent = new SolidColorBrush(GetOverviewCardAccent(cardId));
            if (string.Equals(cardId, "cash_balance", StringComparison.OrdinalIgnoreCase))
            {
                return new TextBlock
                {
                    Text = "\u20BD",
                    FontSize = 15,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            return new FontIcon
            {
                Glyph = GetOverviewCardGlyph(cardId),
                FontSize = 15,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Foreground = accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private string GetOverviewCardEyebrow(string cardId) => cardId switch
        {
            "total_balance" => _isRussian ? "Все активы под рукой" : "All assets at a glance",
            "card_balance" => _isRussian ? "Только собственные деньги на счетах" : "Only your own money on cards",
            "cash_balance" => _isRussian ? "Свободные наличные" : "Cash on hand",
            "credit_debt" => _isRussian ? "Текущая задолженность по кредиткам" : "Current liability on credit cards",
            "loan_debt" => _isRussian ? "Остаток долга по кредитам" : "Current outstanding loan debt",
            "loan_full_debt" => _isRussian ? "Сумма всех оставшихся платежей по графику кредита" : "All remaining scheduled loan payments",
            "credit_spend" => _isRussian ? "Покупки, которые увеличили долг по кредиткам за этот месяц" : "Purchases that increased credit card debt this month",
            "month_income" => _isRussian ? "Входящий поток за месяц по собственным деньгам" : "Monthly incoming flow from your own funds",
            "month_expense" => _isRussian ? "Расходы за месяц по собственным деньгам" : "Monthly spending from your own funds",
            "month_result" => _isRussian ? "Чистый итог периода по собственным деньгам" : "Net period result from your own funds",
            _ => string.Empty
        };

        private Border CreateFinanceAnalyticsMonthsCard(FinanceAnalyticsSnapshot snapshot, string currency)
        {
            var content = new StackPanel { Spacing = 10 };
            foreach (var item in snapshot.MonthSummaries.OrderByDescending(item => item.Month, StringComparer.Ordinal))
            {
                var row = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 12, 14, 12)
                };

                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                grid.Children.Add(new TextBlock
                {
                    Text = FormatFinanceMonthLabel(item.Month),
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                });

                var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                badges.Children.Add(CreateFinanceBadge((_isRussian ? "Доходы " : "Income ") + FormatMoney(item.OwnIncomeMinor, currency)));
                badges.Children.Add(CreateFinanceBadge((_isRussian ? "Расходы " : "Expenses ") + FormatMoney(item.OwnExpenseMinor, currency)));
                if (item.CreditExpenseMinor > 0)
                {
                    badges.Children.Add(CreateFinanceBadge((_isRussian ? "Кредит " : "Credit ") + FormatMoney(item.CreditExpenseMinor, currency)));
                }

                Grid.SetColumn(badges, 1);
                grid.Children.Add(badges);

                var count = CreateMutedText(_isRussian ? $"{item.Count} операций" : $"{item.Count} transactions");
                count.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(count, 2);
                grid.Children.Add(count);

                row.Child = grid;
                content.Children.Add(row);
            }

            return CreateAnalyticsSectionCard(
                _isRussian ? "Динамика по месяцам" : "Month dynamics",
                _isRussian ? "Быстро видно, где был пик трат и когда усилился кредитный поток." : "Quickly spot heavy spending and stronger credit flow.",
                content);
        }

        private Border CreateFinanceAnalyticsBreakdownCard(string title, string subtitle, IReadOnlyList<FinanceAnalyticsSlice> slices, string currency, string emptyState)
        {
            var content = new StackPanel { Spacing = 10 };
            if (slices.Count == 0)
            {
                content.Children.Add(CreateMutedText(emptyState));
            }
            else
            {
                foreach (var slice in slices)
                {
                    content.Children.Add(CreateFinanceAnalyticsSliceRow(slice, currency));
                }
            }

            return CreateAnalyticsSectionCard(title, subtitle, content);
        }

        private Border CreateFinanceAnalyticsSourceCard(FinanceAnalyticsSnapshot snapshot)
        {
            var content = new StackPanel { Spacing = 10 };
            if (snapshot.Sources.Count == 0)
            {
                content.Children.Add(CreateMutedText(_isRussian ? "Пока нет данных об источниках добавления." : "No source data yet."));
            }
            else
            {
                foreach (var slice in snapshot.Sources)
                {
                    content.Children.Add(CreateFinanceAnalyticsSourceRow(slice));
                }
            }

            return CreateAnalyticsSectionCard(
                _isRussian ? "Как добавляли транзакции" : "How transactions were added",
                _isRussian ? "Помогает понять, насколько активно используются импорт и ручной ввод." : "Shows how often imports and manual entry are used.",
                content);
        }

        private Border CreateFinanceAnalyticsSliceRow(FinanceAnalyticsSlice slice, string currency)
        {
            var rowStack = new StackPanel { Spacing = 8 };
            var header = new Grid { ColumnSpacing = 10 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelStack = new StackPanel { Spacing = 2 };
            labelStack.Children.Add(new TextBlock
            {
                Text = slice.Label,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });
            if (!string.IsNullOrWhiteSpace(slice.Subtitle))
            {
                labelStack.Children.Add(CreateMutedText(slice.Subtitle));
            }
            header.Children.Add(labelStack);

            var valueText = new TextBlock
            {
                Text = FormatMoney(slice.AmountMinor, currency),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valueText, 1);
            header.Children.Add(valueText);
            rowStack.Children.Add(header);

            rowStack.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = 1,
                Value = Math.Max(0.04, slice.Share),
                Height = 6,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0x54, 0xC1, 0x7B))
            });

            return new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 12, 14, 12),
                Child = rowStack
            };
        }

        private Border CreateFinanceAnalyticsSourceRow(FinanceAnalyticsSlice slice)
        {
            var rowStack = new StackPanel { Spacing = 8 };
            var header = new Grid { ColumnSpacing = 10 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            header.Children.Add(new TextBlock
            {
                Text = slice.Label,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });

            var countText = new TextBlock
            {
                Text = _isRussian ? $"{slice.Count} шт." : $"{slice.Count} items",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(countText, 1);
            header.Children.Add(countText);
            rowStack.Children.Add(header);

            rowStack.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = 1,
                Value = Math.Max(0.04, slice.Share),
                Height = 6
            });

            return new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 12, 14, 12),
                Child = rowStack
            };
        }

        private Border CreateAnalyticsSectionCard(string? title, string? subtitle, UIElement content, Thickness? padding = null)
        {
            var stack = new StackPanel { Spacing = 10 };
            if (!string.IsNullOrWhiteSpace(title))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
            }

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                stack.Children.Add(CreateMutedText(subtitle));
            }

            stack.Children.Add(content);

            return new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(0.6),
                CornerRadius = new CornerRadius(24),
                Padding = padding ?? new Thickness(18, 18, 18, 18),
                Shadow = (Shadow)Microsoft.UI.Xaml.Application.Current.Resources["CardThemeShadow"],
                Child = stack
            };
        }

        private string BuildFinanceAnalyticsHeadline(FinanceAnalyticsSnapshot snapshot, string currency) =>
            snapshot.NetOwnFlowMinor >= 0
                ? (_isRussian
                    ? $"За период собственный денежный поток в плюсе на {FormatMoney(snapshot.NetOwnFlowMinor, currency)}."
                    : $"Your own cashflow is positive by {FormatMoney(snapshot.NetOwnFlowMinor, currency)} for the period.")
                : (_isRussian
                    ? $"За период собственный денежный поток ушёл в минус на {FormatMoney(Math.Abs(snapshot.NetOwnFlowMinor), currency)}."
                    : $"Your own cashflow is down by {FormatMoney(Math.Abs(snapshot.NetOwnFlowMinor), currency)} for the period.");

        private string BuildFinanceAnalyticsSubheadline(FinanceAnalyticsSnapshot snapshot, string currency)
        {
            var segments = new List<string>();
            if (snapshot.ExpenseCategories.Count > 0)
            {
                var top = snapshot.ExpenseCategories[0];
                segments.Add(_isRussian
                    ? $"Главная категория расходов: {top.Label} ({FormatMoney(top.AmountMinor, currency)})"
                    : $"Top expense category: {top.Label} ({FormatMoney(top.AmountMinor, currency)})");
            }

            if (snapshot.CreditExpenseMinor > 0)
            {
                segments.Add(_isRussian
                    ? $"По кредиткам потрачено {FormatMoney(snapshot.CreditExpenseMinor, currency)}"
                    : $"Credit card spending reached {FormatMoney(snapshot.CreditExpenseMinor, currency)}");
            }

            if (snapshot.LargestPurchaseMinor > 0)
            {
                segments.Add(_isRussian
                    ? $"Крупнейшая трата: {snapshot.LargestPurchaseTitle} на {FormatMoney(snapshot.LargestPurchaseMinor, currency)}"
                    : $"Largest purchase: {snapshot.LargestPurchaseTitle} for {FormatMoney(snapshot.LargestPurchaseMinor, currency)}");
            }

            return segments.Count == 0
                ? (_isRussian ? "Как только появится больше операций, здесь будут заметные паттерны и подсказки." : "Once more transactions appear, this area will surface useful patterns.")
                : string.Join(". ", segments);
        }

        private TextBlock CreateMutedText(string text) => new()
        {
            Text = text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
        };

        private static bool IsCreditAccount(FinanceAccount account) =>
            string.Equals(account.Kind, "bank_card", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(account.CardType, "credit", StringComparison.OrdinalIgnoreCase);

        private string GetFinanceAccountPrimaryAmount(FinanceAccount account) =>
            IsLoanAccount(account)
                ? FormatMoney(-(account.LoanCurrentDebtMinor ?? account.BalanceMinor), account.Currency)
                : IsCreditAccount(account)
                ? FormatMoney(-(account.CreditDebtMinor ?? 0), account.Currency)
                : FormatMoney(account.BalanceMinor, account.Currency);

        private string GetFinanceAccountPrimaryCaption(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian ? "Доступно сейчас" : "Available now";
            }

            if (IsLoanAccount(account))
            {
                return _isRussian ? "Остаток долга" : "Outstanding debt";
            }

            if (IsCreditAccount(account))
            {
                return _isRussian ? "Текущий долг" : "Current debt";
            }

            return _isRussian ? "Баланс" : "Balance";
        }

        private string GetFinanceAccountSummaryText(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? "Сразу доступно для наличных операций."
                    : "Ready for cash transactions.";
            }

            if (IsLoanAccount(account))
            {
                var segments = new List<string>();
                if (account.LoanPrincipalMinor is long principalMinor)
                {
                    segments.Add((_isRussian ? "Сумма кредита" : "Principal") + ": " + FormatMoney(principalMinor, account.Currency));
                }

                if (GetLoanScheduledPaymentAmount(account) is long paymentMinor)
                {
                    segments.Add((_isRussian ? "Следующий платёж" : "Next payment") + ": " + FormatMoney(paymentMinor, account.Currency));
                }

                if (account.LoanTotalPayableMinor is long totalPayableMinor)
                {
                    segments.Add((_isRussian ? "Всего к выплате" : "Total payable") + ": " + FormatMoney(totalPayableMinor, account.Currency));
                }

                if (account.LoanInterestPercent is decimal interestPercent)
                {
                    segments.Add((_isRussian ? "Ставка" : "Rate") + ": " + interestPercent.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',') + "%");
                }

                return segments.Count == 0
                    ? (_isRussian ? "Кредит с ручным учётом долга." : "Loan tracked as a liability.")
                    : string.Join(" · ", segments);
            }

            if (IsCreditAccount(account))
            {
                var segments = new List<string>();
                if (account.CreditLimitMinor is long limitMinor)
                {
                    segments.Add((_isRussian ? "Лимит" : "Limit") + ": " + FormatMoney(limitMinor, account.Currency));
                }

                if (account.CreditAvailableMinor is long availableMinor)
                {
                    segments.Add((_isRussian ? "Доступно" : "Available") + ": " + FormatMoney(availableMinor, account.Currency));
                }

                if (account.CreditRequiredPaymentMinor is long requiredPaymentMinor && requiredPaymentMinor > 0)
                {
                    segments.Add((_isRussian ? "Минимальный платёж" : "Required payment") + ": " + FormatMoney(requiredPaymentMinor, account.Currency));
                }

                return segments.Count == 0
                    ? (_isRussian ? "Кредитная карта с отдельным учётом долга." : "Credit card tracked as a liability.")
                    : string.Join(" · ", segments);
            }

            return _isRussian
                ? "Используется в подборе счетов и ручном вводе."
                : "Available in account pickers and manual entry.";
        }

        private string? GetFinanceAccountAuxiliaryText(FinanceAccount account)
        {
            if (!IsCreditAccount(account))
            {
                if (!IsLoanAccount(account))
                {
                    return null;
                }

                var loanParts = new List<string>();
                if (account.LoanPaymentDueDate is DateTimeOffset paymentDueDate)
                {
                    loanParts.Add((_isRussian ? "Платёж до" : "Pay by") + " " + paymentDueDate.ToString("d", _isRussian ? new CultureInfo("ru-RU") : new CultureInfo("en-US")));
                }

                if (account.LoanRemainingPaymentsCount is int remainingCount)
                {
                    loanParts.Add(_isRussian ? $"Осталось платежей: {remainingCount}" : $"Payments left: {remainingCount}");
                }

                if (GetLoanRemainingPlannedAmount(account) is long remainingPlannedMinor)
                {
                    loanParts.Add((_isRussian ? "Осталось заплатить" : "Planned remaining") + " " + FormatMoney(remainingPlannedMinor, account.Currency));
                }

                return loanParts.Count == 0 ? null : string.Join(" · ", loanParts);
            }

            var parts = new List<string>();
            if (account.CreditPaymentDueDate is DateTimeOffset dueDate)
            {
                parts.Add((_isRussian ? "Внести до" : "Pay by") + " " + dueDate.ToString("d", _isRussian ? new CultureInfo("ru-RU") : new CultureInfo("en-US")));
            }

            if (account.CreditGracePeriodEndDate is DateTimeOffset graceDate)
            {
                parts.Add((_isRussian ? "Льготный период до" : "Grace until") + " " + graceDate.ToString("d", _isRussian ? new CultureInfo("ru-RU") : new CultureInfo("en-US")));
            }

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        private string GetFinanceAccountTitle(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian ? "Наличные" : "Cash";
            }

            if (IsLoanAccount(account))
            {
                return string.IsNullOrWhiteSpace(account.Name)
                    ? (_isRussian ? "Кредит" : "Loan")
                    : account.Name;
            }

            return account.BankName
                ?? GetFinanceProviderLabel(account.ProviderCode)
                ?? (_isRussian ? "Карта" : "Card");
        }

        private string GetFinanceAccountGlyph(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return "\uEAFD";
            }

            if (IsLoanAccount(account))
            {
                return "\uE8C7";
            }

            return "\uE8C7";
        }

        private Color GetFinanceAccountAccent(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(255, 214, 154, 63);
            }

            if (IsLoanAccount(account))
            {
                return Color.FromArgb(255, 255, 174, 70);
            }

            return IsCreditAccount(account)
                ? Color.FromArgb(255, 255, 111, 142)
                : Color.FromArgb(255, 92, 133, 255);
        }

        private string GetFinanceAccountSubtitle(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian ? "Физические деньги вне банковских счетов" : "Physical cash outside bank accounts";
            }

            if (IsLoanAccount(account))
            {
                var loanParts = new List<string>
                {
                    account.BankName ?? GetFinanceProviderLabel(account.ProviderCode) ?? (_isRussian ? "Банк" : "Bank")
                };
                if (account.LoanInterestPercent is decimal interestPercent)
                {
                    loanParts.Add((_isRussian ? "Ставка" : "Rate") + " " + interestPercent.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',') + "%");
                }

                if (account.LoanTotalPaymentsCount is int totalPayments &&
                    account.LoanRemainingPaymentsCount is int remainingPayments)
                {
                    loanParts.Add(_isRussian
                        ? $"Платежей: {Math.Max(totalPayments - remainingPayments, 0)}/{totalPayments}"
                        : $"Payments: {Math.Max(totalPayments - remainingPayments, 0)}/{totalPayments}");
                }

                return string.Join(" · ", loanParts.Where(part => !string.IsNullOrWhiteSpace(part)));
            }

            var parts = new List<string> { GetFinanceCardTypeLabel(account.CardType) };
            if (!string.IsNullOrWhiteSpace(account.LastFourDigits))
            {
                parts.Add($"•••• {account.LastFourDigits}");
            }

            if (IsCreditAccount(account) && account.CreditLimitMinor is long limitMinor)
            {
                parts.Add((_isRussian ? "Лимит" : "Limit") + " " + FormatMoney(limitMinor, account.Currency));
            }

            return string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private string GetFinanceAccountDisplayName(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian ? "Наличные" : "Cash";
            }

            if (IsLoanAccount(account))
            {
                var bankTitle = account.BankName ?? GetFinanceProviderLabel(account.ProviderCode) ?? (_isRussian ? "Кредит" : "Loan");
                return $"{bankTitle} · {account.Name}";
            }

            var suffix = !string.IsNullOrWhiteSpace(account.LastFourDigits)
                ? $" •••• {account.LastFourDigits}"
                : string.Empty;
            var typeSuffix = IsCreditAccount(account)
                ? (_isRussian ? " · Кредитная" : " · Credit")
                : string.Empty;
            return $"{GetFinanceAccountTitle(account)}{typeSuffix}{suffix}";
        }

        private string GetFinanceCardTypeLabel(string? cardType) => (cardType ?? "debit").ToLowerInvariant() switch
        {
            "credit" => _isRussian ? "Кредитная" : "Credit",
            _ => _isRussian ? "Дебетовая" : "Debit"
        };

        private string GetLoanKindLabel() => _isRussian ? "Кредит" : "Loan";

        private Color GetTransactionAccent(FinanceTransaction transaction)
        {
            if (string.Equals(transaction.TransactionKind, "loan_payment", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(255, 255, 174, 70);
            }

            if (string.Equals(transaction.Direction, "income", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(255, 35, 224, 138);
            }

            if (string.Equals(transaction.Direction, "transfer", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(255, 114, 160, 255);
            }

            return Color.FromArgb(255, 255, 138, 101);
        }

        private string GetTransactionGlyph(FinanceTransaction transaction)
        {
            if (string.Equals(transaction.TransactionKind, "loan_payment", StringComparison.OrdinalIgnoreCase))
            {
                return "\uE8C7";
            }

            if (string.Equals(transaction.Direction, "income", StringComparison.OrdinalIgnoreCase))
            {
                return "\uE8C8";
            }

            if (string.Equals(transaction.Direction, "transfer", StringComparison.OrdinalIgnoreCase))
            {
                return "\uE8AB";
            }

            return "\uE74B";
        }

        private string BuildTransactionMetaLine(FinanceTransaction transaction)
        {
            var parts = new List<string>();
            if (string.Equals(transaction.TransactionKind, "loan_payment", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(_isRussian ? "Погашение кредита" : "Loan payment");
            }
            if (!string.IsNullOrWhiteSpace(transaction.CategoryName))
            {
                parts.Add(transaction.CategoryName!);
            }
            if (!string.IsNullOrWhiteSpace(transaction.AccountName))
            {
                parts.Add(transaction.AccountName);
            }
            parts.Add(GetTransactionSourceLabel(transaction.SourceType));
            if (!string.Equals(transaction.TransactionKind, "loan_payment", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(_isRussian
                    ? $"{Math.Max(transaction.ItemCount, transaction.Items.Count)} поз."
                    : $"{Math.Max(transaction.ItemCount, transaction.Items.Count)} items");
            }

            return string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private string GetTransactionSecondaryLine(FinanceTransaction transaction)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(transaction.MerchantName))
            {
                parts.Add(transaction.MerchantName!);
            }
            if (!string.IsNullOrWhiteSpace(transaction.DestinationAccountName) &&
                string.Equals(transaction.TransactionKind, "loan_payment", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(_isRussian
                    ? $"Погашение кредита: {transaction.DestinationAccountName}"
                    : $"Loan payment: {transaction.DestinationAccountName}");
            }
            if (!string.IsNullOrWhiteSpace(transaction.DestinationAccountName) &&
                string.Equals(transaction.Direction, "transfer", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(_isRussian
                    ? $"Перевод на {transaction.DestinationAccountName}"
                    : $"Transfer to {transaction.DestinationAccountName}");
            }
            if (parts.Count == 0)
            {
                parts.Add(_isRussian
                    ? (string.Equals(transaction.Direction, "income", StringComparison.OrdinalIgnoreCase) ? "Поступление" : "Финансовая операция")
                    : (string.Equals(transaction.Direction, "income", StringComparison.OrdinalIgnoreCase) ? "Incoming transaction" : "Finance activity"));
            }

            return string.Join(" · ", parts);
        }

        private string? GetFinanceProviderLabel(string? providerCode) =>
            GetFinanceAccountProviders()
                .FirstOrDefault(item => string.Equals(item.Code, providerCode, StringComparison.OrdinalIgnoreCase))
                .Label;

        private static string? NormalizeLastFourDigits(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var digits = new string(raw.Where(char.IsDigit).ToArray());
            return digits.Length == 4 ? digits : null;
        }

        private string FormatMoney(long minor, string currencyCode)
        {
            var culture = _isRussian ? new CultureInfo("ru-RU") : new CultureInfo("en-US");
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();
            var amount = minor / 100m;
            try
            {
                format.CurrencySymbol = new RegionInfo(currencyCode switch
                {
                    "EUR" => "IE",
                    "USD" => "US",
                    _ => "RU"
                }).CurrencySymbol;
            }
            catch
            {
                format.CurrencySymbol = currencyCode;
            }

            if (decimal.Truncate(amount) == amount)
            {
                format.CurrencyDecimalDigits = 0;
            }

            return amount.ToString("C", format);
        }

        private static long? ParseMinor(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var normalized = raw.Replace(" ", string.Empty).Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
                ? (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero)
                : null;
        }

    }
}

