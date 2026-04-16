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
        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            ApplyAdaptiveShellLayout();
        }

        private void ApplyWindowIcon()
        {
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!System.IO.File.Exists(iconPath) || AppWindow == null)
            {
                return;
            }

            AppWindow.SetIcon(iconPath);
        }

        private void MaximizeWindowOnStartup()
        {
            if (AppWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        }

        private void RussianMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetLanguage(true);
        }

        private void EnglishMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetLanguage(false);
        }

        private void LoginTab_Click(object sender, RoutedEventArgs e)
        {
            _mode = AuthMode.Login;
            ApplyMode();
        }

        private void RegisterTab_Click(object sender, RoutedEventArgs e)
        {
            _mode = AuthMode.Register;
            ApplyMode();
        }

        private void NavOverviewButton_Click(object sender, RoutedEventArgs e) => SetSection(DashboardSection.Home);
        private void NavFinanceButton_Click(object sender, RoutedEventArgs e) => SetSection(DashboardSection.Finance);
        private void NavHealthButton_Click(object sender, RoutedEventArgs e) => SetSection(DashboardSection.Health);
        private void NavTasksButton_Click(object sender, RoutedEventArgs e) => SetSection(DashboardSection.Tasks);
        private void NavChatButton_Click(object sender, RoutedEventArgs e) => SetSection(DashboardSection.Chat);
        private void NavSettingsButton_Click(object sender, RoutedEventArgs e) => SetSection(DashboardSection.Settings);
        private void FinanceOverviewTabButton_Click(object sender, RoutedEventArgs e) => SetFinanceTab(FinanceTab.Overview);
        private void FinanceAccountsTabButton_Click(object sender, RoutedEventArgs e) => SetFinanceTab(FinanceTab.Accounts);
        private void FinanceTransactionsTabButton_Click(object sender, RoutedEventArgs e) => SetFinanceTab(FinanceTab.Transactions);
        private void FinanceCategoriesTabButton_Click(object sender, RoutedEventArgs e) => SetFinanceTab(FinanceTab.Categories);
        private void FinanceAnalyticsTabButton_Click(object sender, RoutedEventArgs e) => SetFinanceTab(FinanceTab.Analytics);
        private async void FinanceConfigureOverviewButton_Click(object sender, RoutedEventArgs e) => await ShowOverviewSettingsDialogAsync();
        private async void FinanceAddTransactionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowFinanceTransactionFlowAsync();
            }
            catch (Exception ex)
            {
                await ShowFinanceMessageDialogAsync(
                    _isRussian ? "Не удалось добавить транзакцию" : "Unable to add transaction",
                    GetFriendlyFinanceFlowError(ex));
            }
        }
        private async void FinanceAddAccountButton_Click(object sender, RoutedEventArgs e) => await ShowAccountDialogAsync(null);
        private void SecondaryTabOneButton_Click(object sender, RoutedEventArgs e) => SetSecondaryTabByIndex(0);
        private void SecondaryTabTwoButton_Click(object sender, RoutedEventArgs e) => SetSecondaryTabByIndex(1);
        private void SecondaryTabThreeButton_Click(object sender, RoutedEventArgs e) => SetSecondaryTabByIndex(2);
        private void SecondaryTabFourButton_Click(object sender, RoutedEventArgs e) => SetSecondaryTabByIndex(3);
        private async void FinanceRetryButton_Click(object sender, RoutedEventArgs e) => await LoadFinanceOverviewAsync();
        private void FinanceBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_financeOnboardingStep > 0)
            {
                _financeOnboardingStep--;
                ApplyFinanceOnboardingStep();
            }
        }

        private void FinanceNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_financeOverview?.OnboardingCompleted == false && _financeOnboardingStep < 2)
            {
                _financeOnboardingStep++;
                ApplyFinanceOnboardingStep();
                return;
            }

            _ = CompleteFinanceOnboardingAsync(skip: false);
        }

        private async void FinanceSkipButton_Click(object sender, RoutedEventArgs e) =>
            await CompleteFinanceOnboardingAsync(skip: true);
        private async void SettingsDisplayNameButton_Click(object sender, RoutedEventArgs e) => await SaveDisplayNameAsync();
        private async void SettingsEmailButton_Click(object sender, RoutedEventArgs e) => await SaveEmailAsync();
        private async void SettingsGeminiButton_Click(object sender, RoutedEventArgs e) => await SaveGeminiApiKeyAsync();
        private async void SettingsPasswordButton_Click(object sender, RoutedEventArgs e) => await SavePasswordAsync();
        private async void SettingsGoogleButton_Click(object sender, RoutedEventArgs e) => await ToggleGoogleIdentityAsync();
        private async void SettingsFinanceResetButton_Click(object sender, RoutedEventArgs e) => await ConfirmFinanceResetAsync();
        private async void SettingsDeleteButton_Click(object sender, RoutedEventArgs e) => await ConfirmDeleteAccountAsync();

        private void ApplyTheme()
        {
            RootGrid.RequestedTheme = ElementTheme.Dark;
            ApplyWindowChrome();
        }

        private void ApplyText()
        {
            LogoText.Text = "ASSISTANT";
            WindowCaptionText.Text = _isRussian ? "Windows 11 client" : "Windows 11 client";
            LangButton.Content = _isRussian ? "RU" : "EN";
            RussianMenuItem.Text = _isRussian ? "Русский" : "Russian";
            EnglishMenuItem.Text = "English";
            RussianMenuItem.IsChecked = _isRussian;
            EnglishMenuItem.IsChecked = !_isRussian;

            if (_isRussian)
            {
                LoginTab.Content = "Вход";
                RegisterTab.Content = "Регистрация";
                EmailInput.Header = "EMAIL";
                EmailInput.PlaceholderText = "you@example.com";
                PasswordInput.Header = "ПАРОЛЬ";
                PasswordInput.PlaceholderText = "••••••••";
                ConfirmPasswordInput.Header = "ПОВТОРИТЕ ПАРОЛЬ";
                ConfirmPasswordInput.PlaceholderText = "••••••••";
                RememberCheck.Content = "Запомнить устройство";
                ForgotButton.Content = "Забыли пароль?";
                AuthDividerText.Text = "или";
                GoogleButton.Content = "Продолжить с Google";
                BackToLoginButton.Content = "Вернуться к входу";
            }
            else
            {
                LoginTab.Content = "Sign in";
                RegisterTab.Content = "Register";
                EmailInput.Header = "EMAIL";
                EmailInput.PlaceholderText = "you@example.com";
                PasswordInput.Header = "PASSWORD";
                PasswordInput.PlaceholderText = "••••••••";
                ConfirmPasswordInput.Header = "CONFIRM PASSWORD";
                ConfirmPasswordInput.PlaceholderText = "••••••••";
                RememberCheck.Content = "Remember this device";
                ForgotButton.Content = "Forgot password?";
                AuthDividerText.Text = "or";
                GoogleButton.Content = "Continue with Google";
                BackToLoginButton.Content = "Back to sign in";
            }

            ApplyDashboardText();
            ApplyFinanceText();
            ApplyTheme();
            ApplyMode();
            UpdateShellState();
        }

        private void ApplyDashboardText()
        {
            NavOverviewLabel.Text = _isRussian ? "Главная" : "Home";
            NavFinanceLabel.Text = _isRussian ? "Финансы" : "Finance";
            NavHealthLabel.Text = _isRussian ? "Здоровье" : "Health";
            NavTasksLabel.Text = _isRussian ? "Задачи" : "Tasks";
            NavChatLabel.Text = _isRussian ? "Чат" : "Chat";
            NavSettingsLabel.Text = _isRussian ? "Настройки" : "Settings";
            CompactNavOverviewLabel.Text = NavOverviewLabel.Text;
            CompactNavFinanceLabel.Text = NavFinanceLabel.Text;
            CompactNavHealthLabel.Text = NavHealthLabel.Text;
            CompactNavTasksLabel.Text = NavTasksLabel.Text;
            CompactNavChatLabel.Text = NavChatLabel.Text;
            CompactNavSettingsLabel.Text = NavSettingsLabel.Text;
            ApplySettingsText();

            DashboardLabel.Text = GetSectionConfig().Eyebrow.ToUpperInvariant();
            DashboardStatusBadge.Text = GetSectionConfig().Badge;
            DashboardStatusTitle.Text = "Assistant2 / Supabase";
            DashboardStatusBody.Text = _isRussian
                ? "Синхронизация между клиентами в работе"
                : "Cross-client sync is in progress";

            ApplySectionContent(false);
        }

        private void ApplyFinanceText()
        {
            FinanceOverviewTabButton.Content = _isRussian ? "Обзор" : "Overview";
            FinanceAccountsTabButton.Content = _isRussian ? "Счета" : "Accounts";
            FinanceTransactionsTabButton.Content = _isRussian ? "Транзакции" : "Transactions";
            FinanceCategoriesTabButton.Content = _isRussian ? "Категории" : "Categories";
            FinanceAnalyticsTabButton.Content = _isRussian ? "Аналитика" : "Analytics";
            FinanceAddTransactionButton.Content = _isRussian ? "Добавить транзакцию" : "Add transaction";
            FinanceAddAccountButton.Content = _isRussian ? "Добавить счёт" : "Add account";
            FinanceConfigureOverviewButton.Content = _isRussian ? "Настроить обзор" : "Overview settings";

            FinanceLoadingText.Text = _isRussian ? "Загружаем финансовую сводку..." : "Loading finance overview...";
            FinanceErrorTitle.Text = _isRussian ? "Не удалось загрузить финансы" : "Failed to load finance";
            FinanceRetryButton.Content = _isRussian ? "Повторить" : "Retry";

            FinanceOnboardingBadge.Text = _isRussian ? "ОНБОРДИНГ" : "ONBOARDING";
            FinanceCurrencyLabel.Text = _isRussian ? "Основная валюта" : "Primary currency";
            FinanceBankLabel.Text = _isRussian ? "Банк основной карты" : "Primary card bank";
            FinanceOnboardingCardTypeLabel.Text = _isRussian ? "Тип основной карты" : "Primary card type";
            FinanceOnboardingLastFourLabel.Text = _isRussian ? "Последние 4 цифры" : "Last 4 digits";
            FinanceCashLabel.Text = _isRussian ? "Наличные" : "Cash";
            FinanceOnboardingLastFourInput.PlaceholderText = _isRussian ? "1234" : "1234";
            FinancePrimaryBalanceInput.PlaceholderText = _isRussian ? "Баланс основной карты" : "Primary card balance";
            FinanceCashInput.PlaceholderText = _isRussian ? "Сумма наличных" : "Cash amount";
            FinanceBackButton.Content = _isRussian ? "Назад" : "Back";
            FinanceSkipButton.Content = _isRussian ? "Пропустить" : "Skip";

            FinanceBalanceLabel.Text = _isRussian ? "ТЕКУЩИЙ БАЛАНС" : "CURRENT BALANCE";
            FinanceAccountsTitle.Text = _isRussian ? "Счета" : "Accounts";
            FinanceTransactionsTitle.Text = _isRussian ? "Последние транзакции" : "Recent transactions";
            FinanceSettingsTitle.Text = _isRussian ? "Категории" : "Categories";

            PopulateFinanceInputs();
            ApplyFinanceOnboardingStep();
            ApplyFinanceTabButtons();
            RenderFinanceContent();
        }

        private void ApplySettingsText()
        {
            SettingsLoadingText.Text = _isRussian ? "Загружаем настройки..." : "Loading settings...";
            SettingsProfileTitle.Text = _isRussian ? "Профиль" : "Profile";
            SettingsProfileBody.Text = _isRussian
                ? "Имя аккаунта и почта для входа управляются через Supabase Auth."
                : "Your name and sign-in email are managed through Supabase Auth.";
            SettingsDisplayNameLabel.Text = _isRussian ? "Имя" : "Name";
            SettingsDisplayNameInput.PlaceholderText = _isRussian ? "Как к вам обращаться?" : "How should we address you?";
            SettingsDisplayNameButton.Content = _isRussian ? "Сохранить имя" : "Save name";

            SettingsEmailTitle.Text = _isRussian ? "Смена почты" : "Change email";
            SettingsEmailBody.Text = _isRussian
                ? "После запроса Supabase может попросить подтвердить новую почту через письмо."
                : "Supabase may require email confirmation before the new address becomes active.";
            SettingsEmailLabel.Text = "Email";
            SettingsEmailInput.PlaceholderText = "you@example.com";
            SettingsEmailButton.Content = _isRussian ? "Сменить почту" : "Change email";

            SettingsPreferencesTitle.Text = _isRussian ? "Параметры" : "Preferences";
            SettingsPreferencesBody.Text = _isRussian
                ? "Подключаем то, что позже будет использоваться в сценариях ассистента."
                : "Connections that the assistant workflows will use later.";
            SettingsGeminiTitle.Text = _isRussian ? "Google Gemini" : "Google Gemini";
            SettingsGeminiBody.Text = _isRussian
                ? "Используем Gemini для AI-улучшений: извлечения данных из документов, автозаполнения и следующих умных сценариев. Ключ хранится в вашей персональной записи Supabase."
                : "Gemini powers AI enhancements like document extraction, autofill, and future smart workflows. The key is stored in your personal Supabase settings row.";
            SettingsAiEnhancementsToggle.Header = _isRussian ? "Включить AI-улучшения" : "Enable AI enhancements";
            SettingsAiEnhancementsToggle.OffContent = _isRussian ? "Выключено" : "Off";
            SettingsAiEnhancementsToggle.OnContent = _isRussian ? "Включено" : "On";
            SettingsGeminiButton.Content = _isRussian ? "Сохранить" : "Save";
            SettingsGeminiClearButton.Content = _isRussian ? "Удалить ключ" : "Delete key";
            SettingsGeminiLinkButton.Content = _isRussian
                ? "Получить бесплатный ключ в Google AI Studio"
                : "Get a free key in Google AI Studio";
            ApplySettingsGeminiPresentation();

            SettingsSecurityTitle.Text = _isRussian ? "Безопасность" : "Security";
            SettingsSecurityBody.Text = _isRussian
                ? "Пароль, связанный Google и критические действия аккаунта."
                : "Password, linked Google account, and destructive account actions.";
            SettingsPasswordTitle.Text = _isRussian ? "Смена пароля" : "Change password";
            SettingsPasswordBody.Text = _isRussian
                ? "Используйте минимум 8 символов. После обновления текущая сессия сохранится."
                : "Use at least 8 characters. Your current session stays active after update.";
            SettingsPasswordLabel.Text = _isRussian ? "Новый пароль" : "New password";
            SettingsPasswordConfirmLabel.Text = _isRussian ? "Повторите пароль" : "Confirm password";
            SettingsPasswordButton.Content = _isRussian ? "Обновить пароль" : "Update password";
            SettingsGoogleTitle.Text = _isRussian ? "Google аккаунт" : "Google account";
            SettingsLogoutTitle.Text = _isRussian ? "Выход из аккаунта" : "Sign out";
            SettingsLogoutBody.Text = _isRussian
                ? "Текущая сессия будет завершена на этом устройстве."
                : "The current session will be closed on this device.";
            SettingsLogoutButton.Content = _isRussian ? "Выйти из аккаунта" : "Sign out";
            SettingsFinanceResetTitle.Text = _isRussian ? "Сброс финансов" : "Reset finance";
            SettingsFinanceResetBody.Text = _isRussian
                ? "Удалит все счета, транзакции, категории и настройки обзора. После этого раздел финансов откроется заново с начальной настройки."
                : "Removes all accounts, transactions, categories, and overview settings. The finance workspace will start from onboarding again.";
            SettingsFinanceResetButton.Content = _isRussian ? "Сбросить финансы" : "Reset finance";
            SettingsDeleteTitle.Text = _isRussian ? "Удаление аккаунта" : "Delete account";
            SettingsDeleteBody.Text = _isRussian
                ? "Действие необратимо. Все пользовательские данные и сессии будут удалены."
                : "This action is irreversible. All user data and sessions will be removed.";
            SettingsDeleteButton.Content = _isRussian ? "Удалить аккаунт" : "Delete account";

            RenderSettingsContent();
        }

        private void ApplyMode()
        {
            AuthTitleText.Text = _mode switch
            {
                AuthMode.Register => _isRussian ? "Создайте аккаунт" : "Create your account",
                AuthMode.Forgot => _isRussian ? "Восстановление доступа" : "Recover access",
                AuthMode.Reset => _isRussian ? "Новый пароль" : "Set a new password",
                _ => _isRussian ? "Добро пожаловать" : "Welcome back"
            };

            AuthSubtitleText.Text = _mode switch
            {
                AuthMode.Register => _isRussian
                    ? "Один аккаунт для Web, Android и Windows."
                    : "One account for Web, Android, and Windows.",
                AuthMode.Forgot => _isRussian
                    ? "Отправим письмо со ссылкой для восстановления."
                    : "We will send a recovery link to your email.",
                AuthMode.Reset => _isRussian
                    ? "Задайте новый пароль для продолжения."
                    : "Set a new password to continue.",
                _ => _isRussian
                    ? "Войдите, чтобы продолжить работу."
                    : "Sign in to continue."
            };

            ConfirmPasswordPanel.Visibility = (_mode == AuthMode.Register || _mode == AuthMode.Reset)
                ? Visibility.Visible
                : Visibility.Collapsed;

            PasswordInput.Visibility = _mode == AuthMode.Forgot ? Visibility.Collapsed : Visibility.Visible;

            RememberCheck.Visibility = _mode == AuthMode.Login ? Visibility.Visible : Visibility.Collapsed;
            ForgotButton.Visibility = _mode == AuthMode.Login ? Visibility.Visible : Visibility.Collapsed;
            AuthAssistRow.Visibility = _mode == AuthMode.Login ? Visibility.Visible : Visibility.Collapsed;
            GoogleButton.Visibility = (AppConfig.GoogleAuthEnabled && (_mode == AuthMode.Login || _mode == AuthMode.Register))
                ? Visibility.Visible
                : Visibility.Collapsed;
            AuthProviderDivider.Visibility = GoogleButton.Visibility;
            BackToLoginButton.Visibility = (_mode == AuthMode.Forgot || _mode == AuthMode.Reset)
                ? Visibility.Visible
                : Visibility.Collapsed;

            SubmitButton.Content = _mode switch
            {
                AuthMode.Login => _isRussian ? "Войти" : "Sign in",
                AuthMode.Register => _isRussian ? "Создать аккаунт" : "Create account",
                AuthMode.Forgot => _isRussian ? "Отправить ссылку" : "Send reset link",
                AuthMode.Reset => _isRussian ? "Сохранить пароль" : "Update password",
                _ => SubmitButton.Content
            };

            var selectedForeground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PrimaryButtonForegroundBrush"];
            var defaultForeground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"];
            var selectedBackground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentBrush"];
            var transparentBackground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0));
            LoginTab.Foreground = _mode == AuthMode.Login ? selectedForeground : defaultForeground;
            LoginTab.Background = _mode == AuthMode.Login ? selectedBackground : transparentBackground;
            RegisterTab.Foreground = _mode == AuthMode.Register ? selectedForeground : defaultForeground;
            RegisterTab.Background = _mode == AuthMode.Register ? selectedBackground : transparentBackground;
            AuthStatusBar.IsOpen = false;
            AuthStatusBar.Visibility = Visibility.Collapsed;
            AuthStatusBar.Title = string.Empty;
            AuthStatusBar.Message = string.Empty;
            AnimateAuthForm();
        }

        private void UpdateShellState()
        {
            var hasSession = HasSession();
            AuthShell.Visibility = hasSession ? Visibility.Collapsed : Visibility.Visible;
            DashboardShell.Visibility = hasSession ? Visibility.Visible : Visibility.Collapsed;
            ApplyAdaptiveShellLayout();

            if (hasSession)
            {
                DashboardWelcomeTitle.Text = _isRussian
                    ? $"{SectionLabel(_section)}, {ExtractDisplayName()}."
                    : $"{SectionLabel(_section)}, {ExtractDisplayName()}.";
                DashboardSubtitle.Text = GetSectionConfig().Note;
                ApplySectionContent(false);
                if (_section == DashboardSection.Finance)
                {
                    _ = LoadFinanceOverviewAsync();
                }
                if (_section == DashboardSection.Settings)
                {
                    _ = LoadSettingsAsync();
                }
            }
        }

        private void SetSection(DashboardSection section)
        {
            _section = section;
            _shellViewModel.SetSection(ToApplicationSection(section));
            _activeSubsection = _shellViewModel.ActiveSubsection;
            if (_section == DashboardSection.Finance)
            {
                _financeTab = FinanceTab.Overview;
            }
            if (HasSession())
            {
                DashboardWelcomeTitle.Text = _isRussian
                    ? $"{SectionLabel(_section)}, {ExtractDisplayName()}."
                    : $"{SectionLabel(_section)}, {ExtractDisplayName()}.";
                DashboardSubtitle.Text = GetSectionConfig().Note;
                DashboardLabel.Text = GetSectionConfig().Eyebrow.ToUpperInvariant();
                ApplySectionContent(true);
                if (_section == DashboardSection.Finance)
                {
                    _ = LoadFinanceOverviewAsync();
                }
                if (_section == DashboardSection.Settings)
                {
                    _ = LoadSettingsAsync();
                }
            }
        }

        private void ApplySectionContent(bool animated)
        {
            var config = GetSectionConfig();
            PlaceholderBadge.Text = config.Badge;
            PlaceholderTitle.Text = SectionLabel(_section);
            PlaceholderBody.Text = config.Note;

            ApplyNavButtonState(NavOverviewButton, _section == DashboardSection.Home);
            ApplyNavButtonState(NavFinanceButton, _section == DashboardSection.Finance);
            ApplyNavButtonState(NavHealthButton, _section == DashboardSection.Health);
            ApplyNavButtonState(NavTasksButton, _section == DashboardSection.Tasks);
            ApplyNavButtonState(NavChatButton, _section == DashboardSection.Chat);
            ApplyNavButtonState(NavSettingsButton, _section == DashboardSection.Settings);
            ApplyNavButtonState(CompactNavOverviewButton, _section == DashboardSection.Home);
            ApplyNavButtonState(CompactNavFinanceButton, _section == DashboardSection.Finance);
            ApplyNavButtonState(CompactNavHealthButton, _section == DashboardSection.Health);
            ApplyNavButtonState(CompactNavTasksButton, _section == DashboardSection.Tasks);
            ApplyNavButtonState(CompactNavChatButton, _section == DashboardSection.Chat);
            ApplyNavButtonState(CompactNavSettingsButton, _section == DashboardSection.Settings);
            ApplySecondaryTabs();

            ApplyStageSurfaceAppearance();

            var financeVisible = _section == DashboardSection.Finance;
            var settingsVisible = _section == DashboardSection.Settings;
            DashboardSummaryRow.Visibility = financeVisible || settingsVisible ? Visibility.Collapsed : Visibility.Visible;
            SecondaryTabsCard.Visibility = financeVisible ? Visibility.Collapsed : Visibility.Visible;
            PlaceholderCard.Visibility = financeVisible || settingsVisible ? Visibility.Collapsed : Visibility.Visible;
            SettingsContent.Visibility = settingsVisible ? Visibility.Visible : Visibility.Collapsed;
            FinanceContent.Visibility = financeVisible ? Visibility.Visible : Visibility.Collapsed;
            RenderSettingsContent();

            if (!animated)
            {
                if (!financeVisible && !settingsVisible)
                {
                    PlaceholderCard.Opacity = 1;
                    PlaceholderTranslate.Y = 0;
                }
                if (settingsVisible)
                {
                    SettingsContent.Opacity = 1;
                    SettingsTranslate.Y = 0;
                }
                return;
            }

            if (financeVisible)
            {
                FinanceContent.Opacity = 0;
                FinanceTranslate.Y = 14;
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(260))
                };
                var slideIn = new DoubleAnimation
                {
                    From = 14,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(260))
                };
                Storyboard.SetTarget(fadeIn, FinanceContent);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                Storyboard.SetTarget(slideIn, FinanceTranslate);
                Storyboard.SetTargetProperty(slideIn, "Y");
                var financeStoryboard = new Storyboard();
                financeStoryboard.Children.Add(fadeIn);
                financeStoryboard.Children.Add(slideIn);
                financeStoryboard.Begin();
                return;
            }

            if (settingsVisible)
            {
                SettingsContent.Opacity = 0;
                SettingsTranslate.Y = 14;
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(260))
                };
                var slideIn = new DoubleAnimation
                {
                    From = 14,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(260))
                };
                Storyboard.SetTarget(fadeIn, SettingsContent);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                Storyboard.SetTarget(slideIn, SettingsTranslate);
                Storyboard.SetTargetProperty(slideIn, "Y");
                var settingsStoryboard = new Storyboard();
                settingsStoryboard.Children.Add(fadeIn);
                settingsStoryboard.Children.Add(slideIn);
                settingsStoryboard.Begin();
                return;
            }

            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(260))
            };

            var offsetAnimation = new DoubleAnimation
            {
                From = 14,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(260))
            };

            Storyboard.SetTarget(opacityAnimation, PlaceholderCard);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
            Storyboard.SetTarget(offsetAnimation, PlaceholderTranslate);
            Storyboard.SetTargetProperty(offsetAnimation, "Y");
            var storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(offsetAnimation);
            PlaceholderCard.Opacity = 0;
            PlaceholderTranslate.Y = 14;
            storyboard.Begin();
        }

        private void ApplyNavButtonState(Button button, bool active)
        {
            var transparent = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0));
            var isCompactButton = button.Name.StartsWith("CompactNav", StringComparison.Ordinal);

            button.Background = active
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PanelStrongBrush"]
                : (isCompactButton
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundBrush"]
                    : transparent);
            button.Foreground = active
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["InkBrush"]
                : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MutedTextBrush"];
            button.BorderBrush = active
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"]
                : (isCompactButton
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"]
                    : transparent);

            Border? iconBox = button.Name switch
            {
                nameof(NavOverviewButton) => NavOverviewIconBox,
                nameof(NavFinanceButton) => NavFinanceIconBox,
                nameof(NavHealthButton) => NavHealthIconBox,
                nameof(NavTasksButton) => NavTasksIconBox,
                nameof(NavChatButton) => NavChatIconBox,
                nameof(NavSettingsButton) => NavSettingsIconBox,
                _ => null
            };

            if (iconBox != null)
            {
                iconBox.Background = active
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["PillBackgroundBrush"]
                    : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
                iconBox.BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["StrokeBrush"];
            }
        }

        private AppShellSectionConfig GetSectionConfig()
        {
            return _shellViewModel.GetSectionConfig(ToApplicationSection(_section));
        }

        private void ApplySecondaryTabs()
        {
            var config = GetSectionConfig();
            var items = config.Subsections.ToList();
            ApplySecondaryTabButton(SecondaryTabOneButton, items.ElementAtOrDefault(0));
            ApplySecondaryTabButton(SecondaryTabTwoButton, items.ElementAtOrDefault(1));
            ApplySecondaryTabButton(SecondaryTabThreeButton, items.ElementAtOrDefault(2));
            ApplySecondaryTabButton(SecondaryTabFourButton, items.ElementAtOrDefault(3));
        }

        private void ApplySecondaryTabButton(Button button, AppShellNavItem? item)
        {
            if (item == null)
            {
                button.Visibility = Visibility.Collapsed;
                return;
            }

            button.Visibility = Visibility.Visible;
            button.Content = item.Label;
            var active = string.Equals(_activeSubsection, item.Key, StringComparison.OrdinalIgnoreCase);
            ApplyNavButtonState(button, active);
        }

        private void SetSecondaryTabByIndex(int index)
        {
            var config = GetSectionConfig();
            if (index < 0 || index >= config.Subsections.Count)
            {
                return;
            }

            _activeSubsection = config.Subsections[index].Key;
            _shellViewModel.SetSubsection(_activeSubsection);
            if (_section == DashboardSection.Finance)
            {
                SetFinanceTab(_activeSubsection switch
                {
                    "accounts" => FinanceTab.Accounts,
                    "transactions" => FinanceTab.Transactions,
                    "categories" => FinanceTab.Categories,
                    "analytics" => FinanceTab.Analytics,
                    _ => FinanceTab.Overview
                });
                return;
            }

            ApplySecondaryTabs();
            if (_section == DashboardSection.Settings)
            {
                RenderSettingsContent();
            }
        }

        private void ApplyAdaptiveShellLayout()
        {
            var width = RootGrid.ActualWidth;
            if (width <= 0 && AppWindow != null)
            {
                width = AppWindow.Size.Width;
            }

            AuthSurfaceCard.MaxWidth = width > 0 && width < 520 ? double.PositiveInfinity : 432;
            AuthSurfaceCard.Padding = width > 0 && width < 520 ? new Thickness(22) : new Thickness(30);

            _isCompactShell = width > 0 && width < 1040;
            _shellViewModel.SetCompact(_isCompactShell);
            var hasSession = HasSession();

            SidebarSurface.Visibility = hasSession && !_isCompactShell ? Visibility.Visible : Visibility.Collapsed;
            CompactPrimaryNavScroller.Visibility = hasSession && _isCompactShell ? Visibility.Visible : Visibility.Collapsed;
            DashboardShellLayout.ColumnDefinitions[0].Width = _isCompactShell ? new GridLength(0) : new GridLength(248);
            Grid.SetColumn(DashboardStageSurface, _isCompactShell ? 0 : 1);
            Grid.SetColumnSpan(DashboardStageSurface, _isCompactShell ? 2 : 1);
            DashboardStageSurface.Margin = _isCompactShell
                ? new Thickness(12, 12, 12, 12)
                : new Thickness(0);
            ApplyStageSurfaceAppearance();
        }

        private void ApplyStageSurfaceAppearance()
        {
            var chatMode = _section == DashboardSection.Chat;
            DashboardStageSurface.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[
                chatMode ? "StageBackgroundBrush" : "ShellBackgroundBrush"];
            DashboardStageSurface.CornerRadius = chatMode
                ? (_isCompactShell ? new CornerRadius(24) : new CornerRadius(30, 0, 0, 0))
                : new CornerRadius(0);
        }

        private void SetLanguage(bool isRussian)
        {
            _isRussian = isRussian;
            _localizationService.SetLanguage(isRussian);
            _shellViewModel.SetLanguage(isRussian);
            _activeSubsection = _shellViewModel.ActiveSubsection;
            ApplyText();
        }

        private static DashboardSection ParseDashboardSection(AppDashboardSection section) => section switch
        {
            AppDashboardSection.Finance => DashboardSection.Finance,
            AppDashboardSection.Health => DashboardSection.Health,
            AppDashboardSection.Tasks => DashboardSection.Tasks,
            AppDashboardSection.Chat => DashboardSection.Chat,
            AppDashboardSection.Settings => DashboardSection.Settings,
            _ => DashboardSection.Home
        };

        private static AppDashboardSection ToApplicationSection(DashboardSection section) => section switch
        {
            DashboardSection.Finance => AppDashboardSection.Finance,
            DashboardSection.Health => AppDashboardSection.Health,
            DashboardSection.Tasks => AppDashboardSection.Tasks,
            DashboardSection.Chat => AppDashboardSection.Chat,
            DashboardSection.Settings => AppDashboardSection.Settings,
            _ => AppDashboardSection.Home
        };

        private string SectionLabel(DashboardSection section) => (_isRussian, section) switch
        {
            (true, DashboardSection.Home) => "Главная",
            (true, DashboardSection.Finance) => "Финансы",
            (true, DashboardSection.Health) => "Здоровье",
            (true, DashboardSection.Tasks) => "Задачи",
            (true, DashboardSection.Chat) => "Чат",
            (true, DashboardSection.Settings) => "Настройки",
            (false, DashboardSection.Home) => "Home",
            (false, DashboardSection.Finance) => "Finance",
            (false, DashboardSection.Health) => "Health",
            (false, DashboardSection.Tasks) => "Tasks",
            (false, DashboardSection.Chat) => "Chat",
            _ => "Settings"
        };

        private string SectionDescription(DashboardSection section) => (_isRussian, section) switch
        {
            (true, DashboardSection.Home) => "Раздел в разработке. Здесь появится главный обзор проекта, быстрые действия и персональная сводка.",
            (true, DashboardSection.Finance) => "Раздел в разработке. Здесь будут бюджеты, кошельки, транзакции и финансовая аналитика.",
            (true, DashboardSection.Health) => "Раздел в разработке. Здесь появятся трекинг самочувствия, метрики и история состояния.",
            (true, DashboardSection.Tasks) => "Раздел в разработке. Здесь будут списки задач, статусы, приоритеты и рабочие потоки.",
            (true, DashboardSection.Chat) => "Раздел в разработке. Здесь останется отдельная AI-сцена для общения с ассистентом.",
            (true, DashboardSection.Settings) => "Раздел в разработке. Здесь будут параметры приложения, профиля и подключённых сервисов.",
            (false, DashboardSection.Home) => "This section is in development. It will contain the main project overview, quick actions, and personal summary.",
            (false, DashboardSection.Finance) => "This section is in development. It will contain budgets, wallets, transactions, and financial analytics.",
            (false, DashboardSection.Health) => "This section is in development. It will contain wellbeing tracking, metrics, and health history.",
            (false, DashboardSection.Tasks) => "This section is in development. It will contain task lists, statuses, priorities, and work flows.",
            (false, DashboardSection.Chat) => "This section is in development. It will keep the dedicated AI assistant scene.",
            _ => "This section is in development. It will contain app, profile, and connected service settings."
        };

        private void ApplyWindowChrome()
        {
            var titleBar = AppWindow?.TitleBar;
            if (titleBar == null)
            {
                return;
            }

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TopBarDragRegion);

            var transparent = Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0);
            var cardBackground = GetResourceColor("CardBackgroundBrush");
            var ink = GetResourceColor("InkBrush");
            var muted = GetResourceColor("MutedTextBrush");

            titleBar.BackgroundColor = transparent;
            titleBar.ForegroundColor = muted;
            titleBar.InactiveBackgroundColor = transparent;
            titleBar.InactiveForegroundColor = muted;

            titleBar.ButtonBackgroundColor = transparent;
            titleBar.ButtonForegroundColor = muted;
            titleBar.ButtonHoverBackgroundColor = cardBackground;
            titleBar.ButtonHoverForegroundColor = ink;
            titleBar.ButtonPressedBackgroundColor = cardBackground;
            titleBar.ButtonPressedForegroundColor = ink;
            titleBar.ButtonInactiveBackgroundColor = transparent;
            titleBar.ButtonInactiveForegroundColor = muted;
        }

        private Color GetResourceColor(string key)
        {
            if (Microsoft.UI.Xaml.Application.Current.Resources[key] is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0);
        }

    }
}

