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
        private string GetFriendlyFinanceFlowError(Exception ex)
        {
            var message = ExtractJsonErrorMessage(ex.Message);
            return LocalizeFinanceImportIssue(message, _isRussian ? "документе" : "document");
        }

        private string LocalizeFinanceRequestError(string? raw, string fallback)
        {
            var message = ExtractJsonErrorMessage(raw);
            if (string.IsNullOrWhiteSpace(message))
            {
                return fallback;
            }

            var normalized = message.ToLowerInvariant();
            if (normalized.Contains("insufficient available funds"))
            {
                return _isRussian
                    ? "На счёте меньше средств, чем указано в операции. Проверьте сумму или выберите другой счёт."
                    : "There are not enough funds on the account for this operation.";
            }

            if (normalized.Contains("account type and identity cannot be changed after first transaction"))
            {
                return _isRussian
                    ? "После первой транзакции нельзя менять тип счёта, банк или идентификационные данные."
                    : "The account kind, provider, and identity fields cannot be changed after the first transaction.";
            }

            if (normalized.Contains("account type and card identity cannot be changed after first transaction"))
            {
                return _isRussian
                    ? "После первой транзакции нельзя менять банк, тип карты или последние 4 цифры."
                    : "Bank, card type, and last 4 digits cannot be changed after the first transaction.";
            }

            if (normalized.Contains("balance cannot be changed after first transaction"))
            {
                return _isRussian
                    ? "После первой транзакции нельзя менять стартовый баланс счёта."
                    : "The opening balance cannot be changed after the first transaction.";
            }

            if (normalized.Contains("loan name is required") ||
                normalized.Contains("loan principal must be positive") ||
                normalized.Contains("loan current debt must be zero or positive") ||
                normalized.Contains("loan interest percent must be zero or positive") ||
                normalized.Contains("loan payment amount must be positive") ||
                normalized.Contains("loan payment due date is required") ||
                normalized.Contains("loan remaining payments count must be zero or positive") ||
                normalized.Contains("loan total payable must be positive") ||
                normalized.Contains("loan total payments count must be positive") ||
                normalized.Contains("loan final payment must be positive") ||
                normalized.Contains("loan remaining payments count cannot exceed total payments count"))
            {
                return _isRussian
                    ? "Заполните все обязательные поля кредита корректными значениями."
                    : "Fill in the required loan fields with valid values.";
            }

            if (normalized.Contains("loan payment exceeds outstanding debt"))
            {
                return _isRussian
                    ? "Сумма платежа превышает текущий остаток долга по кредиту."
                    : "The payment amount exceeds the outstanding loan debt.";
            }

            if (normalized.Contains("finance_transactions_transaction_kind_check"))
            {
                return _isRussian
                    ? "Сервер ещё не принял новый тип операции для платежа по кредиту. Я уже исправил схему, повторите попытку."
                    : "The server schema did not accept the new loan payment transaction type. The schema has been fixed; try again.";
            }

            if (normalized.Contains("loan debt after payment must be zero or positive") ||
                normalized.Contains("loan debt after payment cannot exceed current debt"))
            {
                return _isRussian
                    ? "Проверьте остаток долга после платежа. Он должен быть фактическим значением из банка."
                    : "Check the outstanding debt after payment. It must match the actual bank value.";
            }

            if (normalized.Contains("loan payment source must be a cash or debit account"))
            {
                return _isRussian
                    ? "Платёж по кредиту можно списывать только с наличных или дебетового счёта."
                    : "Loan payments can only be made from cash or a debit account.";
            }

            if (normalized.Contains("loan account not found"))
            {
                return _isRussian
                    ? "Кредитный счёт не найден. Обновите раздел финансов и попробуйте снова."
                    : "The loan account was not found. Refresh Finance and try again.";
            }

            if (normalized.Contains("primary card bank is required"))
            {
                return _isRussian
                    ? "Выберите банк основной карты или очистите данные карты."
                    : "Choose the primary card bank or clear the card details.";
            }

            if (normalized.Contains("last four digits are required"))
            {
                return _isRussian
                    ? "Укажите последние 4 цифры карты."
                    : "Enter the last 4 digits of the card.";
            }

            if (normalized.Contains("unsupported currency"))
            {
                return _isRussian
                    ? "Эта валюта сейчас не поддерживается в финансах."
                    : "This currency is not supported in Finance.";
            }

            if (normalized.Contains("account not found") || normalized.Contains("source account not found"))
            {
                return _isRussian
                    ? "Счёт не найден. Обновите раздел финансов и попробуйте снова."
                    : "The account was not found. Refresh Finance and try again.";
            }

            if (normalized.Contains("destination account not found"))
            {
                return _isRussian
                    ? "Счёт назначения не найден. Обновите раздел финансов и попробуйте снова."
                    : "The destination account was not found. Refresh Finance and try again.";
            }

            if (normalized.Contains("client request id is required"))
            {
                return _isRussian
                    ? "Не удалось подготовить операцию к сохранению. Повторите попытку."
                    : "The operation could not be prepared for saving. Try again.";
            }

            if (normalized.Contains("overview cards list contains unsupported items"))
            {
                return _isRussian
                    ? "Сохранить настройки обзора не удалось. Обновите раздел финансов и попробуйте снова."
                    : "Overview settings could not be saved. Refresh Finance and try again.";
            }

            return fallback;
        }

        private async Task<string> EnsureLocalImportPathAsync(StorageFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.Path))
            {
                return file.Path;
            }

            var tempFolder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetTempPath());
            var copied = await file.CopyAsync(tempFolder, file.Name, NameCollisionOption.GenerateUniqueName);
            return copied.Path;
        }

        private string GetFileContentType(string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".eml" => "message/rfc822",
                _ => "application/octet-stream"
            };
        }

        private TransactionDraftState CreateManualTransactionDraft(string sourceType = "manual")
        {
            var account = _financeOverview?.Accounts.FirstOrDefault(item => item.IsPrimary)
                ?? _financeOverview?.Accounts.FirstOrDefault();

            return new TransactionDraftState
            {
                SourceType = sourceType,
                DocumentKind = sourceType == "manual" ? "manual" : "image",
                AccountId = account?.Id.ToString() ?? string.Empty,
                Currency = account?.Currency ?? _financeOverview?.DefaultCurrency ?? "RUB",
                HappenedAt = DateTimeOffset.Now.ToString("O")
            };
        }

        private TransactionDraftState CreateDraftFromImport(FinanceImportDraft draft)
        {
            var account = _financeOverview?.Accounts.FirstOrDefault(item => item.IsPrimary)
                ?? _financeOverview?.Accounts.FirstOrDefault();

            var items = draft.Items.Count > 0
                ? draft.Items
                : new List<FinanceImportDraftItem>
                {
                    new()
                    {
                        Title = draft.Title,
                        AmountMinor = draft.AmountMinor
                    }
                };

            return new TransactionDraftState
            {
                SourceType = draft.SourceType,
                DocumentKind = draft.DocumentKind,
                Direction = draft.Direction,
                Title = draft.Title,
                MerchantName = draft.MerchantName ?? string.Empty,
                Note = draft.Note ?? string.Empty,
                AccountId = account?.Id.ToString() ?? string.Empty,
                Currency = draft.Currency,
                HappenedAt = draft.HappenedAt ?? DateTimeOffset.Now.ToString("O"),
                TransferAmount = draft.Direction == "transfer" ? FormatAmountInput(draft.AmountMinor) : string.Empty,
                Items = items.Select(item => new TransactionDraftItemState
                {
                    Title = item.Title,
                    Amount = FormatAmountInput(item.AmountMinor),
                    CategoryId = item.SuggestedCategoryId?.ToString()
                }).ToList()
            };
        }

        private List<(string Code, string Label)> BuildFinanceCategoryOptions(string direction)
        {
            var byId = _financeCategories.ToDictionary(item => item.Id, item => item);
            var options = _financeCategories
                .Where(item => string.Equals(item.Direction, direction, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => GetCategoryDepth(item, byId))
                .ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.Name)
                .Select(item => (item.Id.ToString(), BuildCategoryPath(item, byId)))
                .ToList();

            options.Insert(0, (string.Empty, _isRussian ? "Без категории" : "Uncategorized"));
            return options;
        }

        private string GetFinanceCategoryLabel(string direction, string? categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId) || !Guid.TryParse(categoryId, out var categoryGuid))
            {
                return _isRussian ? "Без категории" : "Uncategorized";
            }

            var byId = _financeCategories.ToDictionary(item => item.Id, item => item);
            return byId.TryGetValue(categoryGuid, out var category) &&
                   string.Equals(category.Direction, direction, StringComparison.OrdinalIgnoreCase)
                ? BuildCategoryPath(category, byId)
                : (_isRussian ? "Без категории" : "Uncategorized");
        }

        private async Task<string?> ShowFinanceCategoryPickerAsync(string direction, string? currentCategoryId)
        {
            var categories = _financeCategories
                .Where(item => string.Equals(item.Direction, direction, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Name)
                .ToList();
            var byId = categories.ToDictionary(item => item.Id, item => item);
            var childLookup = categories
                .GroupBy(item => item.ParentId?.ToString() ?? string.Empty)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(item => item.DisplayOrder)
                        .ThenBy(item => item.Name)
                        .ToList());

            Guid? initialCategoryGuid = Guid.TryParse(currentCategoryId, out var parsedCurrentCategoryId)
                ? parsedCurrentCategoryId
                : null;
            string? selectedCategoryId = currentCategoryId;
            Guid? currentParentId = initialCategoryGuid.HasValue && byId.TryGetValue(initialCategoryGuid.Value, out var initialCategory)
                ? initialCategory.ParentId
                : null;
            var completion = new TaskCompletionSource<string?>();
            var committed = false;

            List<Guid> BuildCategoryPathIds(Guid? categoryId)
            {
                var path = new List<Guid>();
                if (!categoryId.HasValue || !byId.TryGetValue(categoryId.Value, out var category))
                {
                    return path;
                }

                var current = category;
                while (current != null)
                {
                    path.Add(current.Id);
                    current = current.ParentId.HasValue && byId.TryGetValue(current.ParentId.Value, out var parent)
                        ? parent
                        : null;
                }

                path.Reverse();
                return path;
            }

            var popup = new Microsoft.UI.Xaml.Controls.Primitives.Popup
            {
                XamlRoot = Content.XamlRoot,
                IsLightDismissEnabled = true
            };

            var overlay = new Grid
            {
                Width = Content.XamlRoot.Size.Width,
                Height = Content.XamlRoot.Size.Height,
                Background = new SolidColorBrush(Color.FromArgb(120, 10, 10, 12)),
                Opacity = 0
            };

            var card = new Border
            {
                Width = Math.Min(560, Math.Max(420, Content.XamlRoot.Size.Width - 120)),
                MaxHeight = Math.Max(420, Content.XamlRoot.Size.Height - 120),
                CornerRadius = new CornerRadius(24),
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PanelStrongBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                RenderTransform = new TranslateTransform { Y = 18 }
            };

            var titleText = new TextBlock
            {
                Text = _isRussian ? "Выбор категории" : "Choose category",
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Display Semibld"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            };
            var helperText = CreateMutedText(_isRussian
                ? "Сначала выберите раздел верхнего уровня, затем спускайтесь глубже только при необходимости."
                : "Choose a top-level section first, then go deeper only if you need to.");
            var searchBox = new TextBox
            {
                PlaceholderText = _isRussian ? "Поиск по категориям" : "Search categories"
            };
            var breadcrumbPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6
            };
            var breadcrumbScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = breadcrumbPanel
            };
            var currentSelectionTitle = new TextBlock
            {
                Text = _isRussian ? "Текущий выбор" : "Current selection",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            };
            var currentSelectionPath = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            var currentSelectionCard = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(14, 12, 14, 12),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        currentSelectionTitle,
                        currentSelectionPath
                    }
                }
            };
            var breadcrumbLabel = new TextBlock
            {
                Text = _isRussian ? "Навигация" : "Navigation",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            };
            var sectionTitle = new TextBlock
            {
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
            };
            var optionsPanel = new StackPanel { Spacing = 10 };
            var emptyState = CreateMutedText(string.Empty);
            emptyState.TextAlignment = TextAlignment.Center;
            emptyState.Visibility = Visibility.Collapsed;
            var backButton = CreateFinanceMiniButton(_isRussian ? "Назад" : "Back");
            var clearButton = CreateFinanceMiniButton(_isRussian ? "Без категории" : "Uncategorized");
            var closeButton = CreateFinanceMiniButton(_isRussian ? "Закрыть" : "Close");
            var selectCurrentButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 12, 14, 12),
                Visibility = Visibility.Collapsed
            };

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerRow.Children.Add(titleText);
            Grid.SetColumn(closeButton, 1);
            headerRow.Children.Add(closeButton);

            var actionsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    backButton,
                    clearButton
                }
            };

            var contentStack = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    headerRow,
                    helperText,
                    searchBox,
                    breadcrumbLabel,
                    breadcrumbScroll,
                    currentSelectionCard,
                    actionsRow,
                    selectCurrentButton,
                    sectionTitle,
                    new ScrollViewer
                    {
                        MaxHeight = 340,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Content = optionsPanel
                    },
                    emptyState
                }
            };

            card.Child = contentStack;
            overlay.Children.Add(card);
            popup.Child = overlay;

            TextBlock CreateHighlightedTextBlock(
                string text,
                string query,
                double fontSize,
                Windows.UI.Text.FontWeight fontWeight,
                Brush foreground,
                Brush highlightForeground)
            {
                var block = new TextBlock
                {
                    FontSize = fontSize,
                    FontWeight = fontWeight,
                    Foreground = foreground,
                    TextWrapping = TextWrapping.Wrap
                };

                if (string.IsNullOrWhiteSpace(query))
                {
                    block.Text = text;
                    return block;
                }

                var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    block.Text = text;
                    return block;
                }

                if (index > 0)
                {
                    block.Inlines.Add(new Run { Text = text[..index] });
                }

                block.Inlines.Add(new Run
                {
                    Text = text.Substring(index, query.Length),
                    Foreground = highlightForeground,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold
                });

                var tailIndex = index + query.Length;
                if (tailIndex < text.Length)
                {
                    block.Inlines.Add(new Run { Text = text[tailIndex..] });
                }

                return block;
            }

            Button CreateCategoryOption(FinanceCategory category, bool fromSearch)
            {
                var hasChildren = childLookup.TryGetValue(category.Id.ToString(), out var children) && children.Count > 0;
                var isCurrentSelection = string.Equals(selectedCategoryId, category.Id.ToString(), StringComparison.OrdinalIgnoreCase);
                var currentQuery = searchBox.Text?.Trim() ?? string.Empty;
                var subtitleText = fromSearch
                    ? BuildCategoryPath(category, byId)
                    : hasChildren
                        ? (_isRussian ? "Открыть следующий уровень" : "Open the next level")
                        : (_isRussian ? "Выбрать эту категорию" : "Choose this category");

                var title = CreateHighlightedTextBlock(
                    category.Name,
                    currentQuery,
                    14,
                    Microsoft.UI.Text.FontWeights.SemiBold,
                    (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"],
                    (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]);
                var subtitle = fromSearch
                    ? CreateHighlightedTextBlock(
                        subtitleText,
                        currentQuery,
                        13,
                        Microsoft.UI.Text.FontWeights.Normal,
                        (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                        (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"])
                    : CreateMutedText(subtitleText);
                var indicator = new FontIcon
                {
                    Glyph = fromSearch || !hasChildren ? "\uE73E" : "\uE76C",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 14,
                    Foreground = isCurrentSelection
                        ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                        : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(indicator, 1);

                var normalBackground = isCurrentSelection
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PillBackgroundBrush"]
                    : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"];
                var hoverBackground = isCurrentSelection
                    ? new SolidColorBrush(Color.FromArgb(255, 52, 61, 56))
                    : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"];
                var pressedBackground = new SolidColorBrush(Color.FromArgb(255, 58, 58, 64));
                var normalBorder = isCurrentSelection
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                    : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
                var hoverBorder = isCurrentSelection
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"]
                    : new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));

                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    BorderBrush = normalBorder,
                    BorderThickness = new Thickness(1),
                    Background = normalBackground,
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 12, 14, 12),
                    Content = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                            new ColumnDefinition { Width = GridLength.Auto }
                        },
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 4,
                                Children =
                                {
                                    title,
                                    subtitle
                                }
                            },
                            indicator
                        }
                    }
                };

                button.Resources["ButtonBackground"] = normalBackground;
                button.Resources["ButtonBackgroundPointerOver"] = hoverBackground;
                button.Resources["ButtonBackgroundPressed"] = pressedBackground;
                button.Resources["ButtonBorderBrush"] = normalBorder;
                button.Resources["ButtonBorderBrushPointerOver"] = hoverBorder;
                button.Resources["ButtonBorderBrushPressed"] = hoverBorder;
                button.Resources["ButtonForeground"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"];
                button.Resources["ButtonForegroundPointerOver"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"];
                button.Resources["ButtonForegroundPressed"] = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"];

                button.Click += (_, _) =>
                {
                    if (!fromSearch && hasChildren)
                    {
                        currentParentId = category.Id;
                        RenderPicker();
                        return;
                    }

                    selectedCategoryId = category.Id.ToString();
                    committed = true;
                    popup.IsOpen = false;
                };

                return button;
            }

            void RenderBreadcrumb(Guid? focusCategoryId)
            {
                breadcrumbPanel.Children.Clear();

                var pathIds = BuildCategoryPathIds(focusCategoryId);
                if (pathIds.Count == 0)
                {
                    breadcrumbPanel.Children.Add(CreateFinanceBadge(_isRussian ? "Все категории" : "All categories", true));
                    return;
                }

                for (var i = 0; i < pathIds.Count; i++)
                {
                    if (!byId.TryGetValue(pathIds[i], out var category))
                    {
                        continue;
                    }

                    breadcrumbPanel.Children.Add(CreateFinanceBadge(category.Name, emphasize: i == pathIds.Count - 1));
                    if (i < pathIds.Count - 1)
                    {
                        breadcrumbPanel.Children.Add(new TextBlock
                        {
                            Text = "›",
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                        });
                    }
                }
            }

            void RenderCurrentSelection()
            {
                if (!Guid.TryParse(selectedCategoryId, out var selectedGuid) || !byId.TryGetValue(selectedGuid, out var selectedCategory))
                {
                    currentSelectionCard.Visibility = Visibility.Visible;
                    currentSelectionPath.Text = _isRussian
                        ? "Категория пока не выбрана."
                        : "No category selected yet.";
                    return;
                }

                currentSelectionCard.Visibility = Visibility.Visible;
                currentSelectionPath.Text = BuildCategoryPath(selectedCategory, byId);
            }

            void RenderPicker()
            {
                optionsPanel.Children.Clear();
                emptyState.Visibility = Visibility.Collapsed;

                var query = searchBox.Text?.Trim() ?? string.Empty;
                var browsingCategory = currentParentId.HasValue && byId.TryGetValue(currentParentId.Value, out var currentCategory)
                    ? currentCategory
                    : null;

                RenderBreadcrumb(!string.IsNullOrWhiteSpace(query)
                    ? (Guid.TryParse(selectedCategoryId, out var selectedGuid) ? selectedGuid : initialCategoryGuid)
                    : currentParentId);
                RenderCurrentSelection();

                backButton.Visibility = string.IsNullOrWhiteSpace(query) && currentParentId.HasValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (string.IsNullOrWhiteSpace(query))
                {
                    sectionTitle.Text = browsingCategory == null
                        ? (_isRussian ? "Категории верхнего уровня" : "Top-level categories")
                        : (_isRussian ? $"Внутри «{browsingCategory.Name}»" : $"Inside “{browsingCategory.Name}”");

                    if (browsingCategory != null)
                    {
                        selectCurrentButton.Visibility = Visibility.Visible;
                        selectCurrentButton.Content = new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = _isRussian
                                        ? $"Выбрать «{browsingCategory.Name}»"
                                        : $"Choose “{browsingCategory.Name}”",
                                    FontSize = 14,
                                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                                },
                                CreateMutedText(_isRussian
                                    ? "Остановиться на этом уровне и не уходить глубже."
                                    : "Stop at this level instead of going deeper.")
                            }
                        };
                    }
                    else
                    {
                        selectCurrentButton.Visibility = Visibility.Collapsed;
                    }

                    var levelKey = currentParentId?.ToString() ?? string.Empty;
                    var levelItems = childLookup.GetValueOrDefault(levelKey, new List<FinanceCategory>());
                    foreach (var category in levelItems)
                    {
                        optionsPanel.Children.Add(CreateCategoryOption(category, fromSearch: false));
                    }

                    if (levelItems.Count == 0)
                    {
                        emptyState.Visibility = Visibility.Visible;
                        emptyState.Text = _isRussian
                            ? "На этом уровне пока нет вложенных категорий."
                            : "There are no nested categories at this level.";
                    }

                    return;
                }

                selectCurrentButton.Visibility = Visibility.Collapsed;
                sectionTitle.Text = _isRussian ? "Результаты поиска" : "Search results";

                var results = categories
                    .Where(category =>
                        category.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        BuildCategoryPath(category, byId).Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(40)
                    .ToList();

                foreach (var category in results)
                {
                    optionsPanel.Children.Add(CreateCategoryOption(category, fromSearch: true));
                }

                if (results.Count == 0)
                {
                    emptyState.Visibility = Visibility.Visible;
                    emptyState.Text = _isRussian
                        ? "Ничего не найдено. Попробуйте другой запрос."
                        : "Nothing found. Try a different query.";
                }
            }

            backButton.Click += (_, _) =>
            {
                if (!currentParentId.HasValue || !byId.TryGetValue(currentParentId.Value, out var currentCategory))
                {
                    currentParentId = null;
                    RenderPicker();
                    return;
                }

                currentParentId = currentCategory.ParentId;
                RenderPicker();
            };

            clearButton.Click += (_, _) =>
            {
                selectedCategoryId = string.Empty;
                committed = true;
                popup.IsOpen = false;
            };

            closeButton.Click += (_, _) => popup.IsOpen = false;

            selectCurrentButton.Click += (_, _) =>
            {
                selectedCategoryId = currentParentId?.ToString() ?? string.Empty;
                committed = true;
                popup.IsOpen = false;
            };

            searchBox.TextChanged += (_, _) => RenderPicker();

            popup.Closed += (_, _) =>
            {
                completion.TrySetResult(committed ? selectedCategoryId : null);
            };

            popup.Opened += (_, _) =>
            {
                var storyboard = new Storyboard();
                var overlayAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(160))
                };
                Storyboard.SetTarget(overlayAnimation, overlay);
                Storyboard.SetTargetProperty(overlayAnimation, nameof(UIElement.Opacity));

                var cardOpacityAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(190))
                };
                Storyboard.SetTarget(cardOpacityAnimation, card);
                Storyboard.SetTargetProperty(cardOpacityAnimation, nameof(UIElement.Opacity));

                var cardTranslateAnimation = new DoubleAnimation
                {
                    From = 18,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(cardTranslateAnimation, (TranslateTransform)card.RenderTransform);
                Storyboard.SetTargetProperty(cardTranslateAnimation, nameof(TranslateTransform.Y));

                storyboard.Children.Add(overlayAnimation);
                storyboard.Children.Add(cardOpacityAnimation);
                storyboard.Children.Add(cardTranslateAnimation);
                storyboard.Begin();
            };

            RenderPicker();
            popup.IsOpen = true;
            return await completion.Task;
        }

        private int GetCategoryDepth(FinanceCategory category, IReadOnlyDictionary<Guid, FinanceCategory> byId)
        {
            var depth = 0;
            var current = category.ParentId.HasValue && byId.TryGetValue(category.ParentId.Value, out var parent)
                ? parent
                : null;
            while (current != null)
            {
                depth++;
                current = current.ParentId.HasValue && byId.TryGetValue(current.ParentId.Value, out var next)
                    ? next
                    : null;
            }

            return depth;
        }

        private string BuildCategoryPath(FinanceCategory category, IReadOnlyDictionary<Guid, FinanceCategory> byId)
        {
            var parts = new List<string> { category.Name };
            var current = category.ParentId.HasValue && byId.TryGetValue(category.ParentId.Value, out var parent)
                ? parent
                : null;
            while (current != null)
            {
                parts.Add(current.Name);
                current = current.ParentId.HasValue && byId.TryGetValue(current.ParentId.Value, out var next)
                    ? next
                    : null;
            }

            parts.Reverse();
            return string.Join(" › ", parts);
        }

        private string GetTransactionSourceLabel(string sourceType) => sourceType.ToLowerInvariant() switch
        {
            "photo" => _isRussian ? "Фото" : "Photo",
            "file" => _isRussian ? "Файл" : "File",
            _ => _isRussian ? "Вручную" : "Manual"
        };

        private async Task<bool> ShowFinanceTransactionEditorAsync(
            List<TransactionDraftState> drafts,
            List<string> warnings)
        {
            if (drafts.Count == 0)
            {
                return false;
            }

            var saved = false;
            var busy = false;
            var selectedIndex = 0;
            var loadingDraft = false;

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = string.Empty
            };

            var root = new StackPanel
            {
                MaxWidth = 620,
                Spacing = 18,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 12, 0)
            };
            var basicSection = new StackPanel { Spacing = 16 };
            var itemsSection = new StackPanel { Spacing = 14 };
            var contentScroll = new ScrollViewer
            {
                Content = root,
                MaxHeight = 620,
                Padding = new Thickness(0, 0, 4, 0),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var itemsSectionCard = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(20),
                Child = itemsSection
            };
            var basicSectionCard = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(20),
                Child = basicSection
            };

            var headerStack = new StackPanel { Spacing = 6 };
            headerStack.Children.Add(new TextBlock
            {
                Text = _isRussian ? "Добавить транзакцию" : "Add transaction",
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Display Semibld"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });

            var stateText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                FontSize = 13
            };
            headerStack.Children.Add(stateText);
            root.Children.Add(headerStack);

            if (warnings.Count > 0)
            {
                var warningsPanel = new StackPanel { Spacing = 6 };
                warningsPanel.Children.Add(new TextBlock
                {
                    Text = _isRussian ? "Требует проверки" : "Needs review",
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                });
                foreach (var warning in warnings)
                {
                    warningsPanel.Children.Add(new TextBlock
                    {
                        Text = warning,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                        FontSize = 12
                    });
                }

                root.Children.Add(new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(14, 12, 14, 12),
                    Child = warningsPanel
                });
            }

            var draftSelector = new ComboBox
            {
                Header = _isRussian ? "Черновик" : "Draft",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = drafts.Count > 1 ? Visibility.Visible : Visibility.Collapsed
            };

            for (var index = 0; index < drafts.Count; index++)
            {
                draftSelector.Items.Add(_isRussian ? $"Черновик {index + 1}" : $"Draft {index + 1}");
            }

            var directionCombo = new ComboBox
            {
                Header = _isRussian ? "Тип операции" : "Transaction type",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            directionCombo.Items.Add(_isRussian ? "Расход" : "Expense");
            directionCombo.Items.Add(_isRussian ? "Доход" : "Income");
            directionCombo.Items.Add(_isRussian ? "Перевод" : "Transfer");

            var accountCombo = new ComboBox
            {
                Header = _isRussian ? "Счет списания" : "Source account",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var destinationCombo = new ComboBox
            {
                Header = _isRussian ? "Счет зачисления" : "Destination account",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var titleBox = new TextBox
            {
                Header = _isRussian ? "Описание" : "Description",
                PlaceholderText = _isRussian ? "Например, продукты или зарплата" : "For example, groceries or salary"
            };
            var merchantBox = new TextBox
            {
                Header = _isRussian ? "Место или источник" : "Merchant or source",
                PlaceholderText = _isRussian ? "Например, ВкусВилл или T-Банк" : "For example, local store or bank"
            };
            var transferAmountBox = new TextBox
            {
                Header = _isRussian ? "Сумма перевода" : "Transfer amount",
                PlaceholderText = _isRussian ? "Например, 1500" : "For example, 1500"
            };
            var datePicker = new DatePicker
            {
                Header = _isRussian ? "Дата" : "Date",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var timePicker = new TimePicker
            {
                Header = _isRussian ? "Время" : "Time",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinuteIncrement = 5,
                ClockIdentifier = "24HourClock"
            };
            var itemsPanel = new StackPanel { Spacing = 12 };

            basicSection.Children.Add(new TextBlock
            {
                Text = _isRussian ? "Основное" : "Basics",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Text Semibold"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });
            basicSection.Children.Add(CreateMutedText(_isRussian
                ? "Выберите счет, задайте тип операции и укажите удобное локальное время."
                : "Choose the account, transaction type, and a friendly local date and time."));

            itemsSection.Children.Add(new TextBlock
            {
                Text = _isRussian ? "Позиции" : "Items",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Text Semibold"),
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
            });
            itemsSection.Children.Add(CreateMutedText(_isRussian
                ? "Разбейте транзакцию на категории, если нужно. Для перевода достаточно одной суммы."
                : "Split the transaction across categories if needed. Transfers only need one amount."));

            void ApplyTransactionEditorMode(TransactionDraftState draft)
            {
                var isTransfer = string.Equals(draft.Direction, "transfer", StringComparison.OrdinalIgnoreCase);
                destinationCombo.Visibility = isTransfer ? Visibility.Visible : Visibility.Collapsed;
                transferAmountBox.Visibility = isTransfer ? Visibility.Visible : Visibility.Collapsed;
                merchantBox.Visibility = isTransfer ? Visibility.Collapsed : Visibility.Visible;
                itemsSectionCard.Visibility = isTransfer ? Visibility.Collapsed : Visibility.Visible;
            }

            void PopulateAccountCombos(TransactionDraftState draft)
            {
                accountCombo.Items.Clear();

                foreach (var account in (_financeOverview?.Accounts ?? Enumerable.Empty<FinanceAccount>())
                    .Where(account => !IsLoanAccount(account)))
                {
                    accountCombo.Items.Add(new ComboBoxItem
                    {
                        Tag = account.Id.ToString(),
                        Content = GetFinanceAccountDisplayName(account)
                    });
                }
                PopulateDestinationCombo(draft);
            }

            void PopulateDestinationCombo(TransactionDraftState draft)
            {
                destinationCombo.Items.Clear();

                var sourceAccounts = _financeOverview?.Accounts
                    .Where(account => !IsLoanAccount(account))
                    .Where(account => !string.Equals(account.Id.ToString(), draft.AccountId, StringComparison.OrdinalIgnoreCase))
                    .ToList() ?? new List<FinanceAccount>();

                foreach (var account in sourceAccounts)
                {
                    destinationCombo.Items.Add(new ComboBoxItem
                    {
                        Tag = account.Id.ToString(),
                        Content = GetFinanceAccountDisplayName(account)
                    });
                }
            }

            void ReselectAccountCombos(TransactionDraftState draft)
            {
                accountCombo.SelectedIndex = -1;
                for (var i = 0; i < accountCombo.Items.Count; i++)
                {
                    if (accountCombo.Items[i] is ComboBoxItem item &&
                        item.Tag is string sourceId &&
                        string.Equals(sourceId, draft.AccountId, StringComparison.OrdinalIgnoreCase))
                    {
                        accountCombo.SelectedIndex = i;
                        break;
                    }
                }

                destinationCombo.SelectedIndex = -1;
                for (var i = 0; i < destinationCombo.Items.Count; i++)
                {
                    if (destinationCombo.Items[i] is ComboBoxItem item &&
                        item.Tag is string destinationId &&
                        string.Equals(destinationId, draft.DestinationAccountId, StringComparison.OrdinalIgnoreCase))
                    {
                        destinationCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            void ReselectDestinationCombo(TransactionDraftState draft)
            {
                destinationCombo.SelectedIndex = -1;
                for (var i = 0; i < destinationCombo.Items.Count; i++)
                {
                    if (destinationCombo.Items[i] is ComboBoxItem item &&
                        item.Tag is string destinationId &&
                        string.Equals(destinationId, draft.DestinationAccountId, StringComparison.OrdinalIgnoreCase))
                    {
                        destinationCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            void RefreshDraftAccountState(TransactionDraftState draft)
            {
                if (_financeOverview?.Accounts.FirstOrDefault(account =>
                        string.Equals(account.Id.ToString(), draft.AccountId, StringComparison.OrdinalIgnoreCase)) is { } selectedAccount)
                {
                    draft.Currency = selectedAccount.Currency;
                }

                if (string.Equals(draft.AccountId, draft.DestinationAccountId, StringComparison.OrdinalIgnoreCase))
                {
                    draft.DestinationAccountId = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(draft.DestinationAccountId) &&
                    !_financeOverview!.Accounts.Any(account =>
                        !string.Equals(account.Id.ToString(), draft.AccountId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(account.Id.ToString(), draft.DestinationAccountId, StringComparison.OrdinalIgnoreCase)))
                {
                    draft.DestinationAccountId = string.Empty;
                }

                loadingDraft = true;
                try
                {
                    PopulateDestinationCombo(draft);
                    ReselectDestinationCombo(draft);
                }
                finally
                {
                    loadingDraft = false;
                }
            }

            void SyncCurrentDraft(TransactionDraftState draft)
            {
                draft.Direction = directionCombo.SelectedIndex switch
                {
                    1 => "income",
                    2 => "transfer",
                    _ => "expense"
                };

                if (accountCombo.SelectedItem is ComboBoxItem sourceItem && sourceItem.Tag is string sourceId)
                {
                    draft.AccountId = sourceId;
                }

                if (destinationCombo.SelectedItem is ComboBoxItem destinationItem && destinationItem.Tag is string destinationId)
                {
                    draft.DestinationAccountId = destinationId;
                }

                draft.Title = titleBox.Text.Trim();
                draft.MerchantName = merchantBox.Text.Trim();
                draft.TransferAmount = transferAmountBox.Text.Trim();
                var selectedDate = datePicker.Date == default ? DateTimeOffset.Now : datePicker.Date;
                var selectedTime = timePicker.Time;
                var localMoment = new DateTime(
                    selectedDate.Year,
                    selectedDate.Month,
                    selectedDate.Day,
                    selectedTime.Hours,
                    selectedTime.Minutes,
                    0,
                    DateTimeKind.Local);
                draft.HappenedAt = new DateTimeOffset(localMoment).ToString("O");
            }

            void RenderItems(TransactionDraftState draft)
            {
                itemsPanel.Children.Clear();
                if (draft.Direction == "transfer" && draft.Items.Count > 1)
                {
                    draft.Items = draft.Items.Take(1).ToList();
                }

                for (var itemIndex = 0; itemIndex < draft.Items.Count; itemIndex++)
                {
                    var item = draft.Items[itemIndex];
                    var card = new Border
                    {
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                        BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(20),
                        Padding = new Thickness(16, 14, 16, 14)
                    };

                    var itemStack = new StackPanel { Spacing = 12 };
                    var itemHeader = new Grid();
                    itemHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    itemHeader.Children.Add(new TextBlock
                    {
                        Text = _isRussian ? $"Позиция {itemIndex + 1}" : $"Item {itemIndex + 1}",
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                    });

                    if (draft.Direction != "transfer" && draft.Items.Count > 1)
                    {
                        var removeButton = CreateFinanceMiniButton(_isRussian ? "Удалить" : "Remove");
                        removeButton.Click += async (_, _) =>
                        {
                            draft.Items = draft.Items.Where(current => current.Id != item.Id).ToList();
                            if (draft.Items.Count == 0)
                            {
                                draft.Items.Add(new TransactionDraftItemState());
                            }

                            RenderItems(draft);
                        };
                        Grid.SetColumn(removeButton, 1);
                        itemHeader.Children.Add(removeButton);
                    }

                    itemStack.Children.Add(itemHeader);

                    var titleInput = new TextBox
                    {
                        Header = _isRussian ? "Название" : "Title",
                        Text = item.Title,
                        PlaceholderText = _isRussian ? "Например, продукты" : "For example, groceries"
                    };
                    titleInput.TextChanged += (_, _) =>
                    {
                        if (loadingDraft) return;
                        item.Title = titleInput.Text;
                    };

                    var amountInput = new TextBox
                    {
                        Header = _isRussian ? "Сумма" : "Amount",
                        Text = item.Amount,
                        PlaceholderText = _isRussian ? "Например, 1250" : "For example, 1250"
                    };
                    amountInput.TextChanged += (_, _) =>
                    {
                        if (loadingDraft) return;
                        item.Amount = amountInput.Text;
                    };

                    var inputsGrid = new Grid { ColumnSpacing = 12 };
                    inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                    inputsGrid.Children.Add(titleInput);
                    Grid.SetColumn(amountInput, 1);
                    inputsGrid.Children.Add(amountInput);
                    itemStack.Children.Add(inputsGrid);

                    if (draft.Direction != "transfer")
                    {
                        var categoryHeader = new TextBlock
                        {
                            Text = _isRussian ? "Категория" : "Category",
                            FontSize = 12,
                            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"]
                        };
                        itemStack.Children.Add(categoryHeader);

                        var categoryTitle = new TextBlock
                        {
                            Text = GetFinanceCategoryLabel(draft.Direction, item.CategoryId),
                            FontSize = 14,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                        };
                        var categoryHint = CreateMutedText(string.IsNullOrWhiteSpace(item.CategoryId)
                            ? (_isRussian
                                ? "Выберите раздел, затем при необходимости уточните вложенную категорию."
                                : "Choose a section, then go deeper if you need a nested category.")
                            : (_isRussian
                                ? "Нажмите, чтобы изменить путь категории."
                                : "Click to change the category path."));
                        var categoryChevronIcon = new FontIcon
                        {
                            Glyph = "\uE70D",
                            FontFamily = new FontFamily("Segoe Fluent Icons"),
                            FontSize = 14,
                            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"],
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(categoryChevronIcon, 1);

                        var categoryButton = CreateFinanceSurfaceButton(
                            new Grid
                            {
                                ColumnDefinitions =
                                {
                                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                                    new ColumnDefinition { Width = GridLength.Auto }
                                },
                                Children =
                                {
                                    new StackPanel
                                    {
                                        Spacing = 4,
                                        Children =
                                        {
                                            categoryTitle,
                                            categoryHint
                                        }
                                    },
                                    categoryChevronIcon
                                }
                            });
                        categoryButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                        categoryButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;

                        categoryButton.Click += async (_, _) =>
                        {
                            if (loadingDraft)
                            {
                                return;
                            }

                            var selectedCategoryId = await ShowFinanceCategoryPickerAsync(draft.Direction, item.CategoryId);
                            if (selectedCategoryId != null)
                            {
                                item.CategoryId = selectedCategoryId;
                                categoryTitle.Text = GetFinanceCategoryLabel(draft.Direction, item.CategoryId);
                                categoryHint.Text = string.IsNullOrWhiteSpace(item.CategoryId)
                                    ? (_isRussian
                                        ? "Выберите раздел, затем при необходимости уточните вложенную категорию."
                                        : "Choose a section, then go deeper if you need a nested category.")
                                    : (_isRussian
                                        ? "Нажмите, чтобы изменить путь категории."
                                        : "Click to change the category path.");
                            }
                        };

                        itemStack.Children.Add(categoryButton);
                    }

                    card.Child = itemStack;
                    itemsPanel.Children.Add(card);
                }

                if (draft.Direction != "transfer")
                {
                    var addItemContent = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new FontIcon
                            {
                                Glyph = "\uE710",
                                FontSize = 12,
                                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                            },
                            new TextBlock
                            {
                                Text = _isRussian ? "Добавить позицию" : "Add item",
                                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                            }
                        }
                    };
                    var addItemButton = CreateFinanceSurfaceButton(addItemContent, centerContent: true);

                    addItemButton.Click += async (_, _) =>
                    {
                        draft.Items.Add(new TransactionDraftItemState());
                        RenderItems(draft);
                    };

                    itemsPanel.Children.Add(addItemButton);
                }

            }

            void LoadDraft(int index)
            {
                if (index < 0 || index >= drafts.Count)
                {
                    return;
                }

                selectedIndex = index;
                loadingDraft = true;

                var draft = drafts[index];
                draftSelector.SelectedIndex = drafts.Count > 1 ? index : -1;
                directionCombo.SelectedIndex = draft.Direction switch
                {
                    "income" => 1,
                    "transfer" => 2,
                    _ => 0
                };
                PopulateAccountCombos(draft);
                ReselectAccountCombos(draft);

                titleBox.Text = draft.Title;
                merchantBox.Text = draft.MerchantName;
                transferAmountBox.Text = draft.TransferAmount;
                var draftMoment = DateTimeOffset.TryParse(draft.HappenedAt, out var parsedDraftMoment)
                    ? parsedDraftMoment.ToLocalTime()
                    : DateTimeOffset.Now;
                datePicker.Date = draftMoment;
                timePicker.Time = draftMoment.TimeOfDay;
                stateText.Text = _isRussian
                    ? $"Источник: {GetTransactionSourceLabel(draft.SourceType)}"
                    : $"Source: {GetTransactionSourceLabel(draft.SourceType)}";
                ApplyTransactionEditorMode(draft);
                RenderItems(draft);
                loadingDraft = false;
            }

            root.Children.Add(draftSelector);

            var primaryGrid = new Grid { ColumnSpacing = 12 };
            primaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            primaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            primaryGrid.Children.Add(directionCombo);
            Grid.SetColumn(accountCombo, 1);
            primaryGrid.Children.Add(accountCombo);

            var dateTimeStack = new StackPanel { Spacing = 12 };
            dateTimeStack.Children.Add(datePicker);
            dateTimeStack.Children.Add(timePicker);

            basicSection.Children.Add(primaryGrid);
            basicSection.Children.Add(destinationCombo);
            basicSection.Children.Add(transferAmountBox);
            basicSection.Children.Add(dateTimeStack);
            basicSection.Children.Add(titleBox);
            basicSection.Children.Add(merchantBox);
            itemsSection.Children.Add(itemsPanel);
            root.Children.Add(basicSectionCard);
            root.Children.Add(itemsSectionCard);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            var cancelButton = new Button
            {
                Content = _isRussian ? "Отмена" : "Cancel",
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 10, 18, 10)
            };

            cancelButton.Click += (_, _) => dialog.Hide();

            var saveButton = new Button
            {
                Content = drafts.Count > 1
                    ? (_isRussian ? "Сохранить все" : "Save all")
                    : (_isRussian ? "Сохранить" : "Save"),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 10, 18, 10)
            };

            saveButton.Click += async (_, _) =>
            {
                if (busy)
                {
                    return;
                }

                SyncCurrentDraft(drafts[selectedIndex]);
                if (!drafts.All(ValidateDraft))
                {
                    stateText.Text = _isRussian
                        ? "Проверьте обязательные поля и суммы."
                        : "Check the required fields and amounts.";
                    return;
                }

                busy = true;
                saveButton.IsEnabled = false;
                cancelButton.IsEnabled = false;
                try
                {
                    var financeClient = _financeClient!;
                    var accessToken = await GetFreshFinanceAccessTokenAsync();
                    var createRequests = new List<FinanceCreateTransactionRequest>();

                    foreach (var draft in drafts)
                    {
                        SyncCurrentDraft(draft);
                        createRequests.Add(ToCreatePayload(draft));
                    }

                    if (!TryValidateFinanceDraftBalances(createRequests, out var balanceError))
                    {
                        stateText.Text = balanceError;
                        return;
                    }

                    _financeOverview = await financeClient.CreateTransactionsAsync(accessToken, createRequests);
                    _financeAnalytics = null;
                    saved = true;
                    dialog.Hide();
                }
                catch (Exception ex)
                {
                    stateText.Text = LocalizeFinanceRequestError(
                        ex.Message,
                        _isRussian ? "Не удалось сохранить транзакции." : "Failed to save transactions.");
                }
                finally
                {
                    busy = false;
                    saveButton.IsEnabled = true;
                    cancelButton.IsEnabled = true;
                }
            };

            actions.Children.Add(cancelButton);
            actions.Children.Add(saveButton);

            var footer = new Border
            {
                Margin = new Thickness(0, 16, 0, 0),
                Padding = new Thickness(0, 16, 0, 0),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = actions
            };

            draftSelector.SelectionChanged += (_, _) =>
            {
                if (loadingDraft || draftSelector.SelectedIndex < 0)
                {
                    return;
                }

                LoadDraft(draftSelector.SelectedIndex);
            };

            directionCombo.SelectionChanged += (_, _) =>
            {
                if (loadingDraft)
                {
                    return;
                }

                try
                {
                    var draft = drafts[selectedIndex];
                    var wasTransfer = string.Equals(draft.Direction, "transfer", StringComparison.OrdinalIgnoreCase);
                    SyncCurrentDraft(draft);
                    var nextDirection = directionCombo.SelectedIndex switch
                    {
                        1 => "income",
                        2 => "transfer",
                        _ => "expense"
                    };
                    var willBeTransfer = string.Equals(nextDirection, "transfer", StringComparison.OrdinalIgnoreCase);

                    if (!wasTransfer && willBeTransfer && string.IsNullOrWhiteSpace(draft.TransferAmount))
                    {
                        draft.TransferAmount = draft.Items.FirstOrDefault()?.Amount ?? string.Empty;
                    }
                    if (wasTransfer && !willBeTransfer && draft.Items.Count > 0 && string.IsNullOrWhiteSpace(draft.Items[0].Amount))
                    {
                        draft.Items[0].Amount = draft.TransferAmount;
                    }

                    if (directionCombo.SelectedIndex == 2 && draft.Items.Count == 0)
                    {
                        draft.Items.Add(new TransactionDraftItemState());
                    }
                    if (directionCombo.SelectedIndex == 2)
                    {
                        draft.Items = draft.Items.Take(1).ToList();
                    }

                    draft.Direction = nextDirection;
                    LoadDraft(selectedIndex);
                }
                catch (Exception ex)
                {
                    stateText.Text = ex.Message;
                }
            };

            accountCombo.SelectionChanged += (_, _) =>
            {
                if (loadingDraft)
                {
                    return;
                }

                try
                {
                    var draft = drafts[selectedIndex];
                    SyncCurrentDraft(draft);
                    RefreshDraftAccountState(draft);
                    ApplyTransactionEditorMode(draft);
                    RenderItems(draft);
                }
                catch (Exception ex)
                {
                    stateText.Text = ex.Message;
                }
            };

            destinationCombo.SelectionChanged += (_, _) =>
            {
                if (loadingDraft)
                {
                    return;
                }

                var draft = drafts[selectedIndex];
                SyncCurrentDraft(draft);
            };

            titleBox.TextChanged += (_, _) =>
            {
                if (!loadingDraft)
                {
                    drafts[selectedIndex].Title = titleBox.Text;
                }
            };

            merchantBox.TextChanged += (_, _) =>
            {
                if (!loadingDraft)
                {
                    drafts[selectedIndex].MerchantName = merchantBox.Text;
                }
            };

            transferAmountBox.TextChanged += (_, _) =>
            {
                if (!loadingDraft)
                {
                    drafts[selectedIndex].TransferAmount = transferAmountBox.Text;
                }
            };

            datePicker.DateChanged += (_, _) =>
            {
                if (!loadingDraft)
                {
                    SyncCurrentDraft(drafts[selectedIndex]);
                }
            };

            timePicker.TimeChanged += (_, _) =>
            {
                if (!loadingDraft)
                {
                    SyncCurrentDraft(drafts[selectedIndex]);
                }
            };

            var dialogLayout = new Grid();
            dialogLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            dialogLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogLayout.Children.Add(contentScroll);
            Grid.SetRow(footer, 1);
            dialogLayout.Children.Add(footer);

            dialog.Content = dialogLayout;
            LoadDraft(0);
            await dialog.ShowAsync();
            return saved;

            bool ValidateDraft(TransactionDraftState draft)
            {
                if (string.IsNullOrWhiteSpace(draft.AccountId))
                {
                    return false;
                }

                if (draft.Direction == "transfer")
                {
                    return !string.IsNullOrWhiteSpace(draft.DestinationAccountId) &&
                        ParseMoneyInputToMinor(draft.TransferAmount) is > 0;
                }

                return draft.Items.Count > 0 && draft.Items.All(item =>
                    ParseMoneyInputToMinor(item.Amount) is > 0);
            }

            FinanceTransactionItemDraft ToItemDraft(TransactionDraftItemState item)
            {
                return new FinanceTransactionItemDraft
                {
                    Title = string.IsNullOrWhiteSpace(item.Title)
                        ? (_isRussian ? "Позиция" : "Item")
                        : item.Title.Trim(),
                    AmountMinor = ParseMoneyInputToMinor(item.Amount) ?? 0,
                    CategoryId = Guid.TryParse(item.CategoryId, out var categoryId) ? categoryId : null
                };
            }

            FinanceCreateTransactionRequest ToCreatePayload(TransactionDraftState draft)
            {
                var items = draft.Direction == "transfer"
                    ? new List<FinanceTransactionItemDraft>()
                    : draft.Items
                        .Select(ToItemDraft)
                        .Where(item => item.AmountMinor > 0)
                        .ToList();

                var amountMinor = draft.Direction == "transfer"
                    ? ParseMoneyInputToMinor(draft.TransferAmount)
                    : items.Sum(item => item.AmountMinor);

                var singleCategoryId = draft.Direction != "transfer" && items.Count == 1
                    ? items[0].CategoryId
                    : null;

                return new FinanceCreateTransactionRequest
                {
                    ClientRequestId = Guid.Parse(draft.Id),
                    AccountId = Guid.Parse(draft.AccountId),
                    Direction = draft.Direction,
                    Title = string.IsNullOrWhiteSpace(draft.Title) ? null : draft.Title.Trim(),
                    Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim(),
                    AmountMinor = amountMinor,
                    Currency = string.IsNullOrWhiteSpace(draft.Currency) ? (_financeOverview?.DefaultCurrency ?? "RUB") : draft.Currency,
                    HappenedAt = DateTimeOffset.TryParse(draft.HappenedAt, out var parsed) ? parsed : DateTimeOffset.Now,
                    CategoryId = singleCategoryId,
                    Items = items,
                    DestinationAccountId = Guid.TryParse(draft.DestinationAccountId, out var destinationAccountId) ? destinationAccountId : null,
                    SourceType = draft.SourceType,
                    MerchantName = draft.Direction == "transfer" || string.IsNullOrWhiteSpace(draft.MerchantName) ? null : draft.MerchantName.Trim()
                };
            }
        }

        private List<(string Code, string Label, string Description)> GetFinanceAccountProviders() => _isRussian
            ? new List<(string, string, string)>
            {
                ("tbank", "Т-Банк", "Основная карта или счёт"),
                ("sber", "Сбер", "Карты и счета Сбера"),
                ("alfa", "Альфа", "Счета Альфа-Банка"),
                ("vtb", "ВТБ", "Карты и счета ВТБ"),
                ("gazprombank", "Газпромбанк", "Банковский счёт"),
                ("yandex", "Яндекс", "Яндекс Банк / карта"),
                ("ozon", "Ozon", "Ozon Банк / карта"),
                ("raiffeisen", "Райффайзен", "Банковский счёт"),
                ("rosselkhoz", "Россельхозбанк", "Банковский счёт"),
                ("other_bank", "Другой счёт", "Если банка нет в списке"),
                ("cash", "Наличные", "Физические деньги")
            }
            : new List<(string, string, string)>
            {
                ("tbank", "T-Bank", "Primary card or account"),
                ("sber", "Sber", "Sber cards and accounts"),
                ("alfa", "Alfa", "Alfa Bank accounts"),
                ("vtb", "VTB", "VTB cards and accounts"),
                ("gazprombank", "Gazprombank", "Bank account"),
                ("yandex", "Yandex", "Yandex Bank / card"),
                ("ozon", "Ozon", "Ozon Bank / card"),
                ("raiffeisen", "Raiffeisen", "Bank account"),
                ("rosselkhoz", "Rosselkhozbank", "Bank account"),
                ("other_bank", "Other account", "When the bank is missing"),
                ("cash", "Cash", "Physical money")
            };

        private static long? ParseMoneyInputToMinor(string raw)
        {
            var normalized = raw.Trim().Replace(" ", string.Empty).Replace(',', '.');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            return (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);
        }

        private static string FormatAmountInput(long amountMinor)
        {
            var amount = amountMinor / 100m;
            if (decimal.Truncate(amount) == amount)
            {
                return amount.ToString("0", CultureInfo.InvariantCulture);
            }

            return amount.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        private bool TryValidateFinanceDraftBalances(
            IReadOnlyList<FinanceCreateTransactionRequest> requests,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_financeOverview == null)
            {
                return true;
            }

            var accounts = _financeOverview.Accounts.ToDictionary(item => item.Id, item => item);
            var simulatedBalances = accounts.ToDictionary(item => item.Key, item => item.Value.BalanceMinor);

            foreach (var request in requests)
            {
                if (!accounts.TryGetValue(request.AccountId, out var sourceAccount))
                {
                    continue;
                }

                if (IsProtectedOwnFundsAccount(sourceAccount))
                {
                    var amountMinor = request.AmountMinor ?? 0;
                    if (string.Equals(request.Direction, "expense", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.Direction, "transfer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (simulatedBalances[sourceAccount.Id] < amountMinor)
                        {
                            errorMessage = _isRussian
                                ? "На счёте меньше средств, чем указано в операции. Проверьте сумму или выберите другой счёт."
                                : "There are not enough funds on the account for this operation.";
                            return false;
                        }

                        simulatedBalances[sourceAccount.Id] -= amountMinor;
                    }
                    else if (string.Equals(request.Direction, "income", StringComparison.OrdinalIgnoreCase))
                    {
                        simulatedBalances[sourceAccount.Id] += amountMinor;
                    }
                }

                if (string.Equals(request.Direction, "transfer", StringComparison.OrdinalIgnoreCase) &&
                    request.DestinationAccountId.HasValue &&
                    accounts.TryGetValue(request.DestinationAccountId.Value, out var destinationAccount) &&
                    IsProtectedOwnFundsAccount(destinationAccount))
                {
                    simulatedBalances[destinationAccount.Id] += request.AmountMinor ?? 0;
                }
            }

            return true;
        }

        private static bool IsProtectedOwnFundsAccount(FinanceAccount account) =>
            string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(account.Kind, "bank_card", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(account.CardType, "credit", StringComparison.OrdinalIgnoreCase));

        private static bool IsLoanAccount(FinanceAccount account) =>
            string.Equals(account.Kind, "loan", StringComparison.OrdinalIgnoreCase);

        private List<FinanceAccount> GetLoanAccounts() => _financeOverview?.Accounts
            .Where(IsLoanAccount)
            .OrderBy(item => GetFinanceAccountDisplayName(item), StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? new List<FinanceAccount>();

        private List<FinanceAccount> GetLoanPaymentSourceAccounts() => _financeOverview?.Accounts
            .Where(IsProtectedOwnFundsAccount)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => GetFinanceAccountDisplayName(item), StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? new List<FinanceAccount>();

        private long? GetLoanScheduledPaymentAmount(FinanceAccount account)
        {
            if (!IsLoanAccount(account))
            {
                return null;
            }

            var remaining = account.LoanRemainingPaymentsCount ?? 0;
            if (remaining <= 0)
            {
                return null;
            }

            if (remaining == 1 && account.LoanFinalPaymentMinor is > 0)
            {
                return account.LoanFinalPaymentMinor;
            }

            return account.LoanPaymentAmountMinor;
        }

        private long? GetLoanRemainingPlannedAmount(FinanceAccount account)
        {
            if (!IsLoanAccount(account))
            {
                return null;
            }

            var remaining = account.LoanRemainingPaymentsCount ?? 0;
            if (remaining <= 0)
            {
                return 0;
            }

            var regularPayment = account.LoanPaymentAmountMinor ?? 0;
            var finalPayment = account.LoanFinalPaymentMinor ?? regularPayment;
            if (remaining == 1)
            {
                return finalPayment;
            }

            return ((remaining - 1L) * regularPayment) + finalPayment;
        }

        private long GetOverviewLoanFullDebtMinor(FinanceOverview overview) =>
            overview.Accounts
                .Where(IsLoanAccount)
                .Select(GetLoanRemainingPlannedAmount)
                .Where(amount => amount.HasValue)
                .Sum(amount => amount ?? 0);

        private int? GetLoanPaidPaymentsCount(FinanceAccount account)
        {
            if (!IsLoanAccount(account) ||
                account.LoanTotalPaymentsCount is not int totalPayments ||
                account.LoanRemainingPaymentsCount is not int remainingPayments)
            {
                return null;
            }

            return Math.Max(totalPayments - remainingPayments, 0);
        }

        private long GetOverviewCardMetric(FinanceOverview overview, string cardId) => cardId switch
        {
            "total_balance" => Math.Abs(overview.TotalBalanceMinor),
            "card_balance" => Math.Abs(overview.CardBalanceMinor),
            "cash_balance" => Math.Abs(overview.CashBalanceMinor),
            "credit_debt" => Math.Abs(overview.CreditDebtMinor),
            "loan_debt" => Math.Abs(overview.LoanDebtMinor),
            "loan_full_debt" => Math.Abs(GetOverviewLoanFullDebtMinor(overview)),
            "credit_spend" => Math.Abs(overview.CreditSpendMinor),
            "month_income" => Math.Abs(overview.MonthIncomeMinor),
            "month_expense" => Math.Abs(overview.MonthExpenseMinor),
            "month_result" => Math.Abs(overview.MonthNetMinor),
            "recent_transactions" => overview.RecentTransactions.Count,
            _ => 0
        };

        private static Color GetOverviewCardAccent(string cardId) => cardId switch
        {
            "total_balance" => Color.FromArgb(255, 35, 224, 138),
            "card_balance" => Color.FromArgb(255, 92, 133, 255),
            "cash_balance" => Color.FromArgb(255, 214, 154, 63),
            "credit_debt" => Color.FromArgb(255, 255, 129, 82),
            "loan_debt" => Color.FromArgb(255, 255, 174, 70),
            "loan_full_debt" => Color.FromArgb(255, 255, 111, 82),
            "credit_spend" => Color.FromArgb(255, 114, 160, 255),
            "month_income" => Color.FromArgb(255, 35, 224, 138),
            "month_expense" => Color.FromArgb(255, 255, 111, 142),
            "month_result" => Color.FromArgb(255, 255, 111, 142),
            _ => Color.FromArgb(255, 255, 255, 255)
        };

        private string GetOverviewCardLabel(string cardId) => cardId switch
        {
            "total_balance" => _isRussian ? "Общий баланс" : "Total balance",
            "card_balance" => _isRussian ? "На картах" : "On cards",
            "cash_balance" => _isRussian ? "Наличные" : "Cash",
            "credit_debt" => _isRussian ? "Долг по кредиткам" : "Credit card debt",
            "loan_debt" => _isRussian ? "Долг по кредитам" : "Loan debt",
            "loan_full_debt" => _isRussian ? "Полный долг по кредитам" : "Full loan payoff",
            "credit_spend" => _isRussian ? "Покупки по кредиткам" : "Credit card spending",
            "month_income" => _isRussian ? "Доходы за месяц" : "Month income",
            "month_expense" => _isRussian ? "Расходы за месяц" : "Month expense",
            "month_result" => _isRussian ? "Результат месяца" : "Month result",
            "recent_transactions" => _isRussian ? "Краткий список транзакций" : "Short transaction list",
            _ => cardId
        };

        private string FormatFinanceMonthLabel(string? month)
        {
            if (string.IsNullOrWhiteSpace(month) || !DateTime.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            {
                return _isRussian ? "Текущий месяц" : "Current month";
            }

            var culture = _isRussian ? new CultureInfo("ru-RU") : new CultureInfo("en-US");
            return value.ToString("Y", culture);
        }

        private bool HandleFinanceSessionError(Exception ex)
        {
            if (!IsExpiredSessionError(ex.Message))
            {
                return false;
            }

            _sessionStore.Clear();
            _session = null;
            _financeOverview = null;
            FinanceErrorCard.Visibility = Visibility.Collapsed;
            SetStatus(
                _isRussian
                    ? "Сессия истекла. Войдите снова, чтобы открыть финансы."
                    : "Session expired. Sign in again to open finance.",
                true);
            UpdateShellState();
            return true;
        }

        private async Task LoadFinanceTransactionsMonthAsync(string? month, bool force = false)
        {
            if (_financeClient == null || _session == null)
            {
                return;
            }

            if (!force && string.Equals(_financeSelectedTransactionsMonth, month, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                _financeTransactionsMonthLoading = true;
                _financeSelectedTransactionsMonth = month;
                RenderFinanceContent();

                var accessToken = await GetFreshFinanceAccessTokenAsync();
                _financeTransactionsMonth = await _financeClient.GetTransactionsAsync(accessToken, month);
                CacheFinanceTransactionsMonth(_financeTransactionsMonth);
                _financeAnalytics = null;
                _financeSelectedTransactionsMonth = _financeTransactionsMonth.Month;

                if (FinanceTransactionsCard.Visibility == Visibility.Visible)
                {
                    AnimateElementRefresh(FinanceTransactionsCard);
                }
            }
            catch (Exception ex)
            {
                if (HandleFinanceSessionError(ex))
                {
                    return;
                }

                SetStatus(
                    LocalizeAuthError(
                        ex.Message,
                        _isRussian ? "Не удалось загрузить транзакции за выбранный месяц." : "Failed to load transactions for the selected month."),
                    true);
            }
            finally
            {
                _financeTransactionsMonthLoading = false;
                RenderFinanceContent();
            }
        }

        private static bool IsExpiredSessionError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("JWT expired", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("PGRST303", StringComparison.OrdinalIgnoreCase);
        }

    }
}

