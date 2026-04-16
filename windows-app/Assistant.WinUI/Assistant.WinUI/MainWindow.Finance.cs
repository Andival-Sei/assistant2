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
        private void SetFinanceTab(FinanceTab tab)
        {
            _financeTab = tab;
            _activeSubsection = tab switch
            {
                FinanceTab.Accounts => "accounts",
                FinanceTab.Transactions => "transactions",
                FinanceTab.Categories => "categories",
                FinanceTab.Analytics => "analytics",
                _ => "overview"
            };
            _shellViewModel.SetSubsection(_activeSubsection);
            ApplySecondaryTabs();
            ApplyFinanceTabButtons();
            RenderFinanceContent();

            if (tab == FinanceTab.Analytics)
            {
                _ = EnsureFinanceAnalyticsReadyAsync();
            }
        }

        private void ApplyFinanceTabButtons()
        {
            ApplyNavButtonState(FinanceOverviewTabButton, _financeTab == FinanceTab.Overview);
            ApplyNavButtonState(FinanceAccountsTabButton, _financeTab == FinanceTab.Accounts);
            ApplyNavButtonState(FinanceTransactionsTabButton, _financeTab == FinanceTab.Transactions);
            ApplyNavButtonState(FinanceCategoriesTabButton, _financeTab == FinanceTab.Categories);
            ApplyNavButtonState(FinanceAnalyticsTabButton, _financeTab == FinanceTab.Analytics);
        }

        private void PopulateFinanceInputs()
        {
            var currentCurrency = FinanceCurrencyCombo.SelectedItem as ComboBoxItem;
            var currentBank = FinanceBankCombo.SelectedItem as ComboBoxItem;
            var currentCardType = FinanceOnboardingCardTypeCombo.SelectedItem as ComboBoxItem;

            FinanceCurrencyCombo.Items.Clear();
            FinanceBankCombo.Items.Clear();
            FinanceOnboardingCardTypeCombo.Items.Clear();

            AddComboItem(FinanceCurrencyCombo, "RUB", _isRussian ? "Рубли" : "Rubles");
            AddComboItem(FinanceCurrencyCombo, "USD", _isRussian ? "Доллары" : "US Dollars");
            AddComboItem(FinanceCurrencyCombo, "EUR", _isRussian ? "Евро" : "Euro");

            AddComboItem(FinanceBankCombo, "", _isRussian ? "Пропустить" : "Skip");
            AddComboItem(FinanceBankCombo, "Т-Банк", "Т-Банк");
            AddComboItem(FinanceBankCombo, "Сбер", "Сбер");
            AddComboItem(FinanceBankCombo, "Альфа", "Альфа");
            AddComboItem(FinanceBankCombo, "ВТБ", "ВТБ");
            AddComboItem(FinanceOnboardingCardTypeCombo, "debit", _isRussian ? "Дебетовая" : "Debit");
            AddComboItem(FinanceOnboardingCardTypeCombo, "credit", _isRussian ? "Кредитная" : "Credit");

            SelectComboItemByTag(FinanceCurrencyCombo, currentCurrency?.Tag as string ?? _financeOverview?.DefaultCurrency ?? "RUB");
            SelectComboItemByTag(FinanceBankCombo, currentBank?.Tag as string ?? string.Empty);
            SelectComboItemByTag(FinanceOnboardingCardTypeCombo, currentCardType?.Tag as string ?? "debit");
        }

        private static void AddComboItem(ComboBox comboBox, string tag, string text)
        {
            comboBox.Items.Add(new ComboBoxItem
            {
                Tag = tag,
                Content = text
            });
        }

        private static void SelectComboItemByTag(ComboBox comboBox, string? tag)
        {
            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem comboItem && string.Equals(comboItem.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = comboItem;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void ApplyFinanceOnboardingStep()
        {
            var stepNumber = _financeOnboardingStep + 1;
            FinanceOnboardingTitle.Text = _isRussian
                ? "Настройте старт финансов"
                : "Set up your finance start";
            FinanceOnboardingSubtitle.Text = _isRussian
                ? stepNumber switch
                {
                    1 => "Выберите основную валюту. Это базовая точка для сводки.",
                    2 => "Если хотите, задайте банк, тип карты, последние 4 цифры и стартовый баланс.",
                    _ => "Укажите наличные или пропустите этот шаг."
                }
                : stepNumber switch
                {
                    1 => "Choose the main currency for your finance summary.",
                    2 => "Optionally set the primary bank, card type, last 4 digits, and opening balance.",
                    _ => "Add your cash balance or skip this step."
                };
            FinanceOnboardingBadge.Text = _isRussian
                ? $"ШАГ {stepNumber}/3"
                : $"STEP {stepNumber}/3";

            FinanceOnboardingStepOnePanel.Visibility = _financeOnboardingStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            FinanceOnboardingStepTwoPanel.Visibility = _financeOnboardingStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            FinanceOnboardingStepThreePanel.Visibility = _financeOnboardingStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            FinanceBackButton.Visibility = _financeOnboardingStep == 0 ? Visibility.Collapsed : Visibility.Visible;
            FinanceNextButton.Content = _financeOnboardingStep == 2
                ? (_isRussian ? "Завершить" : "Finish")
                : (_isRussian ? "Дальше" : "Next");
        }

        private async Task LoadFinanceOverviewAsync()
        {
            if (!HasSession() || _session == null || _section != DashboardSection.Finance)
            {
                return;
            }

            if (_financeClient == null)
            {
                FinanceErrorText.Text = _isRussian ? "Supabase не настроен для финансового модуля." : "Supabase is not configured for the finance module.";
                FinanceErrorCard.Visibility = Visibility.Visible;
                RenderFinanceContent();
                return;
            }

            FinanceLoadingCard.Visibility = Visibility.Visible;
            FinanceErrorCard.Visibility = Visibility.Collapsed;

            try
            {
                var accessToken = await GetFreshFinanceAccessTokenAsync();
                _financeOverview = await _financeClient.GetOverviewAsync(accessToken);
                _financeMonthCache.Clear();
                _financeAnalytics = null;
                _financeTransactionsMonth = await _financeClient.GetTransactionsAsync(accessToken, _financeSelectedTransactionsMonth);
                CacheFinanceTransactionsMonth(_financeTransactionsMonth);
                _financeSelectedTransactionsMonth = _financeTransactionsMonth.Month;
                _financeCategories = await _financeClient.GetCategoriesAsync(accessToken);
                if (_financeOverview.OnboardingCompleted)
                {
                    _financeOnboardingStep = 0;
                }
            }
            catch (Exception ex)
            {
                if (HandleFinanceSessionError(ex))
                {
                    return;
                }
                FinanceErrorText.Text = ex.Message;
                FinanceErrorCard.Visibility = Visibility.Visible;
            }
            finally
            {
                FinanceLoadingCard.Visibility = Visibility.Collapsed;
                RenderFinanceContent();
            }
        }

        private async Task CompleteFinanceOnboardingAsync(bool skip)
        {
            if (!HasSession() || _session == null)
            {
                return;
            }

            if (_financeClient == null)
            {
                FinanceErrorText.Text = _isRussian ? "Supabase не настроен для финансового модуля." : "Supabase is not configured for the finance module.";
                FinanceErrorCard.Visibility = Visibility.Visible;
                return;
            }

            FinanceLoadingCard.Visibility = Visibility.Visible;
            FinanceErrorCard.Visibility = Visibility.Collapsed;

            try
            {
                var currency = skip ? null : (FinanceCurrencyCombo.SelectedItem as ComboBoxItem)?.Tag as string;
                var bank = skip ? null : (FinanceBankCombo.SelectedItem as ComboBoxItem)?.Tag as string;
                var primaryBalanceMinor = skip ? null : ParseMinor(FinancePrimaryBalanceInput.Text);
                var hasPrimaryCardDraft = !skip &&
                    (!string.IsNullOrWhiteSpace(FinanceOnboardingLastFourInput.Text) || (primaryBalanceMinor ?? 0) > 0);
                if (string.IsNullOrWhiteSpace(bank))
                {
                    bank = null;
                }
                if (hasPrimaryCardDraft && bank == null)
                {
                    throw new InvalidOperationException(_isRussian
                        ? "Выберите банк основной карты или очистите данные карты."
                        : "Choose the primary card bank or clear the card details.");
                }
                var cardType = skip || bank == null
                    ? null
                    : (FinanceOnboardingCardTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string;
                var lastFourDigits = skip || bank == null
                    ? null
                    : NormalizeLastFourDigits(FinanceOnboardingLastFourInput.Text);
                if (!skip && bank != null && lastFourDigits == null)
                {
                    throw new InvalidOperationException(_isRussian
                        ? "Введите последние 4 цифры основной карты."
                        : "Enter the last 4 digits of the primary card.");
                }

                _financeOverview = await _financeClient.CompleteOnboardingAsync(
                    _session.AccessToken,
                    currency,
                    bank,
                    cardType,
                    lastFourDigits,
                    skip ? null : ParseMinor(FinanceCashInput.Text),
                    primaryBalanceMinor);
                _financeOnboardingStep = 0;
            }
            catch (Exception ex)
            {
                if (HandleFinanceSessionError(ex))
                {
                    return;
                }
                FinanceErrorText.Text = ex.Message;
                FinanceErrorCard.Visibility = Visibility.Visible;
            }
            finally
            {
                FinanceLoadingCard.Visibility = Visibility.Collapsed;
                RenderFinanceContent();
            }
        }

        private void RenderFinanceContent()
        {
            var overview = _financeOverview;
            var hasData = overview != null && overview.OnboardingCompleted;
            var hasError = FinanceErrorCard.Visibility == Visibility.Visible;
            FinanceOnboardingCard.Visibility = !hasError && !hasData ? Visibility.Visible : Visibility.Collapsed;
            FinanceDataGrid.Visibility = !hasError && hasData ? Visibility.Visible : Visibility.Collapsed;

            if (overview == null)
            {
                FinanceBalanceText.Text = _isRussian ? "—" : "—";
                FinanceBalanceHint.Text = _isRussian ? "Сводка появится после загрузки." : "Summary appears after loading.";
                FinanceSettingsText.Text = _isRussian ? "Сначала загрузим данные." : "Load data first.";
                FinanceBalanceSummaryStack.Visibility = Visibility.Visible;
                FinanceOverviewCardsPanel.Children.Clear();
                FinanceAccountsPanel.Children.Clear();
                FinanceTransactionsPanel.Children.Clear();
                FinanceSettingsPanel.Children.Clear();
                return;
            }

            var currency = overview.DefaultCurrency ?? "RUB";
            FinanceBalanceText.Text = FormatMoney(overview.TotalBalanceMinor, currency);
            FinanceBalanceHint.Text = _isRussian
                ? "Баланс складывается из собственных счетов и наличных. Кредиты и кредитки идут отдельно."
                : "Balance is aggregated from your own accounts and cash. Loans and credit liabilities stay separate.";
            FinanceSettingsTitle.Text = _financeTab == FinanceTab.Analytics
                ? (_isRussian ? "Аналитика" : "Analytics")
                : (_isRussian ? "Категории" : "Categories");
            FinanceSettingsText.Text = _financeTab switch
            {
                FinanceTab.Categories => BuildCategoriesSummary(),
                FinanceTab.Analytics => _isRussian
                    ? "Аналитика пока оставлена как заглушка. Данные и навигация уже собраны."
                    : "Analytics intentionally stays as a placeholder for now. Data and navigation are already aligned.",
                _ => _isRussian
                    ? $"Основная валюта: {currency}. Следующим этапом добавим создание транзакций и расширенные счета."
                    : $"Primary currency: {currency}. Transactions and richer account management are next."
            };

            RenderFinanceOverviewCards(overview);
            RenderFinanceAccounts(overview);
            RenderFinanceTransactions(_financeTransactionsMonth ?? new FinanceTransactionsMonth
            {
                Transactions = overview.RecentTransactions
            });
            RenderFinanceAuxiliary();

            FinanceOverviewBoard.Visibility = _financeTab == FinanceTab.Overview ? Visibility.Visible : Visibility.Collapsed;
            FinanceBalanceCard.Visibility = Visibility.Collapsed;
            FinanceBalanceSummaryStack.Visibility = Visibility.Collapsed;
            FinanceOverviewCardsPanel.Visibility = _financeTab == FinanceTab.Overview ? Visibility.Visible : Visibility.Collapsed;
            FinanceAddTransactionButton.Visibility = Visibility.Visible;
            FinanceAddAccountButton.Visibility = _financeTab == FinanceTab.Accounts ? Visibility.Visible : Visibility.Collapsed;
            FinanceConfigureOverviewButton.Visibility = _financeTab == FinanceTab.Overview ? Visibility.Visible : Visibility.Collapsed;
            FinanceBalanceLabel.Visibility = Visibility.Collapsed;
            var showRecentTransactions = _financeTab == FinanceTab.Overview &&
                overview.OverviewCards.Contains("recent_transactions", StringComparer.OrdinalIgnoreCase) &&
                overview.RecentTransactions.Count > 0;

            FinanceAccountsCard.Visibility = _financeTab == FinanceTab.Accounts ? Visibility.Visible : Visibility.Collapsed;
            FinanceTransactionsCard.Visibility = (_financeTab == FinanceTab.Transactions || showRecentTransactions) ? Visibility.Visible : Visibility.Collapsed;
            FinanceSettingsCard.Visibility = (_financeTab == FinanceTab.Categories || _financeTab == FinanceTab.Analytics) ? Visibility.Visible : Visibility.Collapsed;
            var showPrimary = FinanceAccountsCard.Visibility == Visibility.Visible;
            var showSecondary = FinanceTransactionsCard.Visibility == Visibility.Visible || FinanceSettingsCard.Visibility == Visibility.Visible;
            if (showPrimary && showSecondary)
            {
                FinancePrimaryColumn.Width = new GridLength(1.05, GridUnitType.Star);
                FinanceSecondaryColumn.Width = new GridLength(0.95, GridUnitType.Star);
            }
            else if (showPrimary)
            {
                FinancePrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
                FinanceSecondaryColumn.Width = new GridLength(0, GridUnitType.Pixel);
            }
            else if (showSecondary)
            {
                FinancePrimaryColumn.Width = new GridLength(0, GridUnitType.Pixel);
                FinanceSecondaryColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                FinancePrimaryColumn.Width = new GridLength(0, GridUnitType.Pixel);
                FinanceSecondaryColumn.Width = new GridLength(0, GridUnitType.Pixel);
            }
        }

        private void RenderFinanceAuxiliary()
        {
            FinanceSettingsPanel.Children.Clear();

            if (_financeOverview == null)
            {
                return;
            }

            if (_financeTab == FinanceTab.Analytics)
            {
                RenderFinanceAnalytics();
                return;
            }

            if (_financeCategories.Count == 0)
            {
                FinanceSettingsPanel.Children.Add(CreateMutedText(_isRussian
                    ? "Категории пока не загружены."
                    : "Categories are not loaded yet."));
                return;
            }

            foreach (var direction in new[] { "expense", "income" })
            {
                var items = _financeCategories
                    .Where(item => string.Equals(item.Direction, direction, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.ParentId.HasValue ? 1 : 0)
                    .ThenBy(item => item.DisplayOrder)
                    .ToList();

                if (items.Count == 0)
                {
                    continue;
                }

                var header = new StackPanel { Spacing = 2 };
                header.Children.Add(new TextBlock
                {
                    Text = direction == "expense"
                        ? (_isRussian ? "Расходы" : "Expenses")
                        : (_isRussian ? "Доходы" : "Income"),
                    FontSize = 15,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
                header.Children.Add(CreateMutedText(_isRussian
                    ? $"{items.Count} категорий"
                    : $"{items.Count} categories"));
                FinanceSettingsPanel.Children.Add(header);

                foreach (var root in items.Where(item => item.ParentId == null).Take(6))
                {
                    var childNames = items
                        .Where(item => item.ParentId == root.Id)
                        .OrderBy(item => item.DisplayOrder)
                        .Select(item => item.Name)
                        .Take(4)
                        .ToList();

                    var card = new Border
                    {
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                        BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(18),
                        Padding = new Thickness(14, 12, 14, 12)
                    };

                    var stack = new StackPanel { Spacing = 4 };
                    stack.Children.Add(new TextBlock
                    {
                        Text = root.Name,
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                    });
                    stack.Children.Add(CreateMutedText(childNames.Count == 0
                        ? (_isRussian ? "Без подкатегорий" : "No subcategories")
                        : string.Join(" · ", childNames)));
                    card.Child = stack;
                    FinanceSettingsPanel.Children.Add(card);
                }
            }
        }

        private void RenderFinanceAnalytics()
        {
            var overview = _financeOverview;
            if (overview == null)
            {
                return;
            }

            var availableMonths = GetFinanceAvailableMonths();
            EnsureFinanceAnalyticsRangeInitialized(availableMonths);

            FinanceSettingsTitle.Text = _isRussian ? "Аналитика" : "Analytics";
            FinanceSettingsText.Text = _isRussian
                ? "Смотрите период, структуру расходов, кредитный поток и нагрузку по счетам."
                : "Review the period, spending structure, credit flow, and account load.";

            FinanceSettingsPanel.Children.Add(CreateFinanceAnalyticsPeriodCard(availableMonths));

            if (_financeAnalyticsLoading)
            {
                FinanceSettingsPanel.Children.Add(CreateFinanceAnalyticsLoadingCard());
                return;
            }

            if (_financeAnalytics == null || _financeAnalytics.TotalTransactionsCount == 0)
            {
                FinanceSettingsPanel.Children.Add(CreateMutedText(_isRussian
                    ? "Для аналитики пока не хватает транзакций в выбранном периоде."
                    : "Analytics need transactions in the selected period."));
                return;
            }

            FinanceSettingsPanel.Children.Add(CreateFinanceAnalyticsHeroCard(_financeAnalytics, overview.DefaultCurrency ?? "RUB"));
            FinanceSettingsPanel.Children.Add(CreateFinanceAnalyticsMetricGrid(_financeAnalytics, overview.DefaultCurrency ?? "RUB"));
            FinanceSettingsPanel.Children.Add(CreateFinanceAnalyticsInsightsRow(_financeAnalytics, overview.DefaultCurrency ?? "RUB"));

            if (_financeAnalytics.MonthSummaries.Count > 1)
            {
                FinanceSettingsPanel.Children.Add(CreateFinanceAnalyticsMonthsCard(_financeAnalytics, overview.DefaultCurrency ?? "RUB"));
            }

            var primaryBreakdownGrid = new Grid { ColumnSpacing = 12 };
            primaryBreakdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            primaryBreakdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var categoriesCard = CreateFinanceAnalyticsBreakdownCard(
                _isRussian ? "Категории расходов" : "Expense categories",
                _isRussian ? "Только расходы из собственных денег." : "Only spending from your own funds.",
                _financeAnalytics.ExpenseCategories,
                overview.DefaultCurrency ?? "RUB",
                _isRussian ? "Пока нет расходов из собственных денег." : "No own-funds spending yet.");
            primaryBreakdownGrid.Children.Add(categoriesCard);

            var accountsCard = CreateFinanceAnalyticsBreakdownCard(
                _isRussian ? "Счета и карты" : "Accounts and cards",
                _isRussian ? "Где было больше всего движения за период." : "Where the most movement happened in the period.",
                _financeAnalytics.Accounts,
                overview.DefaultCurrency ?? "RUB",
                _isRussian ? "Нет данных по счетам." : "No account data yet.");
            Grid.SetColumn(accountsCard, 1);
            primaryBreakdownGrid.Children.Add(accountsCard);
            FinanceSettingsPanel.Children.Add(primaryBreakdownGrid);

            var secondaryBreakdownGrid = new Grid { ColumnSpacing = 12 };
            secondaryBreakdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            secondaryBreakdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var creditCard = CreateFinanceAnalyticsBreakdownCard(
                _isRussian ? "Покупки по кредиткам" : "Credit card purchases",
                _isRussian ? "Показываем, что увеличивало долг, отдельно от обычных расходов." : "Shows what increased debt separately from regular spending.",
                _financeAnalytics.CreditCategories,
                overview.DefaultCurrency ?? "RUB",
                _isRussian ? "В выбранном периоде не было покупок по кредиткам." : "No credit card purchases in this period.");
            secondaryBreakdownGrid.Children.Add(creditCard);

            var sourceCard = CreateFinanceAnalyticsSourceCard(_financeAnalytics);
            Grid.SetColumn(sourceCard, 1);
            secondaryBreakdownGrid.Children.Add(sourceCard);
            FinanceSettingsPanel.Children.Add(secondaryBreakdownGrid);
        }

        private void RenderFinanceOverviewCards(FinanceOverview overview)
        {
            FinanceOverviewCardsPanel.Children.Clear();

            var orderedCards = GetRenderedOverviewCards(overview)
                .Where(id => !string.Equals(id, "recent_transactions", StringComparison.OrdinalIgnoreCase))
                .Where(id => GetOverviewCardMetric(overview, id) > 0)
                .ToList();

            if (orderedCards.Count == 0)
            {
                FinanceOverviewCardsPanel.Children.Add(CreateMutedText(_isRussian
                    ? "Пока нет ненулевых показателей для обзора."
                    : "There are no non-zero overview metrics yet."));
                return;
            }

            foreach (var row in orderedCards.Chunk(3))
            {
                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                for (var index = 0; index < row.Length; index++)
                {
                    var card = CreateOverviewMetricCard(overview, row[index], index == 0 && string.Equals(row[index], "total_balance", StringComparison.OrdinalIgnoreCase));
                    Grid.SetColumn(card, index);
                    grid.Children.Add(card);
                }

                FinanceOverviewCardsPanel.Children.Add(grid);
            }
        }

        private List<string> GetRenderedOverviewCards(FinanceOverview overview)
        {
            return (overview.OverviewCards.Count == 0
                    ? OverviewCardOrder.AsEnumerable()
                    : overview.OverviewCards.AsEnumerable())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<string> GetOverviewSettingsCards(FinanceOverview overview)
        {
            var cards = (overview.OverviewCards.Count == 0
                    ? OverviewCardOrder.AsEnumerable()
                    : overview.OverviewCards.AsEnumerable())
                .Concat(OverviewCardOrder)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (overview.Accounts.Any(IsLoanAccount) &&
                !cards.Contains("loan_full_debt", StringComparer.OrdinalIgnoreCase))
            {
                var insertIndex = cards.FindIndex(id => string.Equals(id, "loan_debt", StringComparison.OrdinalIgnoreCase));
                if (insertIndex >= 0)
                {
                    cards.Insert(insertIndex + 1, "loan_full_debt");
                }
                else
                {
                    cards.Add("loan_full_debt");
                }
            }

            return cards;
        }

        private Border CreateOverviewMetricCard(FinanceOverview overview, string cardId, bool emphasize)
        {
            var currency = overview.DefaultCurrency ?? "RUB";
            var loanFullDebtMinor = GetOverviewLoanFullDebtMinor(overview);
            var (title, value) = cardId switch
            {
                "total_balance" => (_isRussian ? "Общий баланс" : "Total balance", FormatMoney(overview.TotalBalanceMinor, currency)),
                "card_balance" => (_isRussian ? "На картах" : "On cards", FormatMoney(overview.CardBalanceMinor, currency)),
                "cash_balance" => (_isRussian ? "Наличные" : "Cash", FormatMoney(overview.CashBalanceMinor, currency)),
                "credit_debt" => (_isRussian ? "Долг по кредиткам" : "Credit card debt", FormatMoney(-overview.CreditDebtMinor, currency)),
                "loan_debt" => (_isRussian ? "Долг по кредитам" : "Loan debt", FormatMoney(-overview.LoanDebtMinor, currency)),
                "loan_full_debt" => (_isRussian ? "Полный долг по кредитам" : "Full loan payoff", FormatMoney(-loanFullDebtMinor, currency)),
                "credit_spend" => (_isRussian ? "Покупки по кредитке" : "Credit card spending", FormatMoney(-overview.CreditSpendMinor, currency)),
                "month_income" => (_isRussian ? "Доходы" : "Income", FormatMoney(overview.MonthIncomeMinor, currency)),
                "month_expense" => (_isRussian ? "Расходы" : "Expenses", FormatMoney(-overview.MonthExpenseMinor, currency)),
                "month_result" => (_isRussian ? "Результат месяца" : "Month result", FormatMoney(overview.MonthNetMinor, currency)),
                _ => (cardId, "—")
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 0x2A, 0x27, 0x2D)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(26),
                Padding = new Thickness(20, 18, 20, 18),
                MinHeight = 144
            };

            var stack = new StackPanel { Spacing = 16 };

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconSurface = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(
                    emphasize ? (byte)64 : (byte)44,
                    GetOverviewCardAccent(cardId).R,
                    GetOverviewCardAccent(cardId).G,
                    GetOverviewCardAccent(cardId).B)),
                Child = CreateOverviewCardIcon(cardId)
            };
            topRow.Children.Add(iconSurface);

            var titleStack = new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Text Semibold"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });
            Grid.SetColumn(titleStack, 1);
            topRow.Children.Add(titleStack);
            stack.Children.Add(topRow);

            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 34,
                FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
                FontFamily = new FontFamily("Segoe UI Variable Display Semibld"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                TextWrapping = TextWrapping.Wrap
            });

            if ((string.Equals(cardId, "loan_debt", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cardId, "loan_full_debt", StringComparison.OrdinalIgnoreCase)) &&
                overview.Accounts.Any(IsLoanAccount))
            {
                var actionButton = CreateFinanceMiniButton(_isRussian ? "Внести платёж" : "Make payment");
                actionButton.HorizontalAlignment = HorizontalAlignment.Left;
                actionButton.Click += async (_, _) => await ShowLoanPaymentDialogAsync(null);
                stack.Children.Add(actionButton);
            }

            border.Child = stack;
            return border;
        }

        private void CacheFinanceTransactionsMonth(FinanceTransactionsMonth month)
        {
            if (string.IsNullOrWhiteSpace(month.Month))
            {
                return;
            }

            _financeMonthCache[month.Month] = month;
        }

        private List<string> GetFinanceAvailableMonths()
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(_financeTransactionsMonth?.Month))
            {
                values.Add(_financeTransactionsMonth.Month);
            }

            foreach (var item in _financeTransactionsMonth?.AvailableMonths ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    values.Add(item);
                }
            }

            foreach (var item in _financeMonthCache.Keys)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    values.Add(item);
                }
            }

            return values
                .OrderByDescending(item => item, StringComparer.Ordinal)
                .ToList();
        }

        private void EnsureFinanceAnalyticsRangeInitialized(IReadOnlyList<string> availableMonths)
        {
            if (availableMonths.Count == 0)
            {
                _financeAnalyticsFromMonth = null;
                _financeAnalyticsToMonth = null;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_financeAnalyticsFromMonth) &&
                !string.IsNullOrWhiteSpace(_financeAnalyticsToMonth))
            {
                return;
            }

            ApplyFinanceAnalyticsPreset("current", availableMonths);
        }

        private void ApplyFinanceAnalyticsPreset(string preset, IReadOnlyList<string>? availableMonths = null)
        {
            var months = (availableMonths ?? GetFinanceAvailableMonths())
                .OrderByDescending(item => item, StringComparer.Ordinal)
                .ToList();
            if (months.Count == 0)
            {
                _financeAnalyticsPreset = preset;
                _financeAnalyticsFromMonth = null;
                _financeAnalyticsToMonth = null;
                return;
            }

            var latest = months[0];
            var oldest = months[^1];
            _financeAnalyticsPreset = preset;

            switch (preset)
            {
                case "last3":
                {
                    var slice = months.Take(Math.Min(3, months.Count)).ToList();
                    _financeAnalyticsToMonth = slice.First();
                    _financeAnalyticsFromMonth = slice.Last();
                    break;
                }
                case "last6":
                {
                    var slice = months.Take(Math.Min(6, months.Count)).ToList();
                    _financeAnalyticsToMonth = slice.First();
                    _financeAnalyticsFromMonth = slice.Last();
                    break;
                }
                case "all":
                    _financeAnalyticsToMonth = latest;
                    _financeAnalyticsFromMonth = oldest;
                    break;
                default:
                    _financeAnalyticsToMonth = latest;
                    _financeAnalyticsFromMonth = latest;
                    _financeAnalyticsPreset = "current";
                    break;
            }
        }

        private async Task EnsureFinanceAnalyticsReadyAsync(bool force = false)
        {
            if (_financeTab != FinanceTab.Analytics)
            {
                return;
            }

            if (!force && _financeAnalytics != null)
            {
                return;
            }

            await RefreshFinanceAnalyticsAsync(force);
        }

        private async Task RefreshFinanceAnalyticsAsync(bool force = false)
        {
            if (_financeClient == null || _session == null || _financeOverview == null)
            {
                return;
            }

            var availableMonths = GetFinanceAvailableMonths();
            EnsureFinanceAnalyticsRangeInitialized(availableMonths);

            if (string.IsNullOrWhiteSpace(_financeAnalyticsFromMonth) || string.IsNullOrWhiteSpace(_financeAnalyticsToMonth))
            {
                _financeAnalytics = null;
                RenderFinanceContent();
                return;
            }

            var monthsToLoad = EnumerateFinanceMonths(_financeAnalyticsFromMonth, _financeAnalyticsToMonth);
            if (monthsToLoad.Count > 0)
            {
                _financeAnalyticsFromMonth = monthsToLoad.Min(StringComparer.Ordinal);
                _financeAnalyticsToMonth = monthsToLoad.Max(StringComparer.Ordinal);
            }

            if (!force &&
                _financeAnalytics != null &&
                string.Equals(_financeAnalytics.FromMonth, _financeAnalyticsFromMonth, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_financeAnalytics.ToMonth, _financeAnalyticsToMonth, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                _financeAnalyticsLoading = true;
                RenderFinanceContent();
                await EnsureFinanceMonthsLoadedAsync(monthsToLoad);

                var transactions = monthsToLoad
                    .Where(month => _financeMonthCache.TryGetValue(month, out _))
                    .SelectMany(month => _financeMonthCache[month].Transactions)
                    .Where(transaction =>
                    {
                        var transactionMonth = transaction.HappenedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                        return monthsToLoad.Contains(transactionMonth, StringComparer.OrdinalIgnoreCase);
                    })
                    .OrderByDescending(transaction => transaction.HappenedAt)
                    .ToList();

                _financeAnalytics = BuildFinanceAnalyticsSnapshot(monthsToLoad, transactions);
            }
            catch (Exception ex)
            {
                if (HandleFinanceSessionError(ex))
                {
                    return;
                }

                FinanceErrorText.Text = ex.Message;
                FinanceErrorCard.Visibility = Visibility.Visible;
            }
            finally
            {
                _financeAnalyticsLoading = false;
                RenderFinanceContent();
                if (_financeTab == FinanceTab.Analytics && FinanceSettingsCard.Visibility == Visibility.Visible)
                {
                    AnimateElementRefresh(FinanceSettingsCard);
                }
            }
        }

        private async Task EnsureFinanceMonthsLoadedAsync(IEnumerable<string> months, bool force = false)
        {
            if (_financeClient == null || _session == null)
            {
                return;
            }

            var missingMonths = months
                .Where(month => force || !_financeMonthCache.ContainsKey(month))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missingMonths.Count == 0)
            {
                return;
            }

            var accessToken = await GetFreshFinanceAccessTokenAsync();
            foreach (var month in missingMonths)
            {
                var payload = await _financeClient.GetTransactionsAsync(accessToken, month);
                CacheFinanceTransactionsMonth(payload);
            }
        }

        private static List<string> EnumerateFinanceMonths(string fromMonth, string toMonth)
        {
            if (!DateTime.TryParseExact($"{fromMonth}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
                !DateTime.TryParseExact($"{toMonth}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                return new List<string>();
            }

            if (fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            var result = new List<string>();
            for (var cursor = new DateTime(fromDate.Year, fromDate.Month, 1); cursor <= toDate; cursor = cursor.AddMonths(1))
            {
                result.Add(cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            }

            return result;
        }

        private FinanceAnalyticsSnapshot BuildFinanceAnalyticsSnapshot(IReadOnlyList<string> months, IReadOnlyList<FinanceTransaction> transactions)
        {
            var overview = _financeOverview ?? new FinanceOverview();
            var currency = overview.DefaultCurrency ?? "RUB";
            var accountsById = overview.Accounts.ToDictionary(account => account.Id);
            var ownExpenseByCategory = new Dictionary<string, (long amount, int count)>(StringComparer.OrdinalIgnoreCase);
            var creditExpenseByCategory = new Dictionary<string, (long amount, int count)>(StringComparer.OrdinalIgnoreCase);
            var accountActivity = new Dictionary<string, (long ownIncome, long ownExpense, long creditExpense, long transfers, int count)>(StringComparer.OrdinalIgnoreCase);
            var sourceMix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var monthSummaries = months.ToDictionary(
                month => month,
                month => new FinanceAnalyticsMonthSummary { Month = month },
                StringComparer.OrdinalIgnoreCase);

            long ownIncomeMinor = 0;
            long ownExpenseMinor = 0;
            long creditExpenseMinor = 0;
            long totalPurchasesMinor = 0;
            long largestPurchaseMinor = 0;
            string? largestPurchaseTitle = null;
            string? largestPurchaseContext = null;
            var incomeCount = 0;
            var expenseCount = 0;
            var creditExpenseCount = 0;
            var transferCount = 0;
            var purchaseCount = 0;

            foreach (var transaction in transactions)
            {
                var transactionMonth = transaction.HappenedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                if (!monthSummaries.TryGetValue(transactionMonth, out var monthSummary))
                {
                    monthSummary = new FinanceAnalyticsMonthSummary { Month = transactionMonth };
                    monthSummaries[transactionMonth] = monthSummary;
                }

                monthSummary.Count += 1;
                var sourceLabel = GetTransactionSourceLabel(transaction.SourceType);
                sourceMix[sourceLabel] = sourceMix.GetValueOrDefault(sourceLabel) + 1;

                accountsById.TryGetValue(transaction.AccountId, out var account);
                var accountLabel = account?.Name ?? transaction.AccountName;
                if (!accountActivity.TryGetValue(accountLabel, out var activity))
                {
                    activity = (0, 0, 0, 0, 0);
                }

                activity.count += 1;
                var isLoanPayment = string.Equals(transaction.TransactionKind, "loan_payment", StringComparison.OrdinalIgnoreCase);
                var isTransfer = !isLoanPayment && (
                    string.Equals(transaction.Direction, "transfer", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(transaction.TransactionKind, "transfer", StringComparison.OrdinalIgnoreCase));
                var isCreditSource = account != null && IsCreditAccount(account);

                if (isTransfer)
                {
                    transferCount++;
                    activity.transfers += transaction.AmountMinor;
                }
                else if (string.Equals(transaction.Direction, "income", StringComparison.OrdinalIgnoreCase))
                {
                    if (!isCreditSource)
                    {
                        ownIncomeMinor += transaction.AmountMinor;
                        incomeCount++;
                        activity.ownIncome += transaction.AmountMinor;
                        monthSummary.OwnIncomeMinor += transaction.AmountMinor;
                    }
                }
                else if (string.Equals(transaction.Direction, "expense", StringComparison.OrdinalIgnoreCase))
                {
                    purchaseCount++;
                    totalPurchasesMinor += transaction.AmountMinor;
                    if (transaction.AmountMinor > largestPurchaseMinor)
                    {
                        largestPurchaseMinor = transaction.AmountMinor;
                        largestPurchaseTitle = transaction.Title;
                        largestPurchaseContext = string.IsNullOrWhiteSpace(transaction.CategoryName)
                            ? accountLabel
                            : $"{transaction.CategoryName} · {accountLabel}";
                    }

                    var categoryLabel = string.IsNullOrWhiteSpace(transaction.CategoryName)
                        ? (_isRussian ? "Без категории" : "No category")
                        : transaction.CategoryName!;

                    if (isCreditSource)
                    {
                        creditExpenseMinor += transaction.AmountMinor;
                        creditExpenseCount++;
                        activity.creditExpense += transaction.AmountMinor;
                        monthSummary.CreditExpenseMinor += transaction.AmountMinor;
                        var current = creditExpenseByCategory.GetValueOrDefault(categoryLabel);
                        creditExpenseByCategory[categoryLabel] = (current.amount + transaction.AmountMinor, current.count + 1);
                    }
                    else
                    {
                        ownExpenseMinor += transaction.AmountMinor;
                        expenseCount++;
                        activity.ownExpense += transaction.AmountMinor;
                        monthSummary.OwnExpenseMinor += transaction.AmountMinor;
                        var current = ownExpenseByCategory.GetValueOrDefault(categoryLabel);
                        ownExpenseByCategory[categoryLabel] = (current.amount + transaction.AmountMinor, current.count + 1);
                    }
                }

                accountActivity[accountLabel] = activity;
                monthSummaries[transactionMonth] = monthSummary;
            }

            var ownExpenseSlices = ownExpenseByCategory
                .OrderByDescending(item => item.Value.amount)
                .Take(6)
                .Select(item => new FinanceAnalyticsSlice
                {
                    Label = item.Key,
                    AmountMinor = item.Value.amount,
                    Count = item.Value.count,
                    Share = ownExpenseMinor > 0 ? (double)item.Value.amount / ownExpenseMinor : 0,
                    Subtitle = _isRussian ? $"{item.Value.count} операций" : $"{item.Value.count} transactions"
                })
                .ToList();

            var creditExpenseSlices = creditExpenseByCategory
                .OrderByDescending(item => item.Value.amount)
                .Take(6)
                .Select(item => new FinanceAnalyticsSlice
                {
                    Label = item.Key,
                    AmountMinor = item.Value.amount,
                    Count = item.Value.count,
                    Share = creditExpenseMinor > 0 ? (double)item.Value.amount / creditExpenseMinor : 0,
                    Subtitle = _isRussian ? $"{item.Value.count} операций" : $"{item.Value.count} transactions"
                })
                .ToList();

            var accountSlices = accountActivity
                .Select(item =>
                {
                    var movement = item.Value.ownIncome + item.Value.ownExpense + item.Value.creditExpense + item.Value.transfers;
                    var parts = new List<string>();
                    if (item.Value.ownExpense > 0)
                    {
                        parts.Add((_isRussian ? "Расходы" : "Expenses") + ": " + FormatMoney(item.Value.ownExpense, currency));
                    }

                    if (item.Value.creditExpense > 0)
                    {
                        parts.Add((_isRussian ? "Кредитные покупки" : "Credit purchases") + ": " + FormatMoney(item.Value.creditExpense, currency));
                    }

                    if (item.Value.ownIncome > 0)
                    {
                        parts.Add((_isRussian ? "Доходы" : "Income") + ": " + FormatMoney(item.Value.ownIncome, currency));
                    }

                    if (item.Value.transfers > 0)
                    {
                        parts.Add((_isRussian ? "Переводы" : "Transfers") + ": " + FormatMoney(item.Value.transfers, currency));
                    }

                    return new FinanceAnalyticsSlice
                    {
                        Label = item.Key,
                        AmountMinor = movement,
                        Count = item.Value.count,
                        Share = movement,
                        Subtitle = parts.Count == 0
                            ? (_isRussian ? "Без движения" : "No movement")
                            : string.Join(" · ", parts)
                    };
                })
                .OrderByDescending(item => item.AmountMinor)
                .Take(6)
                .ToList();

            var maxAccountMovement = accountSlices.Count == 0 ? 0 : accountSlices.Max(item => item.AmountMinor);
            foreach (var slice in accountSlices)
            {
                slice.Share = maxAccountMovement > 0 ? (double)slice.AmountMinor / maxAccountMovement : 0;
            }

            var sourceSlices = sourceMix
                .OrderByDescending(item => item.Value)
                .Select(item => new FinanceAnalyticsSlice
                {
                    Label = item.Key,
                    AmountMinor = item.Value,
                    Count = item.Value,
                    Share = transactions.Count > 0 ? (double)item.Value / transactions.Count : 0,
                    Subtitle = _isRussian ? "Источник добавления" : "Import source"
                })
                .ToList();

            return new FinanceAnalyticsSnapshot
            {
                FromMonth = months.Count == 0 ? string.Empty : months.OrderBy(item => item, StringComparer.Ordinal).First(),
                ToMonth = months.Count == 0 ? string.Empty : months.OrderByDescending(item => item, StringComparer.Ordinal).First(),
                Months = months.OrderBy(item => item, StringComparer.Ordinal).ToList(),
                OwnIncomeMinor = ownIncomeMinor,
                OwnExpenseMinor = ownExpenseMinor,
                CreditExpenseMinor = creditExpenseMinor,
                NetOwnFlowMinor = ownIncomeMinor - ownExpenseMinor,
                IncomeCount = incomeCount,
                ExpenseCount = expenseCount,
                CreditExpenseCount = creditExpenseCount,
                TransferCount = transferCount,
                TotalTransactionsCount = transactions.Count,
                AveragePurchaseMinor = purchaseCount > 0 ? totalPurchasesMinor / purchaseCount : 0,
                LargestPurchaseMinor = largestPurchaseMinor,
                LargestPurchaseTitle = largestPurchaseTitle,
                LargestPurchaseContext = largestPurchaseContext,
                ExpenseCategories = ownExpenseSlices,
                CreditCategories = creditExpenseSlices,
                Accounts = accountSlices,
                Sources = sourceSlices,
                MonthSummaries = monthSummaries.Values
                    .OrderBy(item => item.Month, StringComparer.Ordinal)
                    .ToList()
            };
        }

        private Button CreateFinanceMiniButton(string text) => new()
        {
            Content = text,
            Padding = new Thickness(10, 6, 10, 6),
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
            BorderThickness = new Thickness(1),
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
            CornerRadius = new CornerRadius(14),
            MinWidth = 36
        };

        private Border CreateFinanceAnalyticsLoadingCard()
        {
            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(new ProgressRing
            {
                IsActive = true,
                Width = 28,
                Height = 28
            });
            stack.Children.Add(new TextBlock
            {
                Text = _isRussian ? "Собираем аналитику за выбранный период…" : "Building analytics for the selected period…",
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });
            stack.Children.Add(CreateMutedText(_isRussian
                ? "Подгружаем нужные месяцы и пересчитываем структуру расходов."
                : "Loading the required months and recalculating the spending structure."));
            return CreateAnalyticsSectionCard(null, null, stack);
        }

        private Border CreateFinanceAnalyticsPeriodCard(IReadOnlyList<string> availableMonths)
        {
            var stack = new StackPanel { Spacing = 14 };

            var header = new Grid { ColumnSpacing = 12 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerStack = new StackPanel { Spacing = 4 };
            headerStack.Children.Add(new TextBlock
            {
                Text = _isRussian ? "Период аналитики" : "Analytics period",
                FontSize = 17,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });
            headerStack.Children.Add(CreateMutedText(_financeAnalyticsFromMonth == _financeAnalyticsToMonth
                ? FormatFinanceMonthLabel(_financeAnalyticsToMonth)
                : $"{FormatFinanceMonthLabel(_financeAnalyticsFromMonth)} — {FormatFinanceMonthLabel(_financeAnalyticsToMonth)}"));
            header.Children.Add(headerStack);

            var meta = CreateMutedText(_financeAnalyticsLoading
                ? (_isRussian ? "Обновляем…" : "Refreshing…")
                : (_isRussian ? $"Месяцев в базе: {Math.Max(availableMonths.Count, 1)}" : $"Months available: {Math.Max(availableMonths.Count, 1)}"));
            meta.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(meta, 1);
            header.Children.Add(meta);
            stack.Children.Add(header);

            var presets = new Grid { ColumnSpacing = 8 };
            for (var i = 0; i < 4; i++)
            {
                presets.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            AddAnalyticsPresetButton(presets, 0, _isRussian ? "Месяц" : "Month", "current");
            AddAnalyticsPresetButton(presets, 1, _isRussian ? "3 месяца" : "3 months", "last3");
            AddAnalyticsPresetButton(presets, 2, _isRussian ? "6 месяцев" : "6 months", "last6");
            AddAnalyticsPresetButton(presets, 3, _isRussian ? "Всё время" : "All time", "all");
            stack.Children.Add(presets);

            if (availableMonths.Count > 0)
            {
                var rangeGrid = new Grid { ColumnSpacing = 12 };
                rangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var fromCombo = new ComboBox
                {
                    Header = _isRussian ? "С" : "From",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    IsEnabled = !_financeAnalyticsLoading
                };
                var toCombo = new ComboBox
                {
                    Header = _isRussian ? "По" : "To",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    IsEnabled = !_financeAnalyticsLoading
                };

                foreach (var month in availableMonths)
                {
                    fromCombo.Items.Add(new ComboBoxItem { Tag = month, Content = FormatFinanceMonthLabel(month) });
                    toCombo.Items.Add(new ComboBoxItem { Tag = month, Content = FormatFinanceMonthLabel(month) });
                }

                SelectMonthComboItem(fromCombo, _financeAnalyticsFromMonth);
                SelectMonthComboItem(toCombo, _financeAnalyticsToMonth);

                fromCombo.SelectionChanged += async (_, _) =>
                {
                    if (_financeAnalyticsLoading || fromCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string month)
                    {
                        return;
                    }

                    _financeAnalyticsPreset = "custom";
                    _financeAnalyticsFromMonth = month;
                    await RefreshFinanceAnalyticsAsync();
                };
                toCombo.SelectionChanged += async (_, _) =>
                {
                    if (_financeAnalyticsLoading || toCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string month)
                    {
                        return;
                    }

                    _financeAnalyticsPreset = "custom";
                    _financeAnalyticsToMonth = month;
                    await RefreshFinanceAnalyticsAsync();
                };

                rangeGrid.Children.Add(fromCombo);
                Grid.SetColumn(toCombo, 1);
                rangeGrid.Children.Add(toCombo);
                stack.Children.Add(rangeGrid);
            }

            return CreateAnalyticsSectionCard(null, null, stack);
        }

        private void AddAnalyticsPresetButton(Grid grid, int column, string text, string preset)
        {
            var isActive = string.Equals(_financeAnalyticsPreset, preset, StringComparison.OrdinalIgnoreCase);
            var button = CreateFinanceMiniButton(text);
            button.Background = isActive
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PillBackgroundBrush"]
                : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"];
            button.BorderBrush = isActive
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
            button.Foreground = isActive
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"];
            button.Click += async (_, _) =>
            {
                ApplyFinanceAnalyticsPreset(preset);
                await RefreshFinanceAnalyticsAsync();
            };
            Grid.SetColumn(button, column);
            grid.Children.Add(button);
        }

        private static void SelectMonthComboItem(ComboBox comboBox, string? month)
        {
            if (string.IsNullOrWhiteSpace(month))
            {
                return;
            }

            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag as string, month, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private Border CreateFinanceBadge(string text, bool emphasize = false) => new()
        {
            Background = emphasize
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PillBackgroundBrush"]
                : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
            BorderBrush = emphasize
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = emphasize ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = emphasize
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                    : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            }
        };

        private Button CreateFinanceBadgeButton(string text, bool emphasize = false)
        {
            var button = new Button
            {
                Content = text,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Normal,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4)
            };

            button.Resources["ButtonBackground"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"];
            button.Resources["ButtonBackgroundPointerOver"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"];
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 0x42, 0x3F, 0x46));
            button.Resources["ButtonBorderBrush"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));
            button.Resources["ButtonForeground"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"];
            button.Resources["ButtonForegroundPointerOver"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"];
            button.Resources["ButtonForegroundPressed"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"];
            return button;
        }

        private Button CreateFinanceSurfaceButton(
            UIElement content,
            bool centerContent = false,
            Thickness? padding = null,
            CornerRadius? cornerRadius = null)
        {
            var normalBackground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"];
            var hoverBackground = new SolidColorBrush(Color.FromArgb(255, 0x38, 0x35, 0x3C));
            var pressedBackground = new SolidColorBrush(Color.FromArgb(255, 0x42, 0x3F, 0x46));
            var normalBorder = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
            var hoverBorder = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));

            var surface = new Border
            {
                Background = normalBackground,
                BorderBrush = normalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = cornerRadius ?? new CornerRadius(16),
                Padding = padding ?? new Thickness(14, 12, 14, 12),
                Child = content
            };

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                CornerRadius = cornerRadius ?? new CornerRadius(16),
                Padding = new Thickness(0),
                Content = surface
            };

            button.Resources["ButtonBackground"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBackgroundDisabled"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBorderBrushDisabled"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

            button.PointerEntered += (_, _) =>
            {
                surface.Background = hoverBackground;
                surface.BorderBrush = hoverBorder;
            };

            button.PointerExited += (_, _) =>
            {
                surface.Background = normalBackground;
                surface.BorderBrush = normalBorder;
            };

            button.PointerPressed += (_, _) =>
            {
                surface.Background = pressedBackground;
                surface.BorderBrush = hoverBorder;
            };

            button.PointerReleased += (_, _) =>
            {
                surface.Background = hoverBackground;
                surface.BorderBrush = hoverBorder;
            };

            if (centerContent && content is FrameworkElement element)
            {
                element.HorizontalAlignment = HorizontalAlignment.Center;
            }

            return button;
        }

        private Border CreateFinanceAnalyticsHeroCard(FinanceAnalyticsSnapshot snapshot, string currency)
        {
            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(new TextBlock
            {
                Text = BuildFinanceAnalyticsHeadline(snapshot, currency),
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Display Semibld"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(CreateMutedText(BuildFinanceAnalyticsSubheadline(snapshot, currency)));

            var badges = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            badges.Children.Add(CreateFinanceBadge(_isRussian ? $"Период: {snapshot.Months.Count} мес." : $"Period: {snapshot.Months.Count} mo."));
            badges.Children.Add(CreateFinanceBadge(_isRussian ? $"Операций: {snapshot.TotalTransactionsCount}" : $"Transactions: {snapshot.TotalTransactionsCount}"));
            if (snapshot.LargestPurchaseMinor > 0)
            {
                badges.Children.Add(CreateFinanceBadge(_isRussian ? "Есть крупная трата" : "Largest purchase tracked", true));
            }

            stack.Children.Add(badges);
            return CreateAnalyticsSectionCard(null, null, stack);
        }

        private Grid CreateFinanceAnalyticsMetricGrid(FinanceAnalyticsSnapshot snapshot, string currency)
        {
            var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            AddAnalyticsMetricCard(grid, 0, 0, _isRussian ? "Доходы" : "Income", FormatMoney(snapshot.OwnIncomeMinor, currency), _isRussian ? "Только собственные деньги" : "Own funds only");
            AddAnalyticsMetricCard(grid, 1, 0, _isRussian ? "Расходы" : "Expenses", FormatMoney(snapshot.OwnExpenseMinor, currency), _isRussian ? "Оплаты с дебетовых карт и наличных" : "Paid from debit cards and cash");
            AddAnalyticsMetricCard(grid, 0, 1, _isRussian ? "Покупки по кредиткам" : "Credit purchases", FormatMoney(snapshot.CreditExpenseMinor, currency), _isRussian ? "Что увеличило долг" : "What increased debt");
            AddAnalyticsMetricCard(grid, 1, 1, _isRussian ? "Чистый итог" : "Net result", FormatMoney(snapshot.NetOwnFlowMinor, currency), _isRussian ? "Доходы минус расходы" : "Income minus expenses");
            return grid;
        }

        private void AddAnalyticsMetricCard(Grid grid, int column, int row, string title, string value, string subtitle)
        {
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Display Semibld"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });
            stack.Children.Add(CreateMutedText(subtitle));

            var card = CreateAnalyticsSectionCard(null, null, stack);
            Grid.SetColumn(card, column);
            Grid.SetRow(card, row);
            grid.Children.Add(card);
        }

        private Grid CreateFinanceAnalyticsInsightsRow(FinanceAnalyticsSnapshot snapshot, string currency)
        {
            var grid = new Grid { ColumnSpacing = 10 };
            for (var index = 0; index < 4; index++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            AddAnalyticsInsightCard(grid, 0, _isRussian ? "Средний чек" : "Average purchase", snapshot.AveragePurchaseMinor > 0 ? FormatMoney(snapshot.AveragePurchaseMinor, currency) : "—");
            AddAnalyticsInsightCard(grid, 1, _isRussian ? "Переводы" : "Transfers", snapshot.TransferCount.ToString(CultureInfo.InvariantCulture));
            AddAnalyticsInsightCard(grid, 2, _isRussian ? "Расходных операций" : "Expense tx", (snapshot.ExpenseCount + snapshot.CreditExpenseCount).ToString(CultureInfo.InvariantCulture));
            AddAnalyticsInsightCard(grid, 3, _isRussian ? "Крупнейшая трата" : "Largest purchase", snapshot.LargestPurchaseMinor > 0 ? FormatMoney(snapshot.LargestPurchaseMinor, currency) : "—");
            return grid;
        }

        private void AddAnalyticsInsightCard(Grid grid, int column, string title, string value)
        {
            var stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });

            var card = CreateAnalyticsSectionCard(null, null, stack, new Thickness(14, 12, 14, 12));
            Grid.SetColumn(card, column);
            grid.Children.Add(card);
        }

    }
}

