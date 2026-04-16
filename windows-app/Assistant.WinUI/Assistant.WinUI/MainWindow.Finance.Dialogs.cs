using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        private async Task ShowOverviewSettingsDialogAsync()
        {
            if (_financeOverview == null)
            {
                return;
            }

            var orderedCards = GetOverviewSettingsCards(_financeOverview);
            var selectedCards = new HashSet<string>(_financeOverview.OverviewCards, StringComparer.OrdinalIgnoreCase);
            var contentHost = new StackPanel { Spacing = 10 };
            var surfaces = new List<Border>();
            string? draggedCardId = null;
            int? previewTargetIndex = null;
            double dragOffsetY = 0;
            double dragStartY = 0;
            TranslateTransform? activeTransform = null;
            const double rowHeight = 78d;

            void UpdatePreviewStyles()
            {
                for (var index = 0; index < surfaces.Count; index++)
                {
                    var surface = surfaces[index];
                    var isPreview = previewTargetIndex == index && !string.Equals(draggedCardId, orderedCards[index], StringComparison.OrdinalIgnoreCase);
                    surface.BorderBrush = isPreview
                        ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                        : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
                    surface.BorderThickness = isPreview ? new Thickness(1.5) : new Thickness(1);
                }
            }

            void FinishDrag(PointerRoutedEventArgs? pointerArgs = null, UIElement? captureElement = null)
            {
                if (draggedCardId != null)
                {
                    var currentIndex = orderedCards.FindIndex(item => string.Equals(item, draggedCardId, StringComparison.OrdinalIgnoreCase));
                    if (currentIndex >= 0)
                    {
                        var targetIndex = previewTargetIndex ?? Math.Clamp(currentIndex + (int)Math.Round(dragOffsetY / rowHeight), 0, orderedCards.Count - 1);
                        orderedCards = MoveOverviewCardToIndex(orderedCards, draggedCardId, targetIndex);
                    }
                }

                dragOffsetY = 0;
                draggedCardId = null;
                previewTargetIndex = null;
                if (activeTransform != null)
                {
                    activeTransform.Y = 0;
                    activeTransform = null;
                }
                UpdatePreviewStyles();

                if (pointerArgs != null && captureElement != null)
                {
                    captureElement.ReleasePointerCapture(pointerArgs.Pointer);
                }

                RenderRows();
            }

            void RenderRows()
            {
                contentHost.Children.Clear();
                surfaces.Clear();
                foreach (var cardId in orderedCards)
                {
                    var itemIndex = orderedCards.FindIndex(item => string.Equals(item, cardId, StringComparison.OrdinalIgnoreCase));
                    var transform = new TranslateTransform();
                    var surface = new Border
                    {
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                        BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(18),
                        Padding = new Thickness(14),
                        RenderTransform = transform
                    };
                    surfaces.Add(surface);

                    var row = new Grid { ColumnSpacing = 10 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var dragHandle = new Border
                    {
                        Width = 34,
                        Height = 34,
                        CornerRadius = new CornerRadius(10),
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                        Child = new TextBlock
                        {
                            Text = "⋮⋮",
                            FontSize = 12,
                            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    };
                    dragHandle.PointerPressed += (sender, e) =>
                    {
                        draggedCardId = cardId;
                        dragOffsetY = 0;
                        dragStartY = e.GetCurrentPoint(contentHost).Position.Y;
                        activeTransform = transform;
                        ((UIElement)sender).CapturePointer(e.Pointer);
                        e.Handled = true;
                    };
                    dragHandle.PointerMoved += (sender, e) =>
                    {
                        if (!string.Equals(draggedCardId, cardId, StringComparison.OrdinalIgnoreCase) || activeTransform == null)
                        {
                            return;
                        }

                        dragOffsetY = e.GetCurrentPoint(contentHost).Position.Y - dragStartY;
                        activeTransform.Y = dragOffsetY;
                        var currentIndex = orderedCards.FindIndex(item => string.Equals(item, cardId, StringComparison.OrdinalIgnoreCase));
                        if (currentIndex >= 0)
                        {
                            previewTargetIndex = Math.Clamp(currentIndex + (int)Math.Round(dragOffsetY / rowHeight), 0, orderedCards.Count - 1);
                            UpdatePreviewStyles();
                        }
                        e.Handled = true;
                    };
                    dragHandle.PointerReleased += (sender, e) => FinishDrag(e, (UIElement)sender);
                    dragHandle.PointerCanceled += (sender, e) => FinishDrag(e, (UIElement)sender);
                    row.Children.Add(dragHandle);

                    var toggle = new CheckBox
                    {
                        Content = GetOverviewCardLabel(cardId),
                        IsChecked = selectedCards.Contains(cardId),
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    toggle.Checked += (_, _) => selectedCards.Add(cardId);
                    toggle.Unchecked += (_, _) => selectedCards.Remove(cardId);
                    Grid.SetColumn(toggle, 1);
                    row.Children.Add(toggle);

                    surface.Child = row;
                    contentHost.Children.Add(surface);
                }

                UpdatePreviewStyles();
            }

            RenderRows();

            var container = new StackPanel { Spacing = 14 };
            container.Children.Add(new TextBlock
            {
                Text = _isRussian
                    ? "Перетаскивайте карточки за ручку, чтобы менять порядок. Нулевые карточки автоматически скрываются на экране обзора."
                    : "Drag cards by the handle to change their order. Zero-value cards are hidden automatically on the overview screen.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            });
            container.Children.Add(new ScrollViewer
            {
                Content = contentHost,
                MaxHeight = 420
            });

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian ? "Настройки обзора" : "Overview settings",
                PrimaryButtonText = _isRussian ? "Сохранить" : "Save",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = container
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var cardsToSave = orderedCards.Where(selectedCards.Contains).ToList();
            await UpdateOverviewCardsAsync(cardsToSave);
        }

        private static List<string> MoveOverviewCard(List<string> cards, string cardId, int direction)
        {
            var next = cards.ToList();
            var index = next.FindIndex(item => string.Equals(item, cardId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return next;
            }

            var nextIndex = Math.Clamp(index + direction, 0, next.Count - 1);
            if (index == nextIndex)
            {
                return next;
            }

            (next[index], next[nextIndex]) = (next[nextIndex], next[index]);
            return next;
        }

        private static List<string> MoveOverviewCardToTarget(List<string> cards, string cardId, string targetCardId)
        {
            var next = cards.ToList();
            var fromIndex = next.FindIndex(item => string.Equals(item, cardId, StringComparison.OrdinalIgnoreCase));
            var targetIndex = next.FindIndex(item => string.Equals(item, targetCardId, StringComparison.OrdinalIgnoreCase));
            if (fromIndex < 0 || targetIndex < 0 || fromIndex == targetIndex)
            {
                return next;
            }

            next.RemoveAt(fromIndex);
            next.Insert(targetIndex, cardId);
            return next;
        }

        private static List<string> MoveOverviewCardToIndex(List<string> cards, string cardId, int targetIndex)
        {
            var next = cards.ToList();
            var currentIndex = next.FindIndex(item => string.Equals(item, cardId, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                return next;
            }

            var clampedTarget = Math.Clamp(targetIndex, 0, next.Count - 1);
            if (currentIndex == clampedTarget)
            {
                return next;
            }

            next.RemoveAt(currentIndex);
            next.Insert(clampedTarget, cardId);
            return next;
        }

        private async Task UpdateOverviewCardsAsync(List<string> cards)
        {
            if (_financeClient == null || _session == null || string.IsNullOrWhiteSpace(_session.AccessToken))
            {
                return;
            }

            try
            {
                FinanceSettingsText.Text = _isRussian ? "Сохраняем настройки обзора…" : "Saving overview settings…";
                _financeOverview = await _financeClient.UpdateOverviewCardsAsync(_session.AccessToken, cards);
                RenderFinanceContent();
                AnimateFinanceOverviewRefresh();
                SetStatus(_isRussian ? "Настройки обзора сохранены." : "Overview settings saved.", false);
            }
            catch (Exception ex)
            {
                if (!HandleFinanceSessionError(ex))
                {
                    FinanceSettingsText.Text = LocalizeFinanceRequestError(
                        ex.Message,
                        _isRussian ? "Не удалось сохранить настройки обзора." : "Failed to save overview settings.");
                }
            }
        }

        private void AnimateFinanceOverviewRefresh()
        {
            if (_financeTab != FinanceTab.Overview)
            {
                return;
            }

            AnimateElementRefresh(FinanceOverviewBoard);
            if (FinanceTransactionsCard.Visibility == Visibility.Visible)
            {
                AnimateElementRefresh(FinanceTransactionsCard);
            }
        }

        private static void AnimateElementRefresh(UIElement element)
        {
            if (element.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            element.Opacity = 0;
            transform.Y = 18;

            var storyboard = new Storyboard();
            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(280))
            };
            Storyboard.SetTarget(opacityAnimation, element);
            Storyboard.SetTargetProperty(opacityAnimation, nameof(UIElement.Opacity));

            var translateAnimation = new DoubleAnimation
            {
                From = 18,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(320))
            };
            Storyboard.SetTarget(translateAnimation, transform);
            Storyboard.SetTargetProperty(translateAnimation, nameof(TranslateTransform.Y));

            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(translateAnimation);
            storyboard.Begin();
        }

        private static void ToggleTransactionPositionsPanel(Border panel)
        {
            if (panel.Visibility == Visibility.Visible)
            {
                HideTransactionPositionsPanel(panel);
                return;
            }

            ShowTransactionPositionsPanel(panel);
        }

        private static void ShowTransactionPositionsPanel(Border panel)
        {
            if (panel.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                panel.RenderTransform = transform;
            }

            panel.Visibility = Visibility.Visible;
            panel.Opacity = 0;
            transform.Y = -6;

            var storyboard = new Storyboard();
            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(opacityAnimation, panel);
            Storyboard.SetTargetProperty(opacityAnimation, nameof(UIElement.Opacity));

            var translateAnimation = new DoubleAnimation
            {
                From = -6,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(translateAnimation, transform);
            Storyboard.SetTargetProperty(translateAnimation, nameof(TranslateTransform.Y));

            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(translateAnimation);
            storyboard.Begin();
        }

        private static void HideTransactionPositionsPanel(Border panel)
        {
            if (panel.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                panel.RenderTransform = transform;
            }

            var storyboard = new Storyboard();
            var opacityAnimation = new DoubleAnimation
            {
                From = panel.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(140),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(opacityAnimation, panel);
            Storyboard.SetTargetProperty(opacityAnimation, nameof(UIElement.Opacity));

            var translateAnimation = new DoubleAnimation
            {
                From = 0,
                To = -4,
                Duration = TimeSpan.FromMilliseconds(140),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(translateAnimation, transform);
            Storyboard.SetTargetProperty(translateAnimation, nameof(TranslateTransform.Y));

            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(translateAnimation);
            storyboard.Completed += (_, _) =>
            {
                panel.Visibility = Visibility.Collapsed;
                panel.Opacity = 1;
                transform.Y = 0;
            };
            storyboard.Begin();
        }

        private async Task ShowAccountDialogAsync(FinanceAccount? account)
        {
            if (_financeClient == null || _session == null || string.IsNullOrWhiteSpace(_session.AccessToken) || _financeOverview == null)
            {
                return;
            }

            var providers = GetFinanceAccountProviders()
                .Where(provider => !string.Equals(provider.Code, "cash", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var accountKindCombo = new ComboBox();
            accountKindCombo.Items.Add(new ComboBoxItem { Tag = "bank_card", Content = _isRussian ? "Карта" : "Card" });
            accountKindCombo.Items.Add(new ComboBoxItem { Tag = "loan", Content = GetLoanKindLabel() });
            accountKindCombo.Items.Add(new ComboBoxItem { Tag = "cash", Content = _isRussian ? "Наличные" : "Cash" });

            var providerCombo = new ComboBox();
            foreach (var provider in providers)
            {
                providerCombo.Items.Add(new ComboBoxItem { Tag = provider.Code, Content = provider.Label });
            }

            SelectComboItemByTag(accountKindCombo, account?.Kind ?? "bank_card");
            SelectComboItemByTag(providerCombo, account?.ProviderCode ?? "tbank");

            var cardTypeCombo = new ComboBox();
            cardTypeCombo.Items.Add(new ComboBoxItem { Tag = "debit", Content = _isRussian ? "Дебетовая" : "Debit" });
            cardTypeCombo.Items.Add(new ComboBoxItem { Tag = "credit", Content = _isRussian ? "Кредитная" : "Credit" });
            SelectComboItemByTag(cardTypeCombo, account?.CardType ?? "debit");
            var lastFourInput = new TextBox { PlaceholderText = _isRussian ? "1234" : "1234", MaxLength = 4, Text = account?.LastFourDigits ?? string.Empty };
            var amountInput = new TextBox { PlaceholderText = "0", Text = account != null && !IsLoanAccount(account) ? FormatAmountInput(account.BalanceMinor) : string.Empty };
            var loanNameInput = new TextBox
            {
                PlaceholderText = _isRussian ? "Например, Кредит наличными" : "For example, Cash loan",
                Text = account != null && IsLoanAccount(account) ? account.Name : string.Empty
            };
            var loanPrincipalInput = new TextBox { PlaceholderText = "0", Text = account?.LoanPrincipalMinor is long loanPrincipal ? FormatAmountInput(loanPrincipal) : string.Empty };
            var loanDebtInput = new TextBox { PlaceholderText = "0", Text = account?.LoanCurrentDebtMinor is long loanDebt ? FormatAmountInput(loanDebt) : string.Empty };
            var loanInterestInput = new TextBox
            {
                PlaceholderText = _isRussian ? "Например, 26,5" : "For example, 26.5",
                Text = account?.LoanInterestPercent is decimal loanInterest
                    ? loanInterest.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',')
                    : string.Empty
            };
            var loanPaymentAmountInput = new TextBox { PlaceholderText = "0", Text = account?.LoanPaymentAmountMinor is long loanPayment ? FormatAmountInput(loanPayment) : string.Empty };
            var loanPaymentDuePicker = new DatePicker { Date = account?.LoanPaymentDueDate ?? DateTimeOffset.Now.AddMonths(1) };
            var loanRemainingPaymentsInput = new TextBox
            {
                PlaceholderText = _isRussian ? "Например, 48" : "For example, 48",
                Text = account?.LoanRemainingPaymentsCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
            };
            var loanTotalPayableInput = new TextBox
            {
                PlaceholderText = "0",
                Text = account?.LoanTotalPayableMinor is long loanTotalPayable ? FormatAmountInput(loanTotalPayable) : string.Empty
            };
            var loanTotalPaymentsInput = new TextBox
            {
                PlaceholderText = _isRussian ? "Например, 83" : "For example, 83",
                Text = account?.LoanTotalPaymentsCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
            };
            var loanFinalPaymentInput = new TextBox
            {
                PlaceholderText = "0",
                Text = account?.LoanFinalPaymentMinor is long loanFinalPayment ? FormatAmountInput(loanFinalPayment) : string.Empty
            };
            var creditLimitInput = new TextBox { PlaceholderText = "0", Text = account?.CreditLimitMinor is long creditLimit ? FormatAmountInput(creditLimit) : string.Empty };
            var creditDebtInput = new TextBox { PlaceholderText = "0", Text = account?.CreditDebtMinor is long creditDebt ? FormatAmountInput(creditDebt) : string.Empty };
            var creditRequiredPaymentInput = new TextBox { PlaceholderText = "0", Text = account?.CreditRequiredPaymentMinor is long requiredPayment && requiredPayment > 0 ? FormatAmountInput(requiredPayment) : string.Empty };
            var creditPaymentDueCheck = new CheckBox
            {
                Content = _isRussian ? "Указать срок обязательного платежа" : "Set required payment due date",
                IsChecked = account?.CreditPaymentDueDate != null
            };
            var creditPaymentDuePicker = new DatePicker
            {
                Date = account?.CreditPaymentDueDate ?? DateTimeOffset.Now.AddDays(25)
            };
            var creditGracePeriodCheck = new CheckBox
            {
                Content = _isRussian ? "Указать конец льготного периода" : "Set grace period end date",
                IsChecked = account?.CreditGracePeriodEndDate != null
            };
            var creditGracePeriodPicker = new DatePicker
            {
                Date = account?.CreditGracePeriodEndDate ?? DateTimeOffset.Now.AddDays(30)
            };
            var primaryCheck = new CheckBox
            {
                Content = _isRussian ? "Сделать основным карточным счётом" : "Make it the primary card account",
                IsChecked = account?.IsPrimary ?? !_financeOverview.Accounts.Any(item => item.IsPrimary)
            };
            var noteText = CreateMutedText(string.Empty);
            var amountLabel = new TextBlock
            {
                Text = _isRussian ? "Текущий баланс" : "Current balance",
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            };
            var accountKindSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Тип сущности" : "Entity kind",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    accountKindCombo
                }
            };
            var providerSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Банк" : "Bank",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    providerCombo
                }
            };
            var amountSection = new StackPanel { Spacing = 6, Children = { amountLabel, amountInput } };
            var loanNameSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Название кредита" : "Loan name",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanNameInput
                }
            };
            var loanPrincipalSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Сумма кредита" : "Loan principal",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanPrincipalInput
                }
            };
            var loanDebtSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Текущий остаток долга" : "Current debt",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanDebtInput
                }
            };
            var loanInterestSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Процентная ставка, %" : "Interest rate, %",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanInterestInput
                }
            };
            var loanPaymentAmountSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Следующий платёж" : "Next payment",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanPaymentAmountInput
                }
            };
            var loanPaymentDueSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Дата следующего платежа" : "Next payment date",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanPaymentDuePicker
                }
            };
            var loanRemainingPaymentsSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Осталось платежей" : "Payments remaining",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanRemainingPaymentsInput
                }
            };
            var loanTotalPayableSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Всего заплатите" : "Total payable",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanTotalPayableInput
                }
            };
            var loanTotalPaymentsSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Всего платежей" : "Total payments",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanTotalPaymentsInput
                }
            };
            var loanFinalPaymentSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Последний платёж" : "Final payment",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanFinalPaymentInput
                }
            };
            var creditLimitSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Кредитный лимит" : "Credit limit",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    creditLimitInput
                }
            };
            var creditDebtSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Текущий долг" : "Current debt",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    creditDebtInput
                }
            };
            var creditRequiredPaymentSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Минимальный платёж" : "Required payment",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    creditRequiredPaymentInput
                }
            };
            var creditPaymentDueSection = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    creditPaymentDueCheck,
                    creditPaymentDuePicker
                }
            };
            var creditGraceSection = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    creditGracePeriodCheck,
                    creditGracePeriodPicker
                }
            };
            var cardTypeSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Тип карты" : "Card type",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    cardTypeCombo
                }
            };
            var lastFourSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Последние 4 цифры" : "Last 4 digits",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    lastFourInput
                }
            };

            void SyncState()
            {
                var selectedKind = (accountKindCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "bank_card";
                var isCash = string.Equals(selectedKind, "cash", StringComparison.OrdinalIgnoreCase);
                var isLoan = string.Equals(selectedKind, "loan", StringComparison.OrdinalIgnoreCase);
                var isCard = string.Equals(selectedKind, "bank_card", StringComparison.OrdinalIgnoreCase);
                var isCredit = isCard && string.Equals((cardTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string, "credit", StringComparison.OrdinalIgnoreCase);
                providerSection.Visibility = isCash ? Visibility.Collapsed : Visibility.Visible;
                cardTypeSection.Visibility = isCard ? Visibility.Visible : Visibility.Collapsed;
                lastFourSection.Visibility = isCard ? Visibility.Visible : Visibility.Collapsed;
                primaryCheck.Visibility = isCard ? Visibility.Visible : Visibility.Collapsed;
                amountSection.Visibility = isCash || (isCard && !isCredit) ? Visibility.Visible : Visibility.Collapsed;
                loanNameSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanPrincipalSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanDebtSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanInterestSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanPaymentAmountSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanPaymentDueSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanRemainingPaymentsSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanTotalPayableSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanTotalPaymentsSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                loanFinalPaymentSection.Visibility = isLoan ? Visibility.Visible : Visibility.Collapsed;
                creditLimitSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditDebtSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditRequiredPaymentSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditPaymentDueSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditGraceSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                amountLabel.Text = isCash
                    ? (_isRussian ? "Сумма наличных" : "Cash amount")
                    : (_isRussian ? "Текущий баланс" : "Current balance");
                amountInput.IsEnabled = isCash || !isCredit || (account?.BalanceEditable ?? true);
                creditPaymentDuePicker.IsEnabled = creditPaymentDueCheck.IsChecked == true;
                creditGracePeriodPicker.IsEnabled = creditGracePeriodCheck.IsChecked == true;
                noteText.Text = isLoan
                    ? (_isRussian
                        ? "Кредит хранится как отдельная сущность с явным графиком. После платежа долг обновляется по фактическому значению из банка."
                        : "Loans are tracked as a separate entity with an explicit payment plan. After each payment the debt is updated from the actual bank value.")
                    : isCredit
                        ? (_isRussian
                            ? "Кредитка учитывается как долг: покупки увеличивают задолженность, а пополнения и переводы на неё уменьшают её."
                            : "Credit cards are tracked as debt: spending increases the liability, while repayments and incoming transfers reduce it.")
                    : account != null && !account.BalanceEditable
                        ? (_isRussian
                            ? "Сумму нельзя менять после первой транзакции по счёту."
                            : "Balance is locked after the first transaction for this account.")
                        : providers.FirstOrDefault(item => string.Equals(item.Code, (providerCombo.SelectedItem as ComboBoxItem)?.Tag as string, StringComparison.OrdinalIgnoreCase)).Description;
                if (isCash)
                {
                    primaryCheck.IsChecked = false;
                    lastFourInput.Text = string.Empty;
                }
            }

            accountKindCombo.SelectionChanged += (_, _) => SyncState();
            providerCombo.SelectionChanged += (_, _) => SyncState();
            cardTypeCombo.SelectionChanged += (_, _) => SyncState();
            creditPaymentDueCheck.Checked += (_, _) => SyncState();
            creditPaymentDueCheck.Unchecked += (_, _) => SyncState();
            creditGracePeriodCheck.Checked += (_, _) => SyncState();
            creditGracePeriodCheck.Unchecked += (_, _) => SyncState();
            SyncState();

            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock
            {
                Text = _isRussian ? "Параметры сущности" : "Entity settings",
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            });
            content.Children.Add(accountKindSection);
            content.Children.Add(providerSection);
            content.Children.Add(cardTypeSection);
            content.Children.Add(lastFourSection);
            content.Children.Add(amountSection);
            content.Children.Add(loanNameSection);
            content.Children.Add(loanPrincipalSection);
            content.Children.Add(loanDebtSection);
            content.Children.Add(loanInterestSection);
            content.Children.Add(loanPaymentAmountSection);
            content.Children.Add(loanPaymentDueSection);
            content.Children.Add(loanRemainingPaymentsSection);
            content.Children.Add(loanTotalPayableSection);
            content.Children.Add(loanTotalPaymentsSection);
            content.Children.Add(loanFinalPaymentSection);
            content.Children.Add(creditLimitSection);
            content.Children.Add(creditDebtSection);
            content.Children.Add(creditRequiredPaymentSection);
            content.Children.Add(creditPaymentDueSection);
            content.Children.Add(creditGraceSection);
            content.Children.Add(primaryCheck);
            content.Children.Add(noteText);

            var contentScroll = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 640
            };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = account == null
                    ? (_isRussian ? "Новый счёт" : "New account")
                    : (_isRussian ? "Редактирование счёта" : "Edit account"),
                PrimaryButtonText = _isRussian ? "Сохранить" : "Save",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = contentScroll
            };

            void ShowValidationError(string message, Control target)
            {
                noteText.Text = message;
                target.Focus(FocusState.Programmatic);
                target.StartBringIntoView();
            }

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                args.Cancel = true;
                var deferral = args.GetDeferral();
                dialog.IsPrimaryButtonEnabled = false;
                dialog.IsSecondaryButtonEnabled = false;
                try
                {
                    var selectedKind = (accountKindCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "bank_card";
                    var isCash = string.Equals(selectedKind, "cash", StringComparison.OrdinalIgnoreCase);
                    var isLoan = string.Equals(selectedKind, "loan", StringComparison.OrdinalIgnoreCase);
                    var providerCodeToSave = isCash
                        ? "cash"
                        : (providerCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "tbank";
                    var cardType = selectedKind == "bank_card" ? (cardTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "debit" : null;
                    var isCredit = selectedKind == "bank_card" && string.Equals(cardType, "credit", StringComparison.OrdinalIgnoreCase);
                    var lastFourDigits = selectedKind == "bank_card" ? NormalizeLastFourDigits(lastFourInput.Text) : null;
                    if (selectedKind == "bank_card" && lastFourDigits == null)
                    {
                        ShowValidationError(
                            _isRussian ? "Укажите последние 4 цифры карты." : "Enter the last 4 card digits.",
                            lastFourInput);
                        return;
                    }

                    long? balanceMinor = null;
                    long? creditLimitMinor = null;
                    long? creditDebtMinor = null;
                    long? creditRequiredPaymentMinor = null;
                    DateTimeOffset? creditPaymentDueDate = null;
                    DateTimeOffset? creditGracePeriodEndDate = null;
                    string? loanName = null;
                    long? loanPrincipalMinor = null;
                    long? loanCurrentDebtMinor = null;
                    decimal? loanInterestPercent = null;
                    long? loanPaymentAmountMinor = null;
                    DateTimeOffset? loanPaymentDueDate = null;
                    int? loanRemainingPaymentsCount = null;
                    long? loanTotalPayableMinor = null;
                    int? loanTotalPaymentsCount = null;
                    long? loanFinalPaymentMinor = null;

                    if (isCredit)
                    {
                        creditLimitMinor = ParseMoneyInputToMinor(creditLimitInput.Text);
                        creditDebtMinor = ParseMoneyInputToMinor(creditDebtInput.Text);
                        if (creditLimitMinor is null || creditLimitMinor <= 0 || creditDebtMinor is null)
                        {
                            ShowValidationError(
                                _isRussian
                                    ? "Укажите корректные кредитный лимит и текущий долг."
                                    : "Enter valid credit limit and current debt.",
                                creditLimitInput);
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(creditRequiredPaymentInput.Text))
                        {
                            creditRequiredPaymentMinor = ParseMoneyInputToMinor(creditRequiredPaymentInput.Text);
                            if (creditRequiredPaymentMinor is null)
                            {
                                ShowValidationError(
                                    _isRussian
                                        ? "Введите корректный минимальный платёж."
                                        : "Enter a valid required payment.",
                                    creditRequiredPaymentInput);
                                return;
                            }
                        }

                        creditPaymentDueDate = creditPaymentDueCheck.IsChecked == true ? creditPaymentDuePicker.Date : null;
                        creditGracePeriodEndDate = creditGracePeriodCheck.IsChecked == true ? creditGracePeriodPicker.Date : null;
                    }
                    else if (isLoan)
                    {
                        loanName = string.IsNullOrWhiteSpace(loanNameInput.Text) ? null : loanNameInput.Text.Trim();
                        loanPrincipalMinor = ParseMoneyInputToMinor(loanPrincipalInput.Text);
                        loanCurrentDebtMinor = ParseMoneyInputToMinor(loanDebtInput.Text);
                        loanPaymentAmountMinor = ParseMoneyInputToMinor(loanPaymentAmountInput.Text);
                        loanPaymentDueDate = loanPaymentDuePicker.Date;
                        loanTotalPayableMinor = ParseMoneyInputToMinor(loanTotalPayableInput.Text);
                        loanFinalPaymentMinor = ParseMoneyInputToMinor(loanFinalPaymentInput.Text);
                        if (!decimal.TryParse(loanInterestInput.Text.Trim().Replace(" ", string.Empty).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInterest))
                        {
                            ShowValidationError(
                                _isRussian ? "Введите корректную процентную ставку." : "Enter a valid interest rate.",
                                loanInterestInput);
                            return;
                        }

                        loanInterestPercent = parsedInterest;
                        if (!int.TryParse(loanRemainingPaymentsInput.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRemainingPayments))
                        {
                            ShowValidationError(
                                _isRussian ? "Введите корректное число оставшихся платежей." : "Enter a valid number of payments remaining.",
                                loanRemainingPaymentsInput);
                            return;
                        }

                        loanRemainingPaymentsCount = parsedRemainingPayments;
                        if (!int.TryParse(loanTotalPaymentsInput.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotalPayments))
                        {
                            ShowValidationError(
                                _isRussian ? "Введите корректное общее количество платежей." : "Enter a valid total number of payments.",
                                loanTotalPaymentsInput);
                            return;
                        }

                        loanTotalPaymentsCount = parsedTotalPayments;
                        if (string.IsNullOrWhiteSpace(loanName) ||
                            loanPrincipalMinor is null || loanPrincipalMinor <= 0 ||
                            loanCurrentDebtMinor is null || loanCurrentDebtMinor < 0 ||
                            loanPaymentAmountMinor is null || loanPaymentAmountMinor <= 0 ||
                            loanTotalPayableMinor is null || loanTotalPayableMinor <= 0 ||
                            loanFinalPaymentMinor is null || loanFinalPaymentMinor <= 0 ||
                            loanInterestPercent is null || loanInterestPercent < 0 ||
                            loanRemainingPaymentsCount < 0 ||
                            loanTotalPaymentsCount <= 0 ||
                            loanRemainingPaymentsCount > loanTotalPaymentsCount)
                        {
                            ShowValidationError(
                                _isRussian
                                    ? "Заполните все поля кредита корректно. Проверьте график: общее число платежей, остаток и последний платёж."
                                    : "Fill in all loan fields with valid values. Check the total payments, remaining payments, and final payment.",
                                string.IsNullOrWhiteSpace(loanName) ? loanNameInput :
                                loanPrincipalMinor is null || loanPrincipalMinor <= 0 ? loanPrincipalInput :
                                loanCurrentDebtMinor is null || loanCurrentDebtMinor < 0 ? loanDebtInput :
                                loanPaymentAmountMinor is null || loanPaymentAmountMinor <= 0 ? loanPaymentAmountInput :
                                loanTotalPayableMinor is null || loanTotalPayableMinor <= 0 ? loanTotalPayableInput :
                                loanFinalPaymentMinor is null || loanFinalPaymentMinor <= 0 ? loanFinalPaymentInput :
                                loanInterestPercent is null || loanInterestPercent < 0 ? loanInterestInput :
                                loanTotalPaymentsCount <= 0 ? loanTotalPaymentsInput :
                                loanRemainingPaymentsInput);
                            return;
                        }
                    }
                    else
                    {
                        balanceMinor = ParseMoneyInputToMinor(amountInput.Text);
                        if (balanceMinor == null)
                        {
                            ShowValidationError(
                                _isRussian ? "Введите корректную сумму." : "Enter a valid amount.",
                                amountInput);
                            return;
                        }
                    }

                    var accessToken = await GetFreshFinanceAccessTokenAsync();
                    _financeOverview = await _financeClient.UpsertAccountAsync(
                        accessToken,
                        account?.Id,
                        selectedKind,
                        providerCodeToSave,
                        loanName,
                        cardType,
                        lastFourDigits,
                        balanceMinor ?? 0,
                        _financeOverview.DefaultCurrency ?? "RUB",
                        selectedKind == "bank_card" && primaryCheck.IsChecked == true,
                        creditLimitMinor,
                        creditDebtMinor,
                        creditRequiredPaymentMinor,
                        creditPaymentDueDate,
                        creditGracePeriodEndDate,
                        loanPrincipalMinor,
                        loanCurrentDebtMinor,
                        loanInterestPercent,
                        loanPaymentAmountMinor,
                        loanPaymentDueDate,
                        loanRemainingPaymentsCount,
                        loanTotalPayableMinor,
                        loanTotalPaymentsCount,
                        loanFinalPaymentMinor);
                    _financeAnalytics = null;
                    RenderFinanceContent();
                    SetStatus(account == null
                        ? (_isRussian ? "Счёт сохранён." : "Account saved.")
                        : (_isRussian ? "Изменения по счёту сохранены." : "Account changes saved."),
                        false);
                    args.Cancel = false;
                }
                catch (Exception ex)
                {
                    if (HandleFinanceSessionError(ex))
                    {
                        args.Cancel = false;
                        return;
                    }

                    noteText.Text = LocalizeFinanceRequestError(
                        ex.Message,
                        _isRussian ? "Не удалось сохранить счёт." : "Failed to save the account.");
                }
                finally
                {
                    dialog.IsPrimaryButtonEnabled = true;
                    dialog.IsSecondaryButtonEnabled = true;
                    deferral.Complete();
                }
            };

            await dialog.ShowAsync();
        }

        private async Task ShowFinanceMessageDialogAsync(string title, string body)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = title,
                CloseButtonText = _isRussian ? "Закрыть" : "Close",
                Content = new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                    MaxWidth = 420
                }
            };

            try
            {
                await dialog.ShowAsync();
            }
            catch (System.Runtime.InteropServices.COMException ex)
                when (ex.Message.Contains("Only a single ContentDialog can be open at any time.", StringComparison.OrdinalIgnoreCase) ||
                      ex.Message.Contains("An async operation was not properly started.", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus($"{title}: {body}", true);
            }
        }

        private async Task ShowLoanPaymentDialogAsync(Guid? preferredLoanId)
        {
            if (_financeClient == null || _session == null || string.IsNullOrWhiteSpace(_session.AccessToken) || _financeOverview == null)
            {
                return;
            }

            var loanAccounts = GetLoanAccounts();
            if (loanAccounts.Count == 0)
            {
                await ShowFinanceMessageDialogAsync(
                    _isRussian ? "Нет кредитов" : "No loans",
                    _isRussian
                        ? "Сначала добавьте хотя бы один кредитный счёт, чтобы провести платёж."
                        : "Add at least one loan account before recording a payment.");
                return;
            }

            var sourceAccounts = GetLoanPaymentSourceAccounts();
            if (sourceAccounts.Count == 0)
            {
                await ShowFinanceMessageDialogAsync(
                    _isRussian ? "Нет счёта списания" : "No payment source",
                    _isRussian
                        ? "Для оплаты кредита нужен обычный счёт или наличные с положительным балансом."
                        : "A regular account or cash balance is required to pay the loan.");
                return;
            }

            var selectedLoan = loanAccounts.FirstOrDefault(item => preferredLoanId.HasValue && item.Id == preferredLoanId.Value)
                ?? loanAccounts.First();
            var selectedSourceAccount = sourceAccounts.FirstOrDefault(item => item.IsPrimary) ?? sourceAccounts.First();

            var root = new StackPanel
            {
                Spacing = 16,
                MaxWidth = 520
            };

            var introText = CreateMutedText(_isRussian
                ? "Платёж по кредиту уменьшит долг, создаст расход и отдельную транзакцию погашения."
                : "A loan payment reduces the debt, creates an expense, and records a dedicated repayment transaction.");
            root.Children.Add(introText);

            var loanCombo = new ComboBox();
            foreach (var loan in loanAccounts)
            {
                loanCombo.Items.Add(new ComboBoxItem
                {
                    Tag = loan.Id.ToString(),
                    Content = GetFinanceAccountDisplayName(loan)
                });
            }

            SelectComboItemByTag(loanCombo, selectedLoan.Id.ToString());

            var loanPickerSection = new StackPanel
            {
                Spacing = 6,
                Visibility = preferredLoanId.HasValue || loanAccounts.Count == 1 ? Visibility.Collapsed : Visibility.Visible,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Кредит" : "Loan",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    loanCombo
                }
            };
            root.Children.Add(loanPickerSection);

            var loanSummaryTitle = new TextBlock
            {
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            };
            var loanSummarySubtitle = CreateMutedText(string.Empty);
            var loanSummaryBody = CreateMutedText(string.Empty);

            var loanSummaryCard = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(14, 12, 14, 12),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        loanSummaryTitle,
                        loanSummarySubtitle,
                        loanSummaryBody
                    }
                }
            };
            root.Children.Add(loanSummaryCard);

            var sourceCombo = new ComboBox();
            foreach (var sourceAccount in sourceAccounts)
            {
                sourceCombo.Items.Add(new ComboBoxItem
                {
                    Tag = sourceAccount.Id.ToString(),
                    Content = GetFinanceAccountDisplayName(sourceAccount)
                });
            }

            SelectComboItemByTag(sourceCombo, selectedSourceAccount.Id.ToString());

            root.Children.Add(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Счёт списания" : "Source account",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    sourceCombo
                }
            });

            var paymentTypeCombo = new ComboBox();
            paymentTypeCombo.Items.Add(new ComboBoxItem
            {
                Tag = "standard",
                Content = _isRussian ? "Стандартный платёж" : "Standard payment"
            });
            paymentTypeCombo.Items.Add(new ComboBoxItem
            {
                Tag = "custom",
                Content = _isRussian ? "Платёж с другой суммой" : "Custom amount"
            });

            var amountInput = new TextBox
            {
                PlaceholderText = _isRussian ? "Например, 9500" : "For example, 9500"
            };
            var newDebtInput = new TextBox
            {
                PlaceholderText = _isRussian ? "Введите остаток долга после платежа" : "Enter the outstanding debt after payment"
            };
            var standardPaymentHint = CreateMutedText(string.Empty);
            var amountSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Сумма платежа" : "Payment amount",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    standardPaymentHint,
                    amountInput
                }
            };
            var newDebtSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Остаток долга после платежа" : "Outstanding debt after payment",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    CreateMutedText(_isRussian
                        ? "Берём фактическое значение из банка после проведения платежа."
                        : "Use the actual value from the bank after the payment is posted."),
                    newDebtInput
                }
            };
            root.Children.Add(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Режим платежа" : "Payment mode",
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                    },
                    paymentTypeCombo,
                    amountSection,
                    newDebtSection
                }
            });

            var datePicker = new DatePicker
            {
                Date = DateTimeOffset.Now,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var timePicker = new TimePicker
            {
                Time = DateTimeOffset.Now.TimeOfDay,
                ClockIdentifier = "24HourClock",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            root.Children.Add(new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = _isRussian ? "Дата платежа" : "Payment date",
                                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                            },
                            datePicker
                        }
                    },
                    new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = _isRussian ? "Время платежа" : "Payment time",
                                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                            },
                            timePicker
                        }
                    }
                }
            });

            var noteText = CreateMutedText(string.Empty);
            root.Children.Add(noteText);

            var contentScroll = new ScrollViewer
            {
                Content = root,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 640
            };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian ? "Платёж по кредиту" : "Loan payment",
                PrimaryButtonText = _isRussian ? "Оплатить" : "Pay",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = contentScroll
            };

            void ShowValidationError(string message, Control target)
            {
                noteText.Text = message;
                target.Focus(FocusState.Programmatic);
                target.StartBringIntoView();
            }

            void RefreshLoanSummary()
            {
                loanSummaryTitle.Text = GetFinanceAccountTitle(selectedLoan);
                loanSummarySubtitle.Text = GetFinanceAccountSubtitle(selectedLoan);

                var summaryParts = new List<string>
                {
                    (_isRussian ? "Текущий долг" : "Current debt") + ": " + FormatMoney(selectedLoan.LoanCurrentDebtMinor ?? selectedLoan.BalanceMinor, selectedLoan.Currency)
                };

                if (selectedLoan.LoanPaymentAmountMinor is long paymentMinor)
                {
                    summaryParts.Add((_isRussian ? "Обычный платёж" : "Regular payment") + ": " + FormatMoney(paymentMinor, selectedLoan.Currency));
                }

                if (selectedLoan.LoanFinalPaymentMinor is long finalPaymentMinor)
                {
                    summaryParts.Add((_isRussian ? "Последний платёж" : "Final payment") + ": " + FormatMoney(finalPaymentMinor, selectedLoan.Currency));
                }

                if (selectedLoan.LoanPaymentDueDate is DateTimeOffset dueDate)
                {
                    summaryParts.Add((_isRussian ? "Ближайший платёж" : "Next due date") + ": " + dueDate.ToString("d", _isRussian ? new CultureInfo("ru-RU") : new CultureInfo("en-US")));
                }

                if (selectedLoan.LoanRemainingPaymentsCount is int remainingCount)
                {
                    summaryParts.Add(_isRussian ? $"Осталось платежей: {remainingCount}" : $"Payments left: {remainingCount}");
                }

                if (selectedLoan.LoanTotalPaymentsCount is int totalPayments)
                {
                    var paidPayments = GetLoanPaidPaymentsCount(selectedLoan) ?? 0;
                    summaryParts.Add(_isRussian
                        ? $"Платежей пройдено: {paidPayments} из {totalPayments}"
                        : $"Payments completed: {paidPayments} of {totalPayments}");
                }

                loanSummaryBody.Text = string.Join(" · ", summaryParts);
            }

            void RefreshPaymentModeState()
            {
                var scheduledPaymentAmountMinor = GetLoanScheduledPaymentAmount(selectedLoan);
                var hasStandardAmount = scheduledPaymentAmountMinor is > 0;
                if (!hasStandardAmount)
                {
                    SelectComboItemByTag(paymentTypeCombo, "custom");
                }

                if (paymentTypeCombo.SelectedItem is not ComboBoxItem selectedModeItem || selectedModeItem.Tag is not string mode)
                {
                    mode = hasStandardAmount ? "standard" : "custom";
                    SelectComboItemByTag(paymentTypeCombo, mode);
                }

                var useStandard = string.Equals((paymentTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string, "standard", StringComparison.OrdinalIgnoreCase) && hasStandardAmount;
                standardPaymentHint.Text = useStandard
                    ? (_isRussian
                        ? $"Будет использован следующий платёж по графику: {FormatMoney(scheduledPaymentAmountMinor ?? 0, selectedLoan.Currency)}"
                        : $"The next scheduled payment will be used: {FormatMoney(scheduledPaymentAmountMinor ?? 0, selectedLoan.Currency)}")
                    : (_isRussian
                        ? "Введите сумму, если платёж отличается от стандартного."
                        : "Enter a different amount for a non-standard payment.");
                amountInput.Visibility = useStandard ? Visibility.Collapsed : Visibility.Visible;
            }

            newDebtInput.Text = FormatAmountInput(selectedLoan.LoanCurrentDebtMinor ?? selectedLoan.BalanceMinor);
            SelectComboItemByTag(paymentTypeCombo, GetLoanScheduledPaymentAmount(selectedLoan) is > 0 ? "standard" : "custom");
            RefreshLoanSummary();
            RefreshPaymentModeState();

            loanCombo.SelectionChanged += (_, _) =>
            {
                if (loanCombo.SelectedItem is ComboBoxItem item &&
                    item.Tag is string rawId &&
                    Guid.TryParse(rawId, out var loanId))
                {
                    var nextLoan = loanAccounts.FirstOrDefault(account => account.Id == loanId);
                    if (nextLoan != null)
                    {
                        selectedLoan = nextLoan;
                        newDebtInput.Text = FormatAmountInput(selectedLoan.LoanCurrentDebtMinor ?? selectedLoan.BalanceMinor);
                        RefreshLoanSummary();
                        RefreshPaymentModeState();
                    }
                }
            };

            paymentTypeCombo.SelectionChanged += (_, _) => RefreshPaymentModeState();

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                args.Cancel = true;
                var deferral = args.GetDeferral();
                dialog.IsPrimaryButtonEnabled = false;
                dialog.IsSecondaryButtonEnabled = false;
                try
                {
                    if (sourceCombo.SelectedItem is not ComboBoxItem sourceItem ||
                        sourceItem.Tag is not string sourceRawId ||
                        !Guid.TryParse(sourceRawId, out var sourceAccountId))
                    {
                        ShowValidationError(
                            _isRussian ? "Выберите счёт списания." : "Choose the source account.",
                            sourceCombo);
                        return;
                    }

                    selectedSourceAccount = sourceAccounts.First(account => account.Id == sourceAccountId);

                    var mode = (paymentTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "custom";
                    long? amountMinor = string.Equals(mode, "standard", StringComparison.OrdinalIgnoreCase)
                        ? GetLoanScheduledPaymentAmount(selectedLoan)
                        : ParseMoneyInputToMinor(amountInput.Text);
                    var currentDebtMinor = selectedLoan.LoanCurrentDebtMinor ?? selectedLoan.BalanceMinor;
                    var newCurrentDebtMinor = ParseMoneyInputToMinor(newDebtInput.Text);

                    if (amountMinor is null || amountMinor <= 0)
                    {
                        ShowValidationError(
                            _isRussian ? "Введите корректную сумму платежа." : "Enter a valid payment amount.",
                            string.Equals(mode, "standard", StringComparison.OrdinalIgnoreCase) ? paymentTypeCombo : amountInput);
                        return;
                    }

                    if (newCurrentDebtMinor is null || newCurrentDebtMinor < 0)
                    {
                        ShowValidationError(
                            _isRussian ? "Введите корректный остаток долга после платежа." : "Enter a valid outstanding debt after payment.",
                            newDebtInput);
                        return;
                    }

                    if (currentDebtMinor <= 0)
                    {
                        ShowValidationError(
                            _isRussian ? "По этому кредиту уже нет остатка долга." : "This loan has no outstanding debt left.",
                            paymentTypeCombo);
                        return;
                    }

                    if (amountMinor > currentDebtMinor)
                    {
                        ShowValidationError(
                            _isRussian ? "Сумма платежа не может превышать текущий остаток долга." : "The payment amount cannot exceed the outstanding debt.",
                            string.Equals(mode, "standard", StringComparison.OrdinalIgnoreCase) ? paymentTypeCombo : amountInput);
                        return;
                    }

                    if (newCurrentDebtMinor > currentDebtMinor)
                    {
                        ShowValidationError(
                            _isRussian ? "Новый остаток долга не может быть больше текущего." : "The new outstanding debt cannot be greater than the current debt.",
                            newDebtInput);
                        return;
                    }

                    var happenedAt = datePicker.Date.Date + timePicker.Time;
                    var request = new FinanceRecordLoanPaymentRequest
                    {
                        SourceAccountId = selectedSourceAccount.Id,
                        LoanAccountId = selectedLoan.Id,
                        Title = _isRussian ? "Платёж по кредиту" : "Loan payment",
                        AmountMinor = amountMinor.Value,
                        NewCurrentDebtMinor = newCurrentDebtMinor.Value,
                        HappenedAt = happenedAt,
                        SourceType = "manual"
                    };

                    var balanceCheckRequest = new FinanceCreateTransactionRequest
                    {
                        ClientRequestId = Guid.NewGuid(),
                        AccountId = selectedSourceAccount.Id,
                        Direction = "expense",
                        Title = request.Title,
                        AmountMinor = request.AmountMinor,
                        Currency = selectedSourceAccount.Currency,
                        HappenedAt = happenedAt,
                        SourceType = "manual"
                    };
                    if (!TryValidateFinanceDraftBalances(new[] { balanceCheckRequest }, out var balanceError))
                    {
                        ShowValidationError(balanceError, sourceCombo);
                        return;
                    }

                    var accessToken = await GetFreshFinanceAccessTokenAsync();
                    _financeOverview = await _financeClient.RecordLoanPaymentAsync(accessToken, request);
                    _financeAnalytics = null;
                    await LoadFinanceTransactionsMonthAsync(_financeSelectedTransactionsMonth ?? happenedAt.ToString("yyyy-MM"), true);
                    RenderFinanceContent();
                    SetStatus(
                        _isRussian
                            ? $"Платёж по кредиту «{GetFinanceAccountTitle(selectedLoan)}» сохранён."
                            : $"Loan payment for \"{GetFinanceAccountTitle(selectedLoan)}\" has been saved.",
                        false);
                    args.Cancel = false;
                }
                catch (Exception ex)
                {
                    if (HandleFinanceSessionError(ex))
                    {
                        args.Cancel = false;
                        return;
                    }

                    noteText.Text = LocalizeFinanceRequestError(
                        ex.Message,
                        _isRussian ? "Не удалось сохранить платёж по кредиту." : "Failed to save the loan payment.");
                }
                finally
                {
                    dialog.IsPrimaryButtonEnabled = true;
                    dialog.IsSecondaryButtonEnabled = true;
                    deferral.Complete();
                }
            };

            await dialog.ShowAsync();
        }

        private async Task<string> GetFreshAccessTokenAsync()
        {
            if (_session == null || string.IsNullOrWhiteSpace(_session.AccessToken))
            {
                throw new InvalidOperationException(_isRussian ? "Нет активной сессии." : "No active session.");
            }

            if (!string.IsNullOrWhiteSpace(_session.RefreshToken))
            {
                try
                {
                    _session = await _authClient.RefreshSessionAsync(_session.RefreshToken);
                    PersistSession();
                }
                catch
                {
                    // Fall back to the existing access token if refresh is unavailable.
                }
            }

            return _session.AccessToken;
        }

        private Task<string> GetFreshFinanceAccessTokenAsync() => GetFreshAccessTokenAsync();

        private async Task ShowFinanceTransactionFlowAsync()
        {
            if (_financeClient == null || _session == null || _financeOverview == null)
            {
                return;
            }

            if (_financeCategories.Count == 0)
            {
                await LoadFinanceOverviewAsync();
            }

            var sourceType = await ShowFinanceTransactionSourceChoiceAsync();
            if (sourceType == null)
            {
                return;
            }

            var drafts = new List<TransactionDraftState>();
            var warnings = new List<string>();
            var importSourceLabel = sourceType == "photo"
                ? (_isRussian ? "фото" : "photo")
                : (_isRussian ? "файл" : "file");

            if (sourceType == "manual")
            {
                drafts.Add(CreateManualTransactionDraft());
            }
            else
            {
                var file = sourceType == "photo"
                    ? await CaptureTransactionPhotoAsync()
                    : await PickTransactionFileAsync(photoOnly: false);
                if (file == null)
                {
                    return;
                }

                var localPath = await EnsureLocalImportPathAsync(file);
                FinanceImportResult? importResult = null;
                try
                {
                    importResult = await RunFinanceImportWithProgressAsync(
                        localPath,
                        file.Name,
                        GetFileContentType(file.Name),
                        sourceType);
                }
                catch (Exception ex)
                {
                    var issue = ExtractFinanceImportIssue(ex.Message);
                    warnings.AddRange(LocalizeFinanceWarnings(issue.Warnings));

                    if (ShouldOpenEditorForImportIssue(issue.Error))
                    {
                        warnings.Insert(0, LocalizeFinanceImportIssue(issue.Error, importSourceLabel));
                        drafts.Add(CreateManualTransactionDraft(sourceType));
                    }
                    else
                    {
                        throw new InvalidOperationException(LocalizeFinanceImportIssue(issue.Error, importSourceLabel), ex);
                    }
                }

                if (importResult != null)
                {
                    warnings.AddRange(LocalizeFinanceWarnings(importResult.Warnings));
                    drafts = importResult.Drafts.Count > 0
                        ? importResult.Drafts.Select(draft => CreateDraftFromImport(draft)).ToList()
                        : new List<TransactionDraftState> { CreateManualTransactionDraft(sourceType) };

                    if (importResult.Drafts.Count == 0)
                    {
                        warnings.Insert(0, _isRussian
                            ? $"Не удалось уверенно извлечь транзакцию из {importSourceLabel}. Проверьте поля и заполните форму вручную."
                            : $"We couldn't confidently extract a transaction from the {importSourceLabel}. Review the fields and complete the form manually.");
                    }
                }
            }

            warnings = warnings
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var saved = await ShowFinanceTransactionEditorAsync(drafts, warnings);
            if (saved)
            {
                await LoadFinanceOverviewAsync();
            }
        }

        private async Task<FinanceImportResult> RunFinanceImportWithProgressAsync(
            string filePath,
            string fileName,
            string contentType,
            string sourceType)
        {
            var mutedBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"];
            var inkBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"];
            var strokeBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
            var cardBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"];
            var accentBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"];

            var progressValueText = new TextBlock
            {
                Text = "6%",
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = mutedBrush,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 6,
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(999)
            };

            var stageText = new TextBlock
            {
                Text = _isRussian
                    ? "Подготавливаем документ."
                    : "Preparing the document.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = inkBrush,
                FontSize = 14
            };

            var helperText = new TextBlock
            {
                Text = _isRussian
                    ? "Поля откроются сразу после распознавания."
                    : "The form will open as soon as recognition finishes.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = mutedBrush,
                FontSize = 12
            };

            var progressHeaderGrid = new Grid();
            progressHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            progressHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var progressIconTile = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(44, 0, 204, 136)),
                Child = new FontIcon
                {
                    Glyph = sourceType == "photo" ? "\uE722" : "\uE8B7",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 18,
                    Foreground = accentBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var progressHeaderText = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(14, 0, 0, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian
                            ? "Готовим черновик транзакции"
                            : "Preparing the transaction draft",
                        FontSize = 17,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = inkBrush
                    },
                    new TextBlock
                    {
                        Text = _isRussian
                            ? "Извлекаем сумму, дату и детали операции."
                            : "Extracting the amount, date, and transaction details.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = mutedBrush,
                        FontSize = 12
                    }
                }
            };
            progressHeaderGrid.Children.Add(progressIconTile);
            Grid.SetColumn(progressHeaderText, 1);
            progressHeaderGrid.Children.Add(progressHeaderText);

            var fileText = new TextBlock
            {
                Text = fileName,
                Foreground = mutedBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 360
            };

            var progressRow = new Grid();
            progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            progressRow.Children.Add(progressBar);
            Grid.SetColumn(progressValueText, 1);
            progressValueText.Margin = new Thickness(12, 0, 0, 0);
            progressRow.Children.Add(progressValueText);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = string.Empty,
                Content = new StackPanel
                {
                    Width = 420,
                    Spacing = 0,
                    Children =
                    {
                        new Border
                        {
                            Background = cardBrush,
                            BorderBrush = strokeBrush,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(24),
                            Padding = new Thickness(20),
                            Child = new StackPanel
                            {
                                Spacing = 14,
                                Children =
                                {
                                    progressHeaderGrid,
                                    fileText,
                                    stageText,
                                    progressRow,
                                    helperText
                                }
                            }
                        },
                    }
                }
            };

            var completion = new TaskCompletionSource<FinanceImportResult>();
            var isComplete = false;

            dialog.Opened += async (_, _) =>
            {
                var progressLoop = AnimateFinanceImportProgressAsync(progressBar, progressValueText, stageText, () => isComplete);
                try
                {
                    var result = await ProcessReceiptImportWithRetryAsync(filePath, fileName, contentType, sourceType);
                    stageText.Text = _isRussian
                        ? "Открываем форму для проверки."
                        : "Opening the form for review.";
                    helperText.Text = _isRussian
                        ? "Ещё мгновение."
                        : "Just a moment.";
                    progressBar.Value = 100;
                    progressValueText.Text = "100%";
                    completion.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
                finally
                {
                    isComplete = true;
                    await progressLoop;
                    await Task.Delay(140);
                    dialog.Hide();
                }
            };

            await dialog.ShowAsync();
            return await completion.Task;
        }

        private async Task AnimateFinanceImportProgressAsync(
            ProgressBar progressBar,
            TextBlock progressValueText,
            TextBlock stageText,
            Func<bool> isComplete)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!isComplete())
            {
                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                var current = elapsedSeconds switch
                {
                    < 1.2 => 6 + (elapsedSeconds / 1.2) * 24,
                    < 3.8 => 30 + ((elapsedSeconds - 1.2) / 2.6) * 34,
                    < 7.2 => 64 + ((elapsedSeconds - 3.8) / 3.4) * 22,
                    _ => 86 + Math.Min((elapsedSeconds - 7.2) * 1.4, 8)
                };

                progressBar.Value = Math.Min(current, 94);
                progressValueText.Text = $"{Math.Round(progressBar.Value):0}%";
                stageText.Text = progressBar.Value switch
                {
                    < 28 => _isRussian
                        ? "Проверяем файл и готовим его к распознаванию."
                        : "Checking the file and preparing it for recognition.",
                    < 68 => _isRussian
                        ? "Извлекаем сумму, дату и получателя."
                        : "Extracting the amount, date, and merchant.",
                    _ => _isRussian
                        ? "Собираем черновик и подготавливаем поля."
                        : "Building the draft and preparing the fields."
                };
                await Task.Delay(33);
            }
        }

        private async Task<string?> ShowFinanceTransactionSourceChoiceAsync()
        {
            string? result = null;
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = string.Empty
            };

            var root = new StackPanel
            {
                Spacing = 12,
                Width = 420
            };

            void AddChoiceButton(string key, string eyebrow, string title, string body)
            {
                var accentBrush = key == "photo"
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                    : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"];
                var normalBackground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"];
                var hoverBackground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"];
                var pressedBackground = new SolidColorBrush(Color.FromArgb(210, 58, 58, 64));
                var normalBorder = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
                var hoverBorder = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));

                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0)
                };

                button.Resources["ButtonBackground"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.Resources["ButtonBackgroundDisabled"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.Resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.Resources["ButtonBorderBrushDisabled"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

                var surface = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = normalBackground,
                    BorderBrush = normalBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(16, 14, 16, 14)
                };

                var content = new StackPanel { Spacing = 6 };
                content.Children.Add(new TextBlock
                {
                    Text = eyebrow,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = accentBrush
                });
                content.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
                content.Children.Add(new TextBlock
                {
                    Text = body,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                });

                surface.Child = content;
                button.Content = surface;

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

                button.Click += (_, _) =>
                {
                    result = key;
                    dialog.Hide();
                };
                root.Children.Add(button);
            }

            AddChoiceButton(
                "manual",
                _isRussian ? "РУЧНОЙ ВВОД" : "MANUAL",
                _isRussian ? "Вручную" : "Manual",
                _isRussian ? "Заполнить транзакцию и позиции руками." : "Fill the transaction and items by hand.");
            AddChoiceButton(
                "photo",
                _isRussian ? "КАМЕРА" : "CAMERA",
                _isRussian ? "Фото" : "Photo",
                _isRussian ? "Открыть системную камеру и затем выбрать сделанный снимок." : "Open the system camera and then choose the captured photo.");
            AddChoiceButton(
                "file",
                _isRussian ? "ИМПОРТ" : "IMPORT",
                _isRussian ? "Файл" : "File",
                _isRussian ? "Выбрать изображение, PDF или EML." : "Choose an image, PDF, or EML.");

            dialog.Content = root;
            await dialog.ShowAsync();
            return result;
        }

        private async Task<StorageFile?> PickTransactionFileAsync(bool photoOnly)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };

            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".webp");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            if (!photoOnly)
            {
                picker.FileTypeFilter.Add(".pdf");
                picker.FileTypeFilter.Add(".eml");
            }

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            return await picker.PickSingleFileAsync();
        }

        private async Task<StorageFile?> CaptureTransactionPhotoAsync()
        {
            var cameras = await DeviceInformation.FindAllAsync(MediaDevice.GetVideoCaptureSelector());
            var camera = cameras.FirstOrDefault();
            if (camera == null)
            {
                throw new InvalidOperationException(_isRussian
                    ? "Камера не найдена."
                    : "No camera device was found.");
            }

            MediaCapture? mediaCapture = null;
            MediaPlayer? mediaPlayer = null;
            StorageFile? capturedFile = null;
            var errorText = new TextBlock
            {
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ErrorTextBrush"],
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian ? "Сделайте снимок" : "Take a photo",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                PrimaryButtonText = _isRussian ? "Сделать снимок" : "Capture photo",
                DefaultButton = ContentDialogButton.Primary,
            };

            var previewElement = new MediaPlayerElement
            {
                AreTransportControlsEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 320
            };

            var content = new StackPanel
            {
                Spacing = 12,
                Width = 520
            };

            content.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PanelStrongBrush"],
                Padding = new Thickness(0),
                Child = previewElement
            });
            content.Children.Add(new TextBlock
            {
                Text = _isRussian
                    ? "Наведите камеру на чек и нажмите кнопку снимка."
                    : "Point the camera at the receipt and press capture.",
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(errorText);

            dialog.Content = content;

            try
            {
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = camera.Id,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly
                });

                var previewSource = mediaCapture.FrameSources.Values.FirstOrDefault(source =>
                    source.Info.SourceKind == MediaFrameSourceKind.Color &&
                    source.Info.MediaStreamType == MediaStreamType.VideoPreview)
                    ?? mediaCapture.FrameSources.Values.FirstOrDefault(source =>
                        source.Info.SourceKind == MediaFrameSourceKind.Color &&
                        source.Info.MediaStreamType == MediaStreamType.VideoRecord);

                if (previewSource == null)
                {
                    throw new InvalidOperationException(_isRussian
                        ? "Не удалось открыть видеопоток камеры."
                        : "Unable to open the camera preview stream.");
                }

                mediaPlayer = new MediaPlayer
                {
                    AutoPlay = true,
                    RealTimePlayback = true,
                    Source = MediaSource.CreateFromMediaFrameSource(previewSource)
                };
                previewElement.SetMediaPlayer(mediaPlayer);

                dialog.PrimaryButtonClick += async (_, args) =>
                {
                    var deferral = args.GetDeferral();
                    try
                    {
                        capturedFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                            $"receipt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.jpg",
                            CreationCollisionOption.GenerateUniqueName);
                        await mediaCapture.CapturePhotoToStorageFileAsync(
                            ImageEncodingProperties.CreateJpeg(),
                            capturedFile);
                    }
                    catch (Exception ex)
                    {
                        args.Cancel = true;
                        errorText.Text = GetFriendlyFinanceFlowError(ex);
                        errorText.Visibility = Visibility.Visible;
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                };

                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary ? capturedFile : null;
            }
            finally
            {
                mediaPlayer?.Pause();
                mediaPlayer?.Dispose();
                mediaCapture?.Dispose();
            }
        }

        private async Task<FinanceImportResult> ProcessReceiptImportWithRetryAsync(
            string filePath,
            string fileName,
            string contentType,
            string sourceType)
        {
            var accessToken = await GetFreshFinanceAccessTokenAsync();

            var firstAttempt = await _financeClient!.ProcessReceiptImportAttemptAsync(
                accessToken,
                filePath,
                fileName,
                contentType,
                sourceType);

            if (firstAttempt.IsSuccessStatusCode && firstAttempt.Result != null)
            {
                return firstAttempt.Result;
            }

            if (firstAttempt.StatusCode != 401 && !IsInvalidJwtError(firstAttempt.Payload))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(firstAttempt.Payload)
                    ? "Receipt import failed."
                    : firstAttempt.Payload);
            }

            if (_session == null || string.IsNullOrWhiteSpace(_session.RefreshToken))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(firstAttempt.Payload)
                    ? "Receipt import failed."
                    : firstAttempt.Payload);
            }

            _session = await _authClient.RefreshSessionAsync(_session.RefreshToken);
            PersistSession();

            var secondAttempt = await _financeClient.ProcessReceiptImportAttemptAsync(
                _session.AccessToken,
                filePath,
                fileName,
                contentType,
                sourceType);

            if (secondAttempt.IsSuccessStatusCode && secondAttempt.Result != null)
            {
                return secondAttempt.Result;
            }

            throw new InvalidOperationException(string.IsNullOrWhiteSpace(secondAttempt.Payload)
                ? "Receipt import failed."
                : secondAttempt.Payload);
        }

        private static bool IsInvalidJwtError(string? message) =>
            !string.IsNullOrWhiteSpace(message) &&
            (message.Contains("\"code\":401", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("invalid jwt", StringComparison.OrdinalIgnoreCase));

        private static string ExtractJsonErrorMessage(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("error", out var error))
                    {
                        return error.GetString() ?? payload;
                    }

                    if (root.TryGetProperty("message", out var message))
                    {
                        return message.GetString() ?? payload;
                    }
                }
            }
            catch
            {
            }

            return payload;
        }

        private static FinanceImportIssue ExtractFinanceImportIssue(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return new FinanceImportIssue("Receipt import failed.", new List<string>());
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var error = root.TryGetProperty("error", out var errorElement)
                        ? errorElement.GetString()
                        : root.TryGetProperty("message", out var messageElement)
                            ? messageElement.GetString()
                            : payload;

                    var warnings = new List<string>();
                    if (root.TryGetProperty("warnings", out var warningsElement) &&
                        warningsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var warning in warningsElement.EnumerateArray())
                        {
                            var warningText = warning.GetString();
                            if (!string.IsNullOrWhiteSpace(warningText))
                            {
                                warnings.Add(warningText);
                            }
                        }
                    }

                    return new FinanceImportIssue(error ?? payload, warnings);
                }
            }
            catch
            {
            }

            return new FinanceImportIssue(payload, new List<string>());
        }

        private bool ShouldOpenEditorForImportIssue(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            return message.Contains("No transactions were detected", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("Gemini returned no JSON payload", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("Unable to parse import response", StringComparison.OrdinalIgnoreCase);
        }

        private List<string> LocalizeFinanceWarnings(IEnumerable<string> warnings)
        {
            var localized = new List<string>();
            foreach (var warning in warnings)
            {
                if (string.IsNullOrWhiteSpace(warning))
                {
                    continue;
                }

                if (warning.Contains("partial", StringComparison.OrdinalIgnoreCase) ||
                    warning.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                    warning.Contains("could not", StringComparison.OrdinalIgnoreCase))
                {
                    localized.Add(_isRussian
                        ? "Удалось распознать только часть данных. Проверьте сумму, дату, описание и категории перед сохранением."
                        : "Only part of the data was recognized. Review the amount, date, description, and categories before saving.");
                    continue;
                }

                if (warning.Contains("multiple", StringComparison.OrdinalIgnoreCase))
                {
                    localized.Add(_isRussian
                        ? "В документе найдено несколько возможных покупок или операций. Проверьте выбранный черновик перед сохранением."
                        : "The document appears to contain multiple possible purchases or transactions. Review the selected draft before saving.");
                    continue;
                }

                localized.Add(_isRussian
                    ? "Некоторые поля распознаны неуверенно. Проверьте заполнение перед сохранением."
                    : "Some fields were recognized with low confidence. Review them before saving.");
            }

            return localized.Distinct(StringComparer.Ordinal).ToList();
        }

        private string LocalizeFinanceImportIssue(string? message, string importSourceLabel)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return _isRussian
                    ? "Не удалось обработать документ. Проверьте файл и попробуйте снова."
                    : "We couldn't process the document. Check the file and try again.";
            }

            if (IsInvalidJwtError(message))
            {
                return _isRussian
                    ? "Сессия устарела. Попробуйте повторить действие ещё раз."
                    : "Your session expired. Please try again.";
            }

            if (message.Contains("Gemini API key is missing", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? "Добавьте Gemini API Key в настройках, чтобы включить распознавание фото и файлов."
                    : "Add a Gemini API key in Settings to enable photo and file recognition.";
            }

            if (message.Contains("AI enhancements are disabled", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? "Включите AI-улучшения в настройках, чтобы распознавать фото и документы автоматически."
                    : "Enable AI enhancements in Settings to recognize photos and documents automatically.";
            }

            if (message.Contains("No transactions were detected in the document", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? $"Не удалось найти транзакцию в {importSourceLabel}. Мы открыли форму и сохранили всё, что удалось распознать."
                    : $"We couldn't detect a transaction in the {importSourceLabel}. We opened the form and kept anything we could recognize.";
            }

            if (message.Contains("Gemini returned no JSON payload", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Unable to parse import response", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? $"Не удалось надёжно распознать данные из {importSourceLabel}. Форма открыта для ручной проверки и заполнения."
                    : $"We couldn't reliably recognize the data from the {importSourceLabel}. The form is open for manual review and completion.";
            }

            if (message.Contains("Supported formats: image, PDF, and EML.", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? "Поддерживаются изображения, PDF и EML."
                    : "Supported formats are images, PDF, and EML.";
            }

            if (message.Contains("File size must be between 1 byte and", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? "Размер файла выходит за допустимые пределы."
                    : "The file size is outside the supported range.";
            }

            if (message.Contains("Gemini file processing failed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Gemini file is still", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian
                    ? "PDF ещё обрабатывается сервисом распознавания. Повторите попытку через несколько секунд."
                    : "The recognition service is still processing the PDF. Try again in a few seconds.";
            }

            if (_isRussian)
            {
                return "Не удалось обработать документ. Проверьте файл и попробуйте снова.";
            }

            return message;
        }

    }
}

