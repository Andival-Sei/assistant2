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
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwRestore = 9;
        private const int SwShow = 5;

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

        private bool _isRussian = true;
        private AuthMode _mode = AuthMode.Login;
        private DashboardSection _section = DashboardSection.Home;
        private FinanceTab _financeTab = FinanceTab.Overview;
        private string _activeSubsection = "summary";
        private int _financeOnboardingStep;
        private FinanceOverview? _financeOverview;
        private FinanceTransactionsMonth? _financeTransactionsMonth;
        private List<FinanceCategory> _financeCategories = new();
        private string? _financeSelectedTransactionsMonth;
        private bool _financeTransactionsMonthLoading;
        private readonly Dictionary<string, FinanceTransactionsMonth> _financeMonthCache = new(StringComparer.OrdinalIgnoreCase);
        private FinanceAnalyticsSnapshot? _financeAnalytics;
        private string? _financeAnalyticsFromMonth;
        private string? _financeAnalyticsToMonth;
        private string _financeAnalyticsPreset = "current";
        private bool _financeAnalyticsLoading;
        private readonly SupabaseAuthClient _authClient;
        private readonly FinanceApiClient? _financeClient;
        private readonly SettingsApiClient? _settingsClient;
        private readonly SecureSessionStore _sessionStore = new();
        private readonly SecureGeminiSettingsStore _geminiSettingsStore = new();
        private readonly DisplayNameStore _displayNameStore = new();
        private AuthSession? _session;
        private SettingsSnapshot? _settingsSnapshot;
        private bool _settingsLoading;
        private string? _settingsBusyAction;
        private bool _suppressSettingsAiToggleSave;
        private string? _settingsBanner;
        private bool _settingsBannerIsError;
        private bool _isCompactShell;
        private CancellationTokenSource? _authStatusAnimationCts;
        private CancellationTokenSource? _settingsStatusAnimationCts;
        private static readonly string[] OverviewCardOrder =
        {
            "total_balance",
            "card_balance",
            "cash_balance",
            "credit_debt",
            "credit_spend",
            "month_income",
            "month_expense",
            "month_result",
            "recent_transactions"
        };

        public MainWindow()
        {
            InitializeComponent();
            RememberCheck.IsChecked = _sessionStore.LoadRememberDevice();

            if (string.IsNullOrWhiteSpace(AppConfig.SupabaseUrl) ||
                string.IsNullOrWhiteSpace(AppConfig.SupabaseAnonKey))
            {
                _authClient = new SupabaseAuthClient("http://localhost", "missing");
            }
            else
            {
                _authClient = new SupabaseAuthClient(AppConfig.SupabaseUrl, AppConfig.SupabaseAnonKey);
            }

            _financeClient = string.IsNullOrWhiteSpace(AppConfig.SupabaseUrl) ||
                string.IsNullOrWhiteSpace(AppConfig.SupabaseAnonKey)
                ? null
                : new FinanceApiClient();
            _settingsClient = string.IsNullOrWhiteSpace(AppConfig.SupabaseUrl) ||
                string.IsNullOrWhiteSpace(AppConfig.SupabaseAnonKey)
                ? null
                : new SettingsApiClient();

            ForgotButton.Click += ForgotButton_Click;
            BackToLoginButton.Click += BackToLoginButton_Click;
            SubmitButton.Click += SubmitButton_Click;
            GoogleButton.Click += GoogleButton_Click;

            ApplyText();
            ApplyTheme();
            ApplyWindowChrome();
            ApplyWindowIcon();
            MaximizeWindowOnStartup();
            ApplyMode();
            LoadSession();
            SizeChanged += MainWindow_SizeChanged;
            ApplyAdaptiveShellLayout();

            if (string.IsNullOrWhiteSpace(AppConfig.SupabaseUrl) ||
                string.IsNullOrWhiteSpace(AppConfig.SupabaseAnonKey))
            {
                SetStatus(_isRussian ? "Нет настроек Supabase." : "Supabase config is missing.", true);
                SetBusy(true);
            }
        }

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

        public async Task HandleProtocolActivationAsync(Uri uri)
        {
            var query = QueryHelpers.Parse(uri);
            if (query.TryGetValue("error", out var error))
            {
                SetStatus(error, isError: true);
                return;
            }

            if (query.TryGetValue("access_token", out var token))
            {
                _session = new AuthSession { AccessToken = token };
                PersistSession();
                SetStatus(_isRussian ? "Сессия обновлена." : "Session updated.", isError: false);
                _mode = AuthMode.Reset;
                ApplyMode();
                UpdateShellState();
                return;
            }

            if (query.TryGetValue("code", out var code))
            {
                var verifier = PkceStore.Load();
                if (string.IsNullOrWhiteSpace(verifier))
                {
                    SetStatus(_isRussian ? "Нет PKCE verifier." : "Missing PKCE verifier.", isError: true);
                    return;
                }

                try
                {
                    _session = await _authClient.ExchangeCodeForSessionAsync(code, verifier);
                    PersistSession();
                    PkceStore.Clear();

                    if (query.TryGetValue("type", out var type) &&
                        string.Equals(type, "recovery", StringComparison.OrdinalIgnoreCase))
                    {
                        _mode = AuthMode.Reset;
                        ApplyMode();
                        SetStatus(_isRussian ? "Ссылка подтверждена. Задайте новый пароль." : "Recovery link confirmed. Set a new password.", false);
                    }
                    else
                    {
                        SetStatus(_isRussian ? "Вход завершён." : "Sign-in complete.", false);
                    }

                    UpdateShellState();
                }
                catch (Exception ex)
                {
                    var localized = LocalizeAuthError(ex.Message, _isRussian ? "Не удалось завершить вход." : "Failed to finish sign-in.");
                    var details = string.IsNullOrWhiteSpace(ex.Message) ? localized : $"{localized} ({ex.Message})";
                    SetStatus(details, isError: true);
                }
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

        private void ForgotButton_Click(object sender, RoutedEventArgs e)
        {
            _mode = AuthMode.Forgot;
            ApplyMode();
        }

        private async void SettingsGeminiLinkButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://aistudio.google.com/app/apikey"));
        }

        private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
        {
            _mode = AuthMode.Login;
            ApplyMode();
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailInput.Text?.Trim() ?? string.Empty;
            var password = ReadPassword();
            var confirm = ReadConfirmPassword();

            try
            {
                SetBusy(true);

                switch (_mode)
                {
                    case AuthMode.Login:
                        _session = await _authClient.SignInWithPasswordAsync(email, password);
                        PersistSession();
                        SetStatus(_isRussian ? "Вы вошли." : "Signed in.", false);
                        break;
                    case AuthMode.Register:
                        var registerValidation = ValidateRegistration(email, password, confirm);
                        if (registerValidation != null)
                        {
                            SetStatus(registerValidation, true);
                            return;
                        }

                        _session = await _authClient.SignUpAsync(email, password, AppConfig.RedirectUri);
                        if (_session != null && !string.IsNullOrEmpty(_session.AccessToken))
                        {
                            PersistSession();
                            SetStatus(_isRussian ? "Аккаунт создан." : "Account created.", false);
                        }
                        else
                        {
                            SetStatus(_isRussian ? "Проверьте почту для подтверждения." : "Check your email to confirm.", false);
                        }
                        break;
                    case AuthMode.Forgot:
                        var forgotValidation = ValidateEmail(email);
                        if (forgotValidation != null)
                        {
                            SetStatus(forgotValidation, true);
                            return;
                        }

                        await _authClient.ResetPasswordAsync(email, AppConfig.RedirectUri);
                        SetStatus(_isRussian ? "Ссылка отправлена." : "Reset link sent.", false);
                        break;
                    case AuthMode.Reset:
                        if (_session == null || string.IsNullOrWhiteSpace(_session.AccessToken))
                        {
                            SetStatus(_isRussian ? "Нет активной сессии." : "No active session.", true);
                            return;
                        }

                        var resetValidation = ValidatePasswordForReset(email, password, confirm);
                        if (resetValidation != null)
                        {
                            SetStatus(resetValidation, true);
                            return;
                        }

                        await _authClient.UpdatePasswordAsync(_session.AccessToken, password);
                        SetStatus(_isRussian ? "Пароль обновлён." : "Password updated.", false);
                        _mode = AuthMode.Login;
                        ApplyMode();
                        break;
                }

                UpdateShellState();
            }
            catch (Exception ex)
            {
                SetStatus(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось выполнить действие." : "Failed to complete the action."), true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void GoogleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.GoogleAuthEnabled)
            {
                SetStatus(
                    _isRussian
                        ? "Google OAuth пока не настроен в проекте Supabase."
                        : "Google OAuth is not configured in this Supabase project.",
                    true);
                return;
            }

            try
            {
                SetBusy(true);
                var verifier = PkceUtil.CreateCodeVerifier();
                var challenge = PkceUtil.CreateCodeChallenge(verifier);
                PkceStore.Save(verifier);
                using var listener = new LoopbackOAuthListener(
                    AppConfig.GoogleLoopbackRedirectUri,
                    _isRussian,
                    "assistant://auth/return?source=google-auth");
                var url = _authClient.BuildGoogleAuthorizeUrl(
                    AppConfig.GoogleLoopbackRedirectUri,
                    challenge,
                    forceAccountSelection: true);
                await Launcher.LaunchUriAsync(new Uri(url));
                var callbackUri = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(2));
                BringWindowToFront();
                await HandleProtocolActivationAsync(callbackUri);
            }
            catch (Exception ex)
            {
                SetStatus(LocalizeAuthError(ex.Message, _isRussian ? "Ошибка Google OAuth." : "Google OAuth failed."), true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null || string.IsNullOrWhiteSpace(_session.AccessToken))
            {
                return;
            }

            try
            {
                SetBusy(true);
                await _authClient.SignOutAsync(_session.AccessToken);
            }
            catch (Exception ex)
            {
                SetStatus(LocalizeAuthError(ex.Message, _isRussian ? "Ошибка выхода." : "Sign-out failed."), true);
            }
            finally
            {
                _sessionStore.Clear();
                _sessionStore.SetRememberDevice(RememberCheck.IsChecked == true);
                _session = null;
                _settingsSnapshot = null;
                _settingsBanner = null;
                SetBusy(false);
                SetStatus(_isRussian ? "Вы вышли." : "Signed out.", false);
                _mode = AuthMode.Login;
                ApplyMode();
                UpdateShellState();
            }
        }

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

            var selectedForeground = (Brush)Application.Current.Resources["PrimaryButtonForegroundBrush"];
            var defaultForeground = (Brush)Application.Current.Resources["InkBrush"];
            var selectedBackground = (Brush)Application.Current.Resources["AccentBrush"];
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
            _activeSubsection = GetSectionConfig().DefaultSubsection;
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
                ? (Brush)Application.Current.Resources["PanelStrongBrush"]
                : (isCompactButton
                    ? (Brush)Application.Current.Resources["CardBackgroundBrush"]
                    : transparent);
            button.Foreground = active
                ? (Brush)Application.Current.Resources["InkBrush"]
                : (Brush)Application.Current.Resources["MutedTextBrush"];
            button.BorderBrush = active
                ? (Brush)Application.Current.Resources["StrokeBrush"]
                : (isCompactButton
                    ? (Brush)Application.Current.Resources["StrokeBrush"]
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
                    ? (Brush)Application.Current.Resources["PillBackgroundBrush"]
                    : (Brush)Application.Current.Resources["StrokeBrush"];
                iconBox.BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"];
            }
        }

        private ShellSectionConfig GetSectionConfig()
        {
            var catalog = ShellNavigationCatalog.Create(_isRussian);
            return catalog[_section.ToString()];
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

        private void ApplySecondaryTabButton(Button button, ShellNavItem? item)
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
            DashboardStageSurface.Background = (Brush)Application.Current.Resources[
                chatMode ? "StageBackgroundBrush" : "ShellBackgroundBrush"];
            DashboardStageSurface.CornerRadius = chatMode
                ? (_isCompactShell ? new CornerRadius(24) : new CornerRadius(30, 0, 0, 0))
                : new CornerRadius(0);
        }

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
                ? "Баланс складывается из всех счетов и наличных."
                : "Balance is aggregated from all accounts and cash.";
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
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                        Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                        Foreground = (Brush)Application.Current.Resources["InkBrush"]
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

            var orderedCards = overview.OverviewCards
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

        private Border CreateOverviewMetricCard(FinanceOverview overview, string cardId, bool emphasize)
        {
            var currency = overview.DefaultCurrency ?? "RUB";
            var (title, value) = cardId switch
            {
                "total_balance" => (_isRussian ? "Общий баланс" : "Total balance", FormatMoney(overview.TotalBalanceMinor, currency)),
                "card_balance" => (_isRussian ? "На картах" : "On cards", FormatMoney(overview.CardBalanceMinor, currency)),
                "cash_balance" => (_isRussian ? "Наличные" : "Cash", FormatMoney(overview.CashBalanceMinor, currency)),
                "credit_debt" => (_isRussian ? "Долг по кредиткам" : "Credit card debt", FormatMoney(-overview.CreditDebtMinor, currency)),
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"],
                TextWrapping = TextWrapping.Wrap
            });

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
                var isTransfer = string.Equals(transaction.Direction, "transfer", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(transaction.TransactionKind, "transfer", StringComparison.OrdinalIgnoreCase) ||
                                 transaction.DestinationAccountId.HasValue;
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
            BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
            BorderThickness = new Thickness(1),
            Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
            Foreground = (Brush)Application.Current.Resources["InkBrush"],
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                ? (Brush)Application.Current.Resources["PillBackgroundBrush"]
                : (Brush)Application.Current.Resources["PageBackgroundBrush"];
            button.BorderBrush = isActive
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["StrokeBrush"];
            button.Foreground = isActive
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["InkBrush"];
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
                ? (Brush)Application.Current.Resources["PillBackgroundBrush"]
                : (Brush)Application.Current.Resources["PageBackgroundBrush"],
            BorderBrush = emphasize
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["StrokeBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = emphasize ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = emphasize
                    ? (Brush)Application.Current.Resources["AccentBrush"]
                    : (Brush)Application.Current.Resources["MutedTextBrush"]
            }
        };

        private Button CreateFinanceBadgeButton(string text, bool emphasize = false)
        {
            var button = new Button
            {
                Content = text,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Normal,
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4)
            };

            button.Resources["ButtonBackground"] = (Brush)Application.Current.Resources["CardBackgroundBrush"];
            button.Resources["ButtonBackgroundPointerOver"] = (Brush)Application.Current.Resources["PageBackgroundBrush"];
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 0x42, 0x3F, 0x46));
            button.Resources["ButtonBorderBrush"] = (Brush)Application.Current.Resources["StrokeBrush"];
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));
            button.Resources["ButtonForeground"] = (Brush)Application.Current.Resources["MutedTextBrush"];
            button.Resources["ButtonForegroundPointerOver"] = (Brush)Application.Current.Resources["MutedTextBrush"];
            button.Resources["ButtonForegroundPressed"] = (Brush)Application.Current.Resources["MutedTextBrush"];
            return button;
        }

        private Button CreateFinanceSurfaceButton(
            UIElement content,
            bool centerContent = false,
            Thickness? padding = null,
            CornerRadius? cornerRadius = null)
        {
            var normalBackground = (Brush)Application.Current.Resources["PageBackgroundBrush"];
            var hoverBackground = new SolidColorBrush(Color.FromArgb(255, 0x38, 0x35, 0x3C));
            var pressedBackground = new SolidColorBrush(Color.FromArgb(255, 0x42, 0x3F, 0x46));
            var normalBorder = (Brush)Application.Current.Resources["StrokeBrush"];
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"],
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
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable Display Semibld"),
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
            });

            var card = CreateAnalyticsSectionCard(null, null, stack, new Thickness(14, 12, 14, 12));
            Grid.SetColumn(card, column);
            grid.Children.Add(card);
        }

        private async Task ShowOverviewSettingsDialogAsync()
        {
            if (_financeOverview == null)
            {
                return;
            }

            var orderedCards = (_financeOverview.OverviewCards.Count == 0
                    ? OverviewCardOrder.AsEnumerable()
                    : _financeOverview.OverviewCards.AsEnumerable())
                .Concat(OverviewCardOrder)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
                        ? (Brush)Application.Current.Resources["AccentBrush"]
                        : (Brush)Application.Current.Resources["StrokeBrush"];
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
                        Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                        Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                        Child = new TextBlock
                        {
                            Text = "⋮⋮",
                            FontSize = 12,
                            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
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
                        Foreground = (Brush)Application.Current.Resources["InkBrush"],
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
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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

            var providers = GetFinanceAccountProviders();
            var providerCombo = new ComboBox();
            foreach (var provider in providers)
            {
                providerCombo.Items.Add(new ComboBoxItem { Tag = provider.Code, Content = provider.Label });
            }

            var selectedProvider = account?.ProviderCode ?? "tbank";
            SelectComboItemByTag(providerCombo, selectedProvider);

            var cardTypeCombo = new ComboBox();
            cardTypeCombo.Items.Add(new ComboBoxItem { Tag = "debit", Content = _isRussian ? "Дебетовая" : "Debit" });
            cardTypeCombo.Items.Add(new ComboBoxItem { Tag = "credit", Content = _isRussian ? "Кредитная" : "Credit" });
            SelectComboItemByTag(cardTypeCombo, account?.CardType ?? "debit");
            var lastFourInput = new TextBox { PlaceholderText = _isRussian ? "1234" : "1234", MaxLength = 4, Text = account?.LastFourDigits ?? string.Empty };
            var amountInput = new TextBox { PlaceholderText = "0", Text = account != null ? FormatAmountInput(account.BalanceMinor) : string.Empty };
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
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
            };
            var amountSection = new StackPanel { Spacing = 6, Children = { amountLabel, amountInput } };
            var creditLimitSection = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian ? "Кредитный лимит" : "Credit limit",
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                    },
                    lastFourInput
                }
            };

            void SyncState()
            {
                var providerCode = (providerCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "tbank";
                var isCash = string.Equals(providerCode, "cash", StringComparison.OrdinalIgnoreCase);
                var isCredit = !isCash && string.Equals((cardTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string, "credit", StringComparison.OrdinalIgnoreCase);
                cardTypeSection.Visibility = isCash ? Visibility.Collapsed : Visibility.Visible;
                lastFourSection.Visibility = isCash ? Visibility.Collapsed : Visibility.Visible;
                primaryCheck.Visibility = isCash ? Visibility.Collapsed : Visibility.Visible;
                amountSection.Visibility = isCredit ? Visibility.Collapsed : Visibility.Visible;
                creditLimitSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditDebtSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditRequiredPaymentSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditPaymentDueSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                creditGraceSection.Visibility = isCredit ? Visibility.Visible : Visibility.Collapsed;
                amountLabel.Text = isCash
                    ? (_isRussian ? "Сумма наличных" : "Cash amount")
                    : (_isRussian ? "Текущий баланс" : "Current balance");
                amountInput.IsEnabled = isCredit || (account?.BalanceEditable ?? true);
                creditPaymentDuePicker.IsEnabled = creditPaymentDueCheck.IsChecked == true;
                creditGracePeriodPicker.IsEnabled = creditGracePeriodCheck.IsChecked == true;
                noteText.Text = isCredit
                    ? (_isRussian
                        ? "Кредитка учитывается как долг: покупки увеличивают задолженность, а пополнения и переводы на неё уменьшают её."
                        : "Credit cards are tracked as debt: spending increases the liability, while repayments and incoming transfers reduce it.")
                    : account != null && !account.BalanceEditable
                        ? (_isRussian
                            ? "Сумму нельзя менять после первой транзакции по счёту."
                            : "Balance is locked after the first transaction for this account.")
                        : providers.FirstOrDefault(item => string.Equals(item.Code, providerCode, StringComparison.OrdinalIgnoreCase)).Description;
                if (isCash)
                {
                    primaryCheck.IsChecked = false;
                    lastFourInput.Text = string.Empty;
                }
            }

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
                Text = _isRussian ? "Счёт / банк" : "Account / bank",
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
            });
            content.Children.Add(providerCombo);
            content.Children.Add(cardTypeSection);
            content.Children.Add(lastFourSection);
            content.Children.Add(amountSection);
            content.Children.Add(creditLimitSection);
            content.Children.Add(creditDebtSection);
            content.Children.Add(creditRequiredPaymentSection);
            content.Children.Add(creditPaymentDueSection);
            content.Children.Add(creditGraceSection);
            content.Children.Add(primaryCheck);
            content.Children.Add(noteText);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = account == null
                    ? (_isRussian ? "Новый счёт" : "New account")
                    : (_isRussian ? "Редактирование счёта" : "Edit account"),
                PrimaryButtonText = _isRussian ? "Сохранить" : "Save",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = content
            };

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                args.Cancel = true;
                var deferral = args.GetDeferral();
                dialog.IsPrimaryButtonEnabled = false;
                dialog.IsSecondaryButtonEnabled = false;
                try
                {
                    var providerCodeToSave = (providerCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "tbank";
                    var isCash = string.Equals(providerCodeToSave, "cash", StringComparison.OrdinalIgnoreCase);
                    var cardType = isCash ? null : (cardTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "debit";
                    var isCredit = !isCash && string.Equals(cardType, "credit", StringComparison.OrdinalIgnoreCase);
                    var lastFourDigits = isCash ? null : NormalizeLastFourDigits(lastFourInput.Text);
                    if (!isCash && lastFourDigits == null)
                    {
                        noteText.Text = _isRussian ? "Укажите последние 4 цифры карты." : "Enter the last 4 card digits.";
                        return;
                    }

                    long? balanceMinor = null;
                    long? creditLimitMinor = null;
                    long? creditDebtMinor = null;
                    long? creditRequiredPaymentMinor = null;
                    DateTimeOffset? creditPaymentDueDate = null;
                    DateTimeOffset? creditGracePeriodEndDate = null;

                    if (isCredit)
                    {
                        creditLimitMinor = ParseMoneyInputToMinor(creditLimitInput.Text);
                        creditDebtMinor = ParseMoneyInputToMinor(creditDebtInput.Text);
                        if (creditLimitMinor is null || creditLimitMinor <= 0 || creditDebtMinor is null)
                        {
                            noteText.Text = _isRussian
                                ? "Укажите корректные кредитный лимит и текущий долг."
                                : "Enter valid credit limit and current debt.";
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(creditRequiredPaymentInput.Text))
                        {
                            creditRequiredPaymentMinor = ParseMoneyInputToMinor(creditRequiredPaymentInput.Text);
                            if (creditRequiredPaymentMinor is null)
                            {
                                noteText.Text = _isRussian
                                    ? "Введите корректный минимальный платёж."
                                    : "Enter a valid required payment.";
                                return;
                            }
                        }

                        creditPaymentDueDate = creditPaymentDueCheck.IsChecked == true ? creditPaymentDuePicker.Date : null;
                        creditGracePeriodEndDate = creditGracePeriodCheck.IsChecked == true ? creditGracePeriodPicker.Date : null;
                    }
                    else
                    {
                        balanceMinor = ParseMoneyInputToMinor(amountInput.Text);
                        if (balanceMinor == null)
                        {
                            noteText.Text = _isRussian ? "Введите корректную сумму." : "Enter a valid amount.";
                            return;
                        }
                    }

                    var accessToken = await GetFreshFinanceAccessTokenAsync();
                    _financeOverview = await _financeClient.UpsertAccountAsync(
                        accessToken,
                        account?.Id,
                        providerCodeToSave,
                        cardType,
                        lastFourDigits,
                        balanceMinor ?? 0,
                        _financeOverview.DefaultCurrency ?? "RUB",
                        !isCash && primaryCheck.IsChecked == true,
                        creditLimitMinor,
                        creditDebtMinor,
                        creditRequiredPaymentMinor,
                        creditPaymentDueDate,
                        creditGracePeriodEndDate);
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
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
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
            var progressValueText = new TextBlock
            {
                Text = "8%",
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                FontSize = 12
            };
            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 8,
                Height = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var stageText = new TextBlock
            {
                Text = _isRussian
                    ? "Подготавливаем документ и отправляем его на распознавание."
                    : "Preparing the document and sending it for recognition.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["InkBrush"],
                FontSize = 14
            };

            var statusText = new TextBlock
            {
                Text = _isRussian
                    ? "Ничего делать не нужно. Мы постараемся автоматически заполнить максимум полей."
                    : "You don't need to do anything. We'll try to prefill as many fields as possible.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                FontSize = 12
            };

            var iconAccent = (Brush)Application.Current.Resources["AccentBrush"];
            var progressHeaderGrid = new Grid();
            progressHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            progressHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var progressIconTile = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(44, 0, 204, 136)),
                Child = new FontIcon
                {
                    Glyph = sourceType == "photo" ? "\uE722" : "\uE8B7",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = iconAccent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var progressHeaderText = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(12, 0, 0, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = _isRussian
                            ? "Готовим черновик транзакции"
                            : "Preparing the transaction draft",
                        FontSize = 16,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources["InkBrush"]
                    },
                    new TextBlock
                    {
                        Text = _isRussian
                            ? "Плавно извлекаем данные из документа и собираем форму."
                            : "Extracting data from the document and assembling the form.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                        FontSize = 12
                    }
                }
            };
            progressHeaderGrid.Children.Add(progressIconTile);
            Grid.SetColumn(progressHeaderText, 1);
            progressHeaderGrid.Children.Add(progressHeaderText);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian
                    ? (sourceType == "photo" ? "Обрабатываем фото" : "Обрабатываем файл")
                    : (sourceType == "photo" ? "Processing photo" : "Processing file"),
                Content = new StackPanel
                {
                    Width = 420,
                    Spacing = 14,
                    Children =
                    {
                        new Border
                        {
                            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                            BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(22),
                            Padding = new Thickness(18),
                            Child = new StackPanel
                            {
                                Spacing = 14,
                                Children =
                                {
                                    progressHeaderGrid,
                                    stageText,
                                    progressBar,
                                    progressValueText,
                                    statusText
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
                        ? "Почти готово. Подготавливаем форму транзакции."
                        : "Almost there. Preparing the transaction form.";
                    statusText.Text = _isRussian
                        ? "Ещё мгновение, и откроется готовая форма."
                        : "One more moment and the prepared form will appear.";
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
            var current = progressBar.Value;
            while (!isComplete())
            {
                current = current switch
                {
                    < 36 => current + 7,
                    < 72 => current + 4,
                    < 88 => current + 1.6,
                    _ => current
                };

                progressBar.Value = Math.Min(current, 88);
                progressValueText.Text = $"{Math.Round(progressBar.Value):0}%";
                stageText.Text = progressBar.Value switch
                {
                    < 32 => _isRussian
                        ? "Подготавливаем файл и выделяем полезные фрагменты."
                        : "Preparing the file and isolating useful fragments.",
                    < 62 => _isRussian
                        ? "Извлекаем сумму, дату, магазин и возможные позиции."
                        : "Extracting the amount, date, merchant, and possible items.",
                    _ => _isRussian
                        ? "Собираем аккуратный черновик, чтобы вы могли быстро проверить поля."
                        : "Building a clean draft so you can review the fields quickly."
                };
                await Task.Delay(90);
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
                    ? (Brush)Application.Current.Resources["AccentBrush"]
                    : (Brush)Application.Current.Resources["MutedTextBrush"];
                var normalBackground = (Brush)Application.Current.Resources["CardBackgroundBrush"];
                var hoverBackground = (Brush)Application.Current.Resources["PageBackgroundBrush"];
                var pressedBackground = new SolidColorBrush(Color.FromArgb(210, 58, 58, 64));
                var normalBorder = (Brush)Application.Current.Resources["StrokeBrush"];
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
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
                content.Children.Add(new TextBlock
                {
                    Text = body,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                Foreground = (Brush)Application.Current.Resources["ErrorTextBrush"],
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
                Background = (Brush)Application.Current.Resources["PanelStrongBrush"],
                Padding = new Thickness(0),
                Child = previewElement
            });
            content.Children.Add(new TextBlock
            {
                Text = _isRussian
                    ? "Наведите камеру на чек и нажмите кнопку снимка."
                    : "Point the camera at the receipt and press capture.",
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
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

            if (_isRussian)
            {
                return "Не удалось обработать документ. Проверьте файл и попробуйте снова.";
            }

            return message;
        }

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
                Background = (Brush)Application.Current.Resources["PanelStrongBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
            };
            var currentSelectionPath = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["InkBrush"],
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            var currentSelectionCard = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
            };
            var sectionTitle = new TextBlock
            {
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
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
                    (Brush)Application.Current.Resources["InkBrush"],
                    (Brush)Application.Current.Resources["AccentBrush"]);
                var subtitle = fromSearch
                    ? CreateHighlightedTextBlock(
                        subtitleText,
                        currentQuery,
                        13,
                        Microsoft.UI.Text.FontWeights.Normal,
                        (Brush)Application.Current.Resources["MutedTextBrush"],
                        (Brush)Application.Current.Resources["AccentBrush"])
                    : CreateMutedText(subtitleText);
                var indicator = new FontIcon
                {
                    Glyph = fromSearch || !hasChildren ? "\uE73E" : "\uE76C",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 14,
                    Foreground = isCurrentSelection
                        ? (Brush)Application.Current.Resources["AccentBrush"]
                        : (Brush)Application.Current.Resources["MutedTextBrush"],
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(indicator, 1);

                var normalBackground = isCurrentSelection
                    ? (Brush)Application.Current.Resources["PillBackgroundBrush"]
                    : (Brush)Application.Current.Resources["CardBackgroundBrush"];
                var hoverBackground = isCurrentSelection
                    ? new SolidColorBrush(Color.FromArgb(255, 52, 61, 56))
                    : (Brush)Application.Current.Resources["PageBackgroundBrush"];
                var pressedBackground = new SolidColorBrush(Color.FromArgb(255, 58, 58, 64));
                var normalBorder = isCurrentSelection
                    ? (Brush)Application.Current.Resources["AccentBrush"]
                    : (Brush)Application.Current.Resources["StrokeBrush"];
                var hoverBorder = isCurrentSelection
                    ? (Brush)Application.Current.Resources["AccentBrush"]
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
                button.Resources["ButtonForeground"] = (Brush)Application.Current.Resources["InkBrush"];
                button.Resources["ButtonForegroundPointerOver"] = (Brush)Application.Current.Resources["InkBrush"];
                button.Resources["ButtonForegroundPressed"] = (Brush)Application.Current.Resources["InkBrush"];

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
                            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(20),
                Child = itemsSection
            };
            var basicSectionCard = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
            });

            var stateText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
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
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
                foreach (var warning in warnings)
                {
                    warningsPanel.Children.Add(new TextBlock
                    {
                        Text = warning,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                        FontSize = 12
                    });
                }

                root.Children.Add(new Border
                {
                    Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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

                foreach (var account in _financeOverview?.Accounts ?? Enumerable.Empty<FinanceAccount>())
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
                        Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                        Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                        };
                        itemStack.Children.Add(categoryHeader);

                        var categoryTitle = new TextBlock
                        {
                            Text = GetFinanceCategoryLabel(draft.Direction, item.CategoryId),
                            FontSize = 14,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
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
                                Foreground = (Brush)Application.Current.Resources["InkBrush"]
                            },
                            new TextBlock
                            {
                                Text = _isRussian ? "Добавить позицию" : "Add item",
                                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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

        private long GetOverviewCardMetric(FinanceOverview overview, string cardId) => cardId switch
        {
            "total_balance" => Math.Abs(overview.TotalBalanceMinor),
            "card_balance" => Math.Abs(overview.CardBalanceMinor),
            "cash_balance" => Math.Abs(overview.CashBalanceMinor),
            "credit_debt" => Math.Abs(overview.CreditDebtMinor),
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
                    Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
                titleStack.Children.Add(CreateMutedText(GetFinanceAccountSubtitle(account)));
                Grid.SetColumn(titleStack, 1);
                header.Children.Add(titleStack);

                var editButton = CreateFinanceMiniButton(_isRussian ? "Изменить" : "Edit");
                editButton.Click += async (_, _) => await ShowAccountDialogAsync(account);
                Grid.SetColumn(editButton, 2);
                header.Children.Add(editButton);

                primaryStack.Children.Add(header);
                var badges = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };
                badges.Children.Add(CreateFinanceBadge(
                    account.Kind == "cash"
                        ? (_isRussian ? "Наличные" : "Cash")
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
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                });
                balanceBlock.Children.Add(new TextBlock
                {
                    Text = GetFinanceAccountPrimaryAmount(account),
                    FontSize = 24,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
                balanceBlock.Children.Add(new TextBlock
                {
                    Text = account.Currency,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                    Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                });
                filterLabelStack.Children.Add(new TextBlock
                {
                    Text = FormatFinanceMonthLabel(selectedMonth),
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                    Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
                titleStack.Children.Add(new TextBlock
                {
                    Text = GetTransactionSecondaryLine(transaction),
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
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
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
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
                        Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(12, 10, 12, 10),
                        Child = new TextBlock
                        {
                            Text = transaction.Note,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                        }
                    });
                }

                if (transaction.ItemCount > 0 || transaction.Items.Count > 0)
                {
                    positionsPanel = new Border
                    {
                        Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
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
                                Foreground = (Brush)Application.Current.Resources["InkBrush"]
                            });
                            if (!string.IsNullOrWhiteSpace(item.CategoryName))
                            {
                                itemTextStack.Children.Add(new TextBlock
                                {
                                    Text = item.CategoryName,
                                    FontSize = 12,
                                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                                });
                            }
                            itemGrid.Children.Add(itemTextStack);

                            var itemAmount = new TextBlock
                            {
                                Text = FormatMoney(item.AmountMinor, transaction.Currency),
                                FontSize = 13,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                Foreground = (Brush)Application.Current.Resources["InkBrush"],
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

        private void RenderSettingsContent()
        {
            var profileVisible = _section == DashboardSection.Settings &&
                string.Equals(_activeSubsection, "profile", StringComparison.OrdinalIgnoreCase);
            var preferencesVisible = _section == DashboardSection.Settings &&
                string.Equals(_activeSubsection, "preferences", StringComparison.OrdinalIgnoreCase);
            var securityVisible = _section == DashboardSection.Settings &&
                string.Equals(_activeSubsection, "security", StringComparison.OrdinalIgnoreCase);

            SettingsProfileCard.Visibility = profileVisible ? Visibility.Visible : Visibility.Collapsed;
            SettingsPreferencesCard.Visibility = preferencesVisible ? Visibility.Visible : Visibility.Collapsed;
            SettingsSecurityCard.Visibility = securityVisible ? Visibility.Visible : Visibility.Collapsed;

            SettingsStatusBar.Severity = _settingsBannerIsError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            SettingsStatusBar.Title = _settingsBannerIsError
                ? (_isRussian ? "Проблема с настройками" : "Settings issue")
                : (_isRussian ? "Настройки обновлены" : "Settings updated");
            SettingsStatusBar.Message = _settingsBanner ?? string.Empty;
            SettingsLoadingText.Visibility = _settingsLoading ? Visibility.Visible : Visibility.Collapsed;

            var hasGoogle = _settingsSnapshot?.Identities.Any(identity =>
                string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase)) == true;
            SettingsGoogleBody.Text = hasGoogle
                ? (_isRussian ? "Google уже привязан к вашему аккаунту." : "Google is already linked to your account.")
                : (_isRussian ? "Подключите Google к текущему аккаунту." : "Connect Google to the current account.");

            var googleBusy = _settingsBusyAction is "google_link" or "google_unlink";
            SettingsGoogleButton.Content = googleBusy
                ? (_isRussian ? "Переходим..." : "Opening...")
                : (hasGoogle
                    ? (_isRussian ? "Отключить Google" : "Disconnect Google")
                    : (_isRussian ? "Подключить Google" : "Connect Google"));

            ApplySettingsBusyState();
        }

        private void ApplySettingsBusyState()
        {
            var busy = _settingsLoading || !string.IsNullOrWhiteSpace(_settingsBusyAction);
            var hasSavedGeminiKey = _settingsSnapshot?.HasGeminiApiKey == true;
            SettingsDisplayNameInput.IsEnabled = !busy;
            SettingsEmailInput.IsEnabled = !busy;
            SettingsGeminiInput.IsEnabled = !busy;
            SettingsAiEnhancementsToggle.IsEnabled = !busy && hasSavedGeminiKey;
            SettingsPasswordInput.IsEnabled = !busy;
            SettingsPasswordConfirmInput.IsEnabled = !busy;
            SettingsDisplayNameButton.IsEnabled = !busy;
            SettingsEmailButton.IsEnabled = !busy;
            SettingsGeminiButton.IsEnabled = !busy;
            SettingsGeminiClearButton.IsEnabled = !busy && hasSavedGeminiKey;
            SettingsGeminiLinkButton.IsEnabled = !busy;
            SettingsPasswordButton.IsEnabled = !busy;
            SettingsGoogleButton.IsEnabled = !busy;
            SettingsLogoutButton.IsEnabled = !busy;
            SettingsFinanceResetButton.IsEnabled = !busy;
            SettingsDeleteButton.IsEnabled = !busy;
        }

        private void ApplySettingsGeminiPresentation()
        {
            var hasSavedGeminiKey = _settingsSnapshot?.HasGeminiApiKey == true;
            SettingsGeminiInput.PlaceholderText = _isRussian
                ? "Вставьте Gemini API key"
                : "Paste your Gemini API key";

            SettingsGeminiStateText.Text = hasSavedGeminiKey
                ? (_isRussian
                    ? "Сохранённый ключ загружен. Можно заменить его и снова нажать «Сохранить»."
                    : "The saved key is loaded. Replace it and click Save again if needed.")
                : (_isRussian
                    ? "Ключ ещё не сохранён."
                    : "No Gemini key has been saved yet.");

            SettingsGeminiClearButton.Visibility = hasSavedGeminiKey ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetSettingsBanner(string? message, bool isError, bool persist = false)
        {
            _settingsBanner = message;
            _settingsBannerIsError = isError;
            RenderSettingsContent();
            _ = ShowAnimatedInfoBarAsync(
                SettingsStatusBar,
                message,
                isError,
                false,
                persist,
                _isRussian
                    ? (_settingsBannerIsError ? "Проблема с настройками" : "Настройки обновлены")
                    : (_settingsBannerIsError ? "Settings issue" : "Settings updated"));
        }

        private async Task LoadSettingsAsync(bool clearBanner = true)
        {
            if (_settingsClient == null || !HasSession() || _session == null || _section != DashboardSection.Settings)
            {
                return;
            }

            var userId = ExtractUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                SetSettingsBanner(
                    _isRussian ? "Не удалось определить пользователя для настроек." : "Failed to resolve the current user for settings.",
                    true);
                return;
            }

            try
            {
                _settingsLoading = true;
                if (clearBanner)
                {
                    SetSettingsBanner(null, false);
                }
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();

                var snapshot = await _settingsClient.GetSnapshotAsync(
                    accessToken,
                    userId,
                    ExtractEmail());

                var cachedDisplayName = _displayNameStore.Load(userId);
                if (string.IsNullOrWhiteSpace(snapshot.DisplayName) && !string.IsNullOrWhiteSpace(_settingsSnapshot?.DisplayName))
                {
                    snapshot.DisplayName = _settingsSnapshot.DisplayName;
                }
                if (string.IsNullOrWhiteSpace(snapshot.DisplayName) && !string.IsNullOrWhiteSpace(cachedDisplayName))
                {
                    snapshot.DisplayName = cachedDisplayName;
                }

                var cachedGeminiSettings = _geminiSettingsStore.Load(userId);
                if (!snapshot.HasGeminiApiKey && cachedGeminiSettings is { } cached && !string.IsNullOrWhiteSpace(cached.GeminiApiKey))
                {
                    snapshot.GeminiApiKey = cached.GeminiApiKey;
                    snapshot.AiEnhancementsEnabled = cached.AiEnhancementsEnabled;
                }

                snapshot.Identities = BuildLinkedIdentityFallbacksFromSession();
                try
                {
                    snapshot.Identities = await LoadLinkedIdentitiesAsync();
                }
                catch
                {
                    // Keep the fallback provider list from the current session so the UI remains stable
                    // even when the identities RPC is temporarily unavailable.
                }

                _settingsSnapshot = snapshot;
                if (!string.IsNullOrWhiteSpace(_settingsSnapshot.DisplayName))
                {
                    _displayNameStore.Save(userId, _settingsSnapshot.DisplayName);
                }
                if (_settingsSnapshot.HasGeminiApiKey)
                {
                    _geminiSettingsStore.Save(_settingsSnapshot.UserId, _settingsSnapshot.GeminiApiKey, _settingsSnapshot.AiEnhancementsEnabled);
                }
                SettingsDisplayNameInput.Text = snapshot.DisplayName;
                SettingsEmailInput.Text = snapshot.Email;
                SettingsGeminiInput.Password = snapshot.GeminiApiKey;
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = snapshot.HasGeminiApiKey && snapshot.AiEnhancementsEnabled;
                _suppressSettingsAiToggleSave = false;
                SettingsPasswordInput.Password = string.Empty;
                SettingsPasswordConfirmInput.Password = string.Empty;
                ApplySettingsGeminiPresentation();
            }
            catch (Exception ex)
            {
                _settingsSnapshot = CreateFallbackSettingsSnapshot(userId);
                var cachedDisplayName = _displayNameStore.Load(userId);
                if (string.IsNullOrWhiteSpace(_settingsSnapshot.DisplayName) && !string.IsNullOrWhiteSpace(cachedDisplayName))
                {
                    _settingsSnapshot.DisplayName = cachedDisplayName;
                }
                var cachedGeminiSettings = _geminiSettingsStore.Load(userId);
                if (cachedGeminiSettings is { } cached && !string.IsNullOrWhiteSpace(cached.GeminiApiKey))
                {
                    _settingsSnapshot.GeminiApiKey = cached.GeminiApiKey;
                    _settingsSnapshot.AiEnhancementsEnabled = cached.AiEnhancementsEnabled;
                }
                SettingsDisplayNameInput.Text = _settingsSnapshot.DisplayName;
                SettingsEmailInput.Text = _settingsSnapshot.Email;
                SettingsGeminiInput.Password = _settingsSnapshot.GeminiApiKey;
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = _settingsSnapshot.HasGeminiApiKey && _settingsSnapshot.AiEnhancementsEnabled;
                _suppressSettingsAiToggleSave = false;
                SettingsPasswordInput.Password = string.Empty;
                SettingsPasswordConfirmInput.Password = string.Empty;
                if (_settingsSnapshot.Identities.Count == 0)
                {
                    _settingsSnapshot.Identities = BuildLinkedIdentityFallbacksFromSession();
                }
                if (clearBanner)
                {
                    var hasVisibleData =
                        !string.IsNullOrWhiteSpace(_settingsSnapshot.Email) ||
                        !string.IsNullOrWhiteSpace(_settingsSnapshot.DisplayName) ||
                        _settingsSnapshot.HasGeminiApiKey ||
                        _settingsSnapshot.Identities.Count > 0;

                    if (hasVisibleData)
                    {
                        SetSettingsBanner(null, false);
                    }
                    else
                    {
                        SetSettingsBanner(LocalizeSettingsLoadError(ex.Message), true);
                    }
                }
                ApplySettingsGeminiPresentation();
            }
            finally
            {
                _settingsLoading = false;
                RenderSettingsContent();
            }
        }

        private SettingsSnapshot CreateFallbackSettingsSnapshot(string userId)
        {
            return new SettingsSnapshot
            {
                UserId = userId,
                Email = ExtractEmail(),
                DisplayName = string.Empty,
                GeminiApiKey = _settingsSnapshot?.GeminiApiKey ?? string.Empty,
                AiEnhancementsEnabled = (_settingsSnapshot?.HasGeminiApiKey ?? false) &&
                                        (_settingsSnapshot?.AiEnhancementsEnabled ?? false),
                Identities = _settingsSnapshot?.Identities ?? new List<LinkedIdentity>()
            };
        }

        private async Task SaveDisplayNameAsync()
        {
            if (_settingsClient == null || _session == null || _settingsSnapshot == null)
            {
                return;
            }

            var nextName = SettingsDisplayNameInput.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextName))
            {
                SetSettingsBanner(_isRussian ? "Имя не может быть пустым." : "Name cannot be empty.", true);
                return;
            }

            try
            {
                _settingsBusyAction = "display_name";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _settingsClient.SaveDisplayNameAsync(accessToken, _settingsSnapshot.UserId, ExtractEmail(), nextName);
                _settingsSnapshot.DisplayName = nextName;
                _displayNameStore.Save(_settingsSnapshot.UserId, nextName);
                SetSettingsBanner(_isRussian ? "Имя сохранено." : "Name saved.", false);
                DashboardWelcomeTitle.Text = _isRussian
                    ? $"{SectionLabel(_section)}, {ExtractDisplayName()}."
                    : $"{SectionLabel(_section)}, {ExtractDisplayName()}.";
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось сохранить имя." : "Failed to save name."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task SaveEmailAsync()
        {
            if (_session == null)
            {
                return;
            }

            var nextEmail = SettingsEmailInput.Text?.Trim() ?? string.Empty;
            var validation = ValidateEmail(nextEmail);
            if (validation != null)
            {
                SetSettingsBanner(validation, true);
                return;
            }

            try
            {
                _settingsBusyAction = "email";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _authClient.UpdateEmailAsync(accessToken, nextEmail);
                if (_settingsSnapshot != null)
                {
                    _settingsSnapshot.Email = nextEmail;
                }
                SetSettingsBanner(
                    _isRussian
                        ? "Запрос на смену почты отправлен. Подтвердите новую почту, если Supabase запросит подтверждение."
                        : "Email change requested. Confirm the new email if Supabase asks for verification.",
                    false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось изменить почту." : "Failed to change email."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task SaveGeminiApiKeyAsync()
        {
            if (_settingsClient == null || _session == null || _settingsSnapshot == null)
            {
                return;
            }

            try
            {
                _settingsBusyAction = "gemini";
                RenderSettingsContent();
                await PersistGeminiSettingsAsync(
                    preserveExistingKeyWhenInputBlank: true,
                    successMessage: _isRussian ? "Настройки Gemini сохранены." : "Gemini settings saved.");
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось сохранить настройки Gemini." : "Failed to save Gemini settings."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task PersistGeminiSettingsAsync(bool preserveExistingKeyWhenInputBlank, string successMessage)
        {
            if (_settingsClient == null || _settingsSnapshot == null)
            {
                return;
            }

            var accessToken = await GetFreshAccessTokenAsync();
            var enteredGeminiApiKey = SettingsGeminiInput.Password?.Trim() ?? string.Empty;
            var geminiApiKey = !string.IsNullOrWhiteSpace(enteredGeminiApiKey)
                ? enteredGeminiApiKey
                : preserveExistingKeyWhenInputBlank
                    ? _settingsSnapshot.GeminiApiKey
                    : string.Empty;

            var aiEnhancementsEnabled = !string.IsNullOrWhiteSpace(geminiApiKey) && SettingsAiEnhancementsToggle.IsOn;
            await _settingsClient.SaveGeminiSettingsAsync(
                accessToken,
                _settingsSnapshot.UserId,
                geminiApiKey,
                aiEnhancementsEnabled);

            _settingsSnapshot.GeminiApiKey = geminiApiKey;
            _settingsSnapshot.AiEnhancementsEnabled = aiEnhancementsEnabled;
            _geminiSettingsStore.Save(_settingsSnapshot.UserId, geminiApiKey, aiEnhancementsEnabled);
            SettingsGeminiInput.Password = geminiApiKey;
            _suppressSettingsAiToggleSave = true;
            SettingsAiEnhancementsToggle.IsOn = _settingsSnapshot.HasGeminiApiKey && _settingsSnapshot.AiEnhancementsEnabled;
            _suppressSettingsAiToggleSave = false;
            ApplySettingsGeminiPresentation();
            SetSettingsBanner(successMessage, false);
        }

        private void SettingsGeminiInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ApplySettingsGeminiPresentation();
            ApplySettingsBusyState();
        }

        private async void SettingsAiEnhancementsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingsAiToggleSave || _settingsLoading || _settingsBusyAction != null || _settingsSnapshot?.HasGeminiApiKey != true)
            {
                return;
            }

            try
            {
                _settingsBusyAction = "gemini_toggle";
                RenderSettingsContent();
                await PersistGeminiSettingsAsync(
                    preserveExistingKeyWhenInputBlank: true,
                    successMessage: SettingsAiEnhancementsToggle.IsOn
                        ? (_isRussian ? "AI-улучшения включены." : "AI enhancements enabled.")
                        : (_isRussian ? "AI-улучшения выключены." : "AI enhancements disabled."));
            }
            catch (Exception ex)
            {
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = _settingsSnapshot.AiEnhancementsEnabled;
                _suppressSettingsAiToggleSave = false;
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось обновить AI-улучшения." : "Failed to update AI enhancements."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async void SettingsGeminiClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsSnapshot?.HasGeminiApiKey != true || _settingsClient == null || _session == null)
            {
                return;
            }

            try
            {
                _settingsBusyAction = "gemini_clear";
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = false;
                _suppressSettingsAiToggleSave = false;
                RenderSettingsContent();
                SettingsGeminiInput.Password = string.Empty;
                await PersistGeminiSettingsAsync(
                    preserveExistingKeyWhenInputBlank: false,
                    successMessage: _isRussian ? "Ключ Gemini удалён." : "Gemini key deleted.");
                _geminiSettingsStore.Clear(_settingsSnapshot.UserId);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось удалить ключ Gemini." : "Failed to delete Gemini key."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task SavePasswordAsync()
        {
            if (_session == null)
            {
                return;
            }

            var nextPassword = SettingsPasswordInput.Password ?? string.Empty;
            var confirmPassword = SettingsPasswordConfirmInput.Password ?? string.Empty;
            var validation = ValidatePasswordForReset(ExtractEmail(), nextPassword, confirmPassword);
            if (validation != null)
            {
                SetSettingsBanner(validation, true);
                return;
            }

            try
            {
                _settingsBusyAction = "password";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _authClient.UpdatePasswordAsync(accessToken, nextPassword);
                SettingsPasswordInput.Password = string.Empty;
                SettingsPasswordConfirmInput.Password = string.Empty;
                SetSettingsBanner(_isRussian ? "Пароль обновлён." : "Password updated.", false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось обновить пароль." : "Failed to update password."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task ToggleGoogleIdentityAsync()
        {
            if (_session == null || _settingsClient == null)
            {
                return;
            }

            var googleIdentity = _settingsSnapshot?.Identities.FirstOrDefault(identity =>
                string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase));
            if (googleIdentity == null)
            {
                try
                {
                    var previousSession = CloneSession(_session);
                    var previousUserId = ExtractUserId();
                    _settingsBusyAction = "google_link";
                    RenderSettingsContent();
                    var verifier = PkceUtil.CreateCodeVerifier();
                    var challenge = PkceUtil.CreateCodeChallenge(verifier);
                    PkceStore.Save(verifier);
                    using var listener = new LoopbackOAuthListener(
                        AppConfig.GoogleLoopbackRedirectUri,
                        _isRussian,
                        "assistant://auth/return?source=google-link");
                    var url = _authClient.BuildGoogleAuthorizeUrl(
                        AppConfig.GoogleLoopbackRedirectUri,
                        challenge,
                        forceAccountSelection: true);
                    await Launcher.LaunchUriAsync(new Uri(url));
                    var callbackUri = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(2));
                    BringWindowToFront();
                    var query = QueryHelpers.Parse(callbackUri);
                    if (query.TryGetValue("error", out var error))
                    {
                        if (query.TryGetValue("error_description", out var description) && !string.IsNullOrWhiteSpace(description))
                        {
                            throw new InvalidOperationException(description);
                        }

                        throw new InvalidOperationException(error);
                    }

                    await CompleteGoogleLinkSignInAsync(callbackUri);
                    var currentUserId = ExtractUserId();
                    if (!string.Equals(previousUserId, currentUserId, StringComparison.OrdinalIgnoreCase))
                    {
                        _session = previousSession;
                        PersistSession();
                        await LoadSettingsAsync(clearBanner: false);
                        throw new InvalidOperationException(
                            _isRussian
                                ? "Вы вошли через другой Google-аккаунт. Для привязки используйте Google с тем же email, что и у текущего профиля."
                                : "You signed in with a different Google account. Use the Google account with the same email as the current profile.");
                    }

                    var identities = await WaitForLinkedIdentitiesAsync();
                    _settingsSnapshot ??= CreateFallbackSettingsSnapshot(currentUserId);
                    _settingsSnapshot.Identities = identities;
                    await LoadSettingsAsync(clearBanner: false);
                    RenderSettingsContent();

                    var linkedGoogleIdentity = identities.FirstOrDefault(identity =>
                        string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase));
                    if (linkedGoogleIdentity == null)
                    {
                        var providers = identities
                            .Select(identity => identity.Provider)
                            .Where(provider => !string.IsNullOrWhiteSpace(provider))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var providersText = providers.Count == 0
                            ? (_isRussian ? "identity не найдены" : "no identities found")
                            : string.Join(", ", providers);
                        throw new InvalidOperationException(
                            _isRussian
                                ? $"Google вход завершился, но identity не появилась в профиле. Текущие identity: {providersText}."
                                : $"Google sign-in completed, but the identity did not appear in the profile. Current identities: {providersText}.");
                    }

                    SetSettingsBanner(_isRussian ? "Google аккаунт подключён." : "Google account linked.", false);
                }
                catch (Exception ex)
                {
                    SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось привязать Google." : "Failed to link Google."), true);
                }
                finally
                {
                    _settingsBusyAction = null;
                    RenderSettingsContent();
                }

                return;
            }

            if ((_settingsSnapshot?.Identities.Count ?? 0) < 2)
            {
                SetSettingsBanner(
                    _isRussian
                        ? "Нельзя отвязать единственный способ входа. Сначала задайте пароль или привяжите другую identity."
                        : "You cannot unlink the only sign-in method. Set a password or link another identity first.",
                    true);
                return;
            }

            try
            {
                _settingsBusyAction = "google_unlink";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _settingsClient.UnlinkGoogleIdentityAsync(accessToken);
                if (_settingsSnapshot != null)
                {
                    _settingsSnapshot.Identities = _settingsSnapshot.Identities
                        .Where(identity => !string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                RenderSettingsContent();
                await LoadSettingsAsync(clearBanner: false);
                SetSettingsBanner(_isRussian ? "Google аккаунт отключён." : "Google account disconnected.", false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось отвязать Google." : "Failed to unlink Google."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private static AuthSession CloneSession(AuthSession session) => new()
        {
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            TokenType = session.TokenType,
            ExpiresIn = session.ExpiresIn,
            ExpiresAt = session.ExpiresAt,
            UserId = session.UserId,
            UserEmail = session.UserEmail
        };

        private async Task<List<LinkedIdentity>> WaitForLinkedIdentitiesAsync()
        {
            if (_settingsClient == null || _session == null)
            {
                return new List<LinkedIdentity>();
            }

            List<LinkedIdentity> latest = new();
            for (var attempt = 0; attempt < 8; attempt++)
            {
                latest = await LoadLinkedIdentitiesAsync();
                if (latest.Any(identity => string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase)))
                {
                    return latest;
                }

                await Task.Delay(350);
            }

            return latest;
        }

        private async Task CompleteGoogleLinkSignInAsync(Uri callbackUri)
        {
            var query = QueryHelpers.Parse(callbackUri);
            if (query.TryGetValue("error", out var error))
            {
                if (query.TryGetValue("error_description", out var description) && !string.IsNullOrWhiteSpace(description))
                {
                    throw new InvalidOperationException(description);
                }

                throw new InvalidOperationException(error);
            }

            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException(_isRussian ? "Код авторизации Google не получен." : "Google authorization code is missing.");
            }

            var verifier = PkceStore.Load();
            if (string.IsNullOrWhiteSpace(verifier))
            {
                throw new InvalidOperationException(_isRussian ? "Нет PKCE verifier." : "Missing PKCE verifier.");
            }

            _session = await _authClient.ExchangeCodeForSessionAsync(code, verifier);
            PersistSession();
            PkceStore.Clear();
        }

        private async Task<List<LinkedIdentity>> LoadLinkedIdentitiesAsync()
        {
            if (_settingsClient == null)
            {
                return new List<LinkedIdentity>();
            }

            Exception? lastError = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var accessToken = await GetFreshAccessTokenAsync();
                    return await _settingsClient.GetLinkedIdentitiesAsync(accessToken);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < 4)
                    {
                        await Task.Delay(250);
                    }
                }
            }

            throw lastError ?? new InvalidOperationException(_isRussian ? "Не удалось загрузить связанные identity." : "Failed to load linked identities.");
        }

        private List<LinkedIdentity> BuildLinkedIdentityFallbacksFromSession()
        {
            var providers = ExtractSessionProviders();
            return providers
                .Select(provider => new LinkedIdentity
                {
                    Provider = provider
                })
                .ToList();
        }

        private void BringWindowToFront()
        {
            try
            {
                Activate();
                var hWnd = WindowNative.GetWindowHandle(this);
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }

                if (AppWindow?.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized)
                {
                    ShowWindow(hWnd, SwRestore);
                }
                else
                {
                    ShowWindow(hWnd, SwShow);
                }
                SetForegroundWindow(hWnd);
            }
            catch
            {
                // ignore focus errors
            }
        }

        private async Task ConfirmDeleteAccountAsync()
        {
            if (_settingsClient == null || _session == null)
            {
                return;
            }

            var input = new TextBox
            {
                PlaceholderText = _isRussian ? "Введите: удалить" : "Type: delete"
            };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian ? "Подтвердите удаление аккаунта" : "Confirm account deletion",
                PrimaryButtonText = _isRussian ? "Удалить аккаунт" : "Delete account",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Close,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = _isRussian
                                ? "Чтобы подтвердить удаление, введите слово «удалить». После этого аккаунт будет удалён без возможности восстановления."
                                : "Type “delete” to confirm. The account will be removed permanently without recovery.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        input
                    }
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var expected = _isRussian ? "удалить" : "delete";
            if (!string.Equals(input.Text?.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                SetSettingsBanner(
                    _isRussian ? "Введите слово «удалить» для подтверждения." : "Type “delete” to confirm.",
                    true);
                return;
            }

            try
            {
                _settingsBusyAction = "delete";
                RenderSettingsContent();
                await _settingsClient.DeleteAccountAsync(_session.AccessToken);
                _sessionStore.Clear();
                _sessionStore.SetRememberDevice(RememberCheck.IsChecked == true);
                _session = null;
                _settingsSnapshot = null;
                _settingsBanner = null;
                _mode = AuthMode.Login;
                ApplyMode();
                UpdateShellState();
                SetStatus(_isRussian ? "Аккаунт удалён." : "Account deleted.", false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось удалить аккаунт." : "Failed to delete account."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task ConfirmFinanceResetAsync()
        {
            if (_financeClient == null || _session == null)
            {
                return;
            }

            var input = new TextBox
            {
                PlaceholderText = _isRussian ? "Введите: сбросить" : "Type: reset"
            };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian ? "Подтвердите сброс финансов" : "Confirm finance reset",
                PrimaryButtonText = _isRussian ? "Сбросить финансы" : "Reset finance",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Close,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = _isRussian
                                ? "Чтобы подтвердить сброс, введите слово «сбросить». Все счета, транзакции, категории и настройки обзора будут удалены. После этого финансы начнутся с чистого листа."
                                : "Type “reset” to confirm. All accounts, transactions, categories, and overview settings will be deleted. Finance will start again from a clean state.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        input
                    }
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var expected = _isRussian ? "сбросить" : "reset";
            if (!string.Equals(input.Text?.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                SetSettingsBanner(
                    _isRussian ? "Введите слово «сбросить» для подтверждения." : "Type “reset” to confirm.",
                    true);
                return;
            }

            try
            {
                _settingsBusyAction = "finance_reset";
                RenderSettingsContent();
                await _financeClient.ResetAllAsync(_session.AccessToken);
                ResetFinanceState();
                SetSettingsBanner(
                    _isRussian ? "Финансы сброшены. Раздел можно заполнить заново." : "Finance reset complete. You can set up the workspace again.",
                    false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(
                    LocalizeAuthError(ex.Message, _isRussian ? "Не удалось сбросить финансы." : "Failed to reset finance."),
                    true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private void ResetFinanceState()
        {
            _financeOnboardingStep = 0;
            _financeOverview = null;
            _financeTransactionsMonth = null;
            _financeCategories.Clear();
            _financeSelectedTransactionsMonth = null;
            _financeTransactionsMonthLoading = false;
            _financeMonthCache.Clear();
            _financeAnalytics = null;
            _financeAnalyticsFromMonth = null;
            _financeAnalyticsToMonth = null;
            _financeAnalyticsPreset = "current";
            _financeAnalyticsLoading = false;
            FinanceErrorCard.Visibility = Visibility.Collapsed;
            FinanceLoadingCard.Visibility = Visibility.Collapsed;
            FinanceCurrencyCombo.SelectedIndex = -1;
            FinanceBankCombo.SelectedIndex = -1;
            FinanceOnboardingCardTypeCombo.SelectedIndex = -1;
            FinanceOnboardingLastFourInput.Text = string.Empty;
            FinancePrimaryBalanceInput.Text = string.Empty;
            FinanceCashInput.Text = string.Empty;

            if (_section == DashboardSection.Finance)
            {
                _financeTab = FinanceTab.Overview;
                _activeSubsection = "overview";
                ApplySecondaryTabs();
                ApplyFinanceTabButtons();
                RenderFinanceContent();
            }
        }

        private string GetOverviewCardGlyph(string cardId) => cardId switch
        {
            "total_balance" => "\uE8C7",
            "card_balance" => "\uE8C7",
            "cash_balance" => "\uEAFD",
            "credit_debt" => "\uE8C7",
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
                    Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                    Foreground = (Brush)Application.Current.Resources["InkBrush"],
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"],
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
                Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                Foreground = (Brush)Application.Current.Resources["InkBrush"]
            });

            var countText = new TextBlock
            {
                Text = _isRussian ? $"{slice.Count} шт." : $"{slice.Count} items",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["InkBrush"],
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
                Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
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
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
            }

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                stack.Children.Add(CreateMutedText(subtitle));
            }

            stack.Children.Add(content);

            return new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
                BorderThickness = new Thickness(0.6),
                CornerRadius = new CornerRadius(24),
                Padding = padding ?? new Thickness(18, 18, 18, 18),
                Shadow = (Shadow)Application.Current.Resources["CardThemeShadow"],
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
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
        };

        private static bool IsCreditAccount(FinanceAccount account) =>
            string.Equals(account.Kind, "bank_card", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(account.CardType, "credit", StringComparison.OrdinalIgnoreCase);

        private string GetFinanceAccountPrimaryAmount(FinanceAccount account) =>
            IsCreditAccount(account)
                ? FormatMoney(-(account.CreditDebtMinor ?? 0), account.Currency)
                : FormatMoney(account.BalanceMinor, account.Currency);

        private string GetFinanceAccountPrimaryCaption(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return _isRussian ? "Доступно сейчас" : "Available now";
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
                return null;
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

            return "\uE8C7";
        }

        private Color GetFinanceAccountAccent(FinanceAccount account)
        {
            if (string.Equals(account.Kind, "cash", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(255, 214, 154, 63);
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

        private Color GetTransactionAccent(FinanceTransaction transaction)
        {
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
            if (!string.IsNullOrWhiteSpace(transaction.CategoryName))
            {
                parts.Add(transaction.CategoryName!);
            }
            if (!string.IsNullOrWhiteSpace(transaction.AccountName))
            {
                parts.Add(transaction.AccountName);
            }
            parts.Add(GetTransactionSourceLabel(transaction.SourceType));
            parts.Add(_isRussian
                ? $"{Math.Max(transaction.ItemCount, transaction.Items.Count)} поз."
                : $"{Math.Max(transaction.ItemCount, transaction.Items.Count)} items");

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

        private void SetLanguage(bool isRussian)
        {
            _isRussian = isRussian;
            ApplyText();
        }

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
            if (Application.Current.Resources[key] is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0);
        }

        private bool HasSession() =>
            _session != null && !string.IsNullOrWhiteSpace(_session.AccessToken);

        private string ExtractUserId()
        {
            if (!string.IsNullOrWhiteSpace(_session?.UserId))
            {
                return _session.UserId!;
            }

            var token = _session?.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2)
                {
                    return string.Empty;
                }

                var payload = parts[1]
                    .Replace('-', '+')
                    .Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);
                var sub = doc.RootElement.GetPropertyOrDefault("sub");
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    _session!.UserId = sub;
                    return sub;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private string ExtractEmail() =>
            !string.IsNullOrWhiteSpace(_session?.UserEmail)
                ? _session.UserEmail!
                : (string.IsNullOrWhiteSpace(EmailInput.Text) ? "session@assistant" : EmailInput.Text.Trim());

        private List<string> ExtractSessionProviders()
        {
            var token = _session?.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return new List<string>();
            }

            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2)
                {
                    return new List<string>();
                }

                var payload = parts[1]
                    .Replace('-', '+')
                    .Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);

                if (doc.RootElement.TryGetProperty("app_metadata", out var appMetadata))
                {
                    if (appMetadata.TryGetProperty("providers", out var providersElement) &&
                        providersElement.ValueKind == JsonValueKind.Array)
                    {
                        var providers = new List<string>();
                        foreach (var item in providersElement.EnumerateArray())
                        {
                            var provider = item.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(provider) &&
                                !providers.Contains(provider, StringComparer.OrdinalIgnoreCase))
                            {
                                providers.Add(provider);
                            }
                        }

                        if (providers.Count > 0)
                        {
                            return providers;
                        }
                    }

                    if (appMetadata.TryGetProperty("provider", out var providerElement))
                    {
                        var provider = providerElement.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(provider))
                        {
                            return new List<string> { provider };
                        }
                    }
                }
            }
            catch
            {
            }

            return new List<string>();
        }

        private string ExtractDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(_settingsSnapshot?.DisplayName))
            {
                return _settingsSnapshot.DisplayName!;
            }

            var email = ExtractEmail();
            var local = email.Split('@', StringSplitOptions.RemoveEmptyEntries)[0];
            if (string.IsNullOrWhiteSpace(local))
            {
                return _isRussian ? "пользователь" : "user";
            }

            return char.ToUpper(local[0]) + local[1..];
        }

        private void SetStatus(string? message, bool isError)
        {
            var safeMessage = message ?? string.Empty;
            AuthStatusBar.Severity = isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            AuthStatusBar.Title = isError
                ? (_isRussian ? "Нужно внимание" : "Attention required")
                : (_isRussian ? "Готово" : "Done");
            AuthStatusBar.Message = safeMessage;
            _ = ShowAnimatedInfoBarAsync(
                AuthStatusBar,
                safeMessage,
                isError,
                true,
                false,
                isError
                    ? (_isRussian ? "Нужно внимание" : "Attention required")
                    : (_isRussian ? "Готово" : "Done"));
        }

        private async Task ShowAnimatedInfoBarAsync(
            InfoBar infoBar,
            string? message,
            bool isError,
            bool isAuthBar,
            bool persist,
            string title)
        {
            var animationCts = isAuthBar ? _authStatusAnimationCts : _settingsStatusAnimationCts;
            animationCts?.Cancel();
            animationCts?.Dispose();
            animationCts = new CancellationTokenSource();
            if (isAuthBar)
            {
                _authStatusAnimationCts = animationCts;
            }
            else
            {
                _settingsStatusAnimationCts = animationCts;
            }
            var token = animationCts.Token;

            if (string.IsNullOrWhiteSpace(message))
            {
                await HideAnimatedInfoBarAsync(infoBar, token);
                return;
            }

            EnsureAnimatedInfoBarSetup(infoBar);
            infoBar.Title = title;
            infoBar.Message = message;
            infoBar.Severity = isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            infoBar.Visibility = Visibility.Visible;
            infoBar.IsOpen = true;

            if (infoBar.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                infoBar.RenderTransform = transform;
            }

            var showStoryboard = new Storyboard();
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(220),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fadeIn, infoBar);
            Storyboard.SetTargetProperty(fadeIn, nameof(UIElement.Opacity));

            var slideIn = new DoubleAnimation
            {
                From = -10,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(slideIn, transform);
            Storyboard.SetTargetProperty(slideIn, nameof(TranslateTransform.Y));

            showStoryboard.Children.Add(fadeIn);
            showStoryboard.Children.Add(slideIn);
            showStoryboard.Begin();

            try
            {
                if (persist)
                {
                    return;
                }

                await Task.Delay(isError ? TimeSpan.FromSeconds(4.8) : TimeSpan.FromSeconds(3.2), token);
                await HideAnimatedInfoBarAsync(infoBar, token);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static void EnsureAnimatedInfoBarSetup(InfoBar infoBar)
        {
            infoBar.Opacity = 0;
            if (infoBar.RenderTransform is not TranslateTransform)
            {
                infoBar.RenderTransform = new TranslateTransform();
            }
        }

        private static Task HideAnimatedInfoBarAsync(InfoBar infoBar, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            if (infoBar.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                infoBar.RenderTransform = transform;
            }

            var tcs = new TaskCompletionSource<object?>();
            var hideStoryboard = new Storyboard();

            var fadeOut = new DoubleAnimation
            {
                From = infoBar.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fadeOut, infoBar);
            Storyboard.SetTargetProperty(fadeOut, nameof(UIElement.Opacity));

            var slideOut = new DoubleAnimation
            {
                From = 0,
                To = -8,
                Duration = TimeSpan.FromMilliseconds(180),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(slideOut, transform);
            Storyboard.SetTargetProperty(slideOut, nameof(TranslateTransform.Y));

            hideStoryboard.Children.Add(fadeOut);
            hideStoryboard.Children.Add(slideOut);
            hideStoryboard.Completed += (_, _) =>
            {
                infoBar.IsOpen = false;
                infoBar.Visibility = Visibility.Collapsed;
                infoBar.Opacity = 1;
                transform.Y = 0;
                tcs.TrySetResult(null);
            };
            hideStoryboard.Begin();
            return tcs.Task;
        }

        private void SetBusy(bool busy)
        {
            SubmitButton.IsEnabled = !busy;
            GoogleButton.IsEnabled = !busy;
            SettingsLogoutButton.IsEnabled = !busy;
            SettingsDeleteButton.IsEnabled = !busy;
        }

        private string LocalizeAuthError(string? raw, string fallback)
        {
            var message = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return fallback;
            }

            var normalized = message.ToLowerInvariant();
            if (normalized.Contains("invalid login credentials"))
            {
                return _isRussian ? "Неверный email или пароль." : "Incorrect email or password.";
            }

            if (normalized.Contains("email not confirmed"))
            {
                return _isRussian ? "Подтвердите email и затем войдите." : "Confirm your email first, then sign in.";
            }

            if (normalized.Contains("user already registered") || normalized.Contains("already been registered"))
            {
                return _isRussian ? "Этот email уже занят." : "This email is already in use.";
            }

            if (normalized.Contains("over_email_send_rate_limit") || normalized.Contains("security purposes") || normalized.Contains("rate limit"))
            {
                return _isRussian ? "Слишком много попыток. Подождите немного и попробуйте снова." : "Too many attempts. Wait a bit and try again.";
            }

            if (normalized.Contains("signup is disabled"))
            {
                return _isRussian ? "Регистрация сейчас недоступна." : "Sign-up is currently unavailable.";
            }

            if (normalized.Contains("network request failed"))
            {
                return _isRussian ? "Проблема сети. Проверьте подключение и попробуйте снова." : "Network issue. Check your connection and try again.";
            }

            if (normalized.Contains("manual linking") && normalized.Contains("disabled"))
            {
                return _isRussian
                    ? "В текущей конфигурации Supabase ручная привязка или отвязка Google недоступна."
                    : "Manual Google linking or unlinking is unavailable in the current Supabase configuration.";
            }

            if (normalized.Contains("not added to the linked identities"))
            {
                return _isRussian
                    ? "Google OAuth завершился, но identity не привязалась. Скорее всего, в Supabase выключен Manual Linking."
                    : "Google OAuth completed, but the identity was not linked. Manual Linking is likely disabled in Supabase.";
            }

            if (normalized.Contains("different google account"))
            {
                return _isRussian
                    ? "Выбран другой Google-аккаунт. Для привязки нужен тот же email, что и у текущего профиля."
                    : "A different Google account was selected. Use the same email as the current profile.";
            }

            if (normalized.Contains("invalid jwt") || normalized.Contains("\"code\":401") || normalized.Contains("jwt"))
            {
                return _isRussian ? "Сессия устарела. Попробуйте открыть настройки ещё раз." : "Your session expired. Open Settings again.";
            }

            if (normalized.Contains("password should be at least"))
            {
                return _isRussian ? "Минимум 8 символов." : "Use at least 8 characters.";
            }

            return fallback;
        }

        private string LocalizeSettingsLoadError(string? raw)
        {
            var message = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return _isRussian ? "Не удалось загрузить настройки." : "Failed to load settings.";
            }

            var normalized = message.ToLowerInvariant();
            if (normalized.Contains("profiles"))
            {
                return _isRussian
                    ? "Не удалось загрузить профиль: имя или email."
                    : "Failed to load the profile: display name or email.";
            }

            if (normalized.Contains("user_settings"))
            {
                return _isRussian
                    ? "Не удалось загрузить настройки Gemini."
                    : "Failed to load Gemini settings.";
            }

            if (normalized.Contains("settings_get_linked_identities") || normalized.Contains("identity"))
            {
                return _isRussian
                    ? "Не удалось загрузить привязанные способы входа."
                    : "Failed to load linked sign-in methods.";
            }

            if (normalized.Contains("bad gateway") || normalized.Contains("gateway") || normalized.Contains("timeout") || normalized.Contains("timed out"))
            {
                return _isRussian
                    ? "Не удалось загрузить часть настроек из-за временной проблемы сети или Supabase."
                    : "Part of the settings could not be loaded because of a temporary network or Supabase issue.";
            }

            return _isRussian ? "Не удалось загрузить настройки." : "Failed to load settings.";
        }

        private void LoadSession()
        {
            RememberCheck.IsChecked = _sessionStore.LoadRememberDevice();
            if (RememberCheck.IsChecked != true)
            {
                _sessionStore.Clear();
                UpdateShellState();
                return;
            }

            var json = _sessionStore.Load();
            if (string.IsNullOrWhiteSpace(json))
            {
                UpdateShellState();
                return;
            }

            try
            {
                _session = JsonSerializer.Deserialize<AuthSession>(json);
                if (HasSession())
                {
                    SetStatus(_isRussian ? "Сессия восстановлена." : "Session restored.", false);
                }
            }
            catch
            {
                _session = null;
            }

            UpdateShellState();
        }

        private void PersistSession()
        {
            if (_session == null)
            {
                return;
            }

            var rememberDevice = RememberCheck.IsChecked == true;
            _sessionStore.Save(_session, rememberDevice);
        }

        private string? ValidateRegistration(string email, string password, string confirm)
        {
            return ValidateEmail(email) ??
                ValidatePassword(email, password) ??
                ValidatePasswordConfirmation(password, confirm);
        }

        private string? ValidatePasswordForReset(string email, string password, string confirm)
        {
            return ValidatePassword(email, password) ??
                ValidatePasswordConfirmation(password, confirm);
        }

        private string? ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return _isRussian ? "Введите email." : "Enter your email.";
            }

            try
            {
                var address = new System.Net.Mail.MailAddress(email.Trim());
                if (!string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return _isRussian ? "Введите корректный email." : "Enter a valid email.";
                }
            }
            catch
            {
                return _isRussian ? "Введите корректный email." : "Enter a valid email.";
            }

            return null;
        }

        private string? ValidatePassword(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return _isRussian ? "Введите пароль." : "Enter your password.";
            }

            if (password.Length < 8)
            {
                return _isRussian ? "Минимум 8 символов." : "Use at least 8 characters.";
            }

            if (!ContainsUppercase(password))
            {
                return _isRussian ? "Добавьте заглавную букву." : "Add an uppercase letter.";
            }

            if (!ContainsLowercase(password))
            {
                return _isRussian ? "Добавьте строчную букву." : "Add a lowercase letter.";
            }

            if (!ContainsDigit(password))
            {
                return _isRussian ? "Добавьте цифру." : "Add a number.";
            }

            var lowered = password.Trim().ToLowerInvariant();
            if (lowered is "password" or "password1" or "qwerty123" or "12345678")
            {
                return _isRussian ? "Используйте более сложный пароль." : "Use a stronger password.";
            }

            var emailHead = email.Split('@', StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(emailHead) && lowered.Contains(emailHead))
            {
                return _isRussian
                    ? "Пароль не должен содержать имя из email."
                    : "Password should not contain your email name.";
            }

            return null;
        }

        private string? ValidatePasswordConfirmation(string password, string confirm)
        {
            if (string.IsNullOrWhiteSpace(confirm))
            {
                return _isRussian ? "Повторите пароль." : "Confirm your password.";
            }

            return password == confirm
                ? null
                : (_isRussian ? "Пароли не совпадают." : "Passwords do not match.");
        }

        private string ReadPassword() => PasswordInput.Password ?? string.Empty;

        private string ReadConfirmPassword() => ConfirmPasswordInput.Password ?? string.Empty;

        private void AnimateAuthForm()
        {
            AuthFormPanel.Opacity = 0;
            AuthFormTranslate.Y = 12;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var slideIn = new DoubleAnimation
            {
                From = 12,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(fadeIn, AuthFormPanel);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            Storyboard.SetTarget(slideIn, AuthFormTranslate);
            Storyboard.SetTargetProperty(slideIn, "Y");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeIn);
            storyboard.Children.Add(slideIn);
            storyboard.Begin();
        }

        private static bool ContainsUppercase(string value)
        {
            foreach (var ch in value)
            {
                if (char.IsUpper(ch))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsLowercase(string value)
        {
            foreach (var ch in value)
            {
                if (char.IsLower(ch))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDigit(string value)
        {
            foreach (var ch in value)
            {
                if (char.IsDigit(ch))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

internal static class QueryHelpers
{
    public static Dictionary<string, string> Parse(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddPairs(result, uri.Query?.TrimStart('?'));
        AddPairs(result, uri.Fragment?.TrimStart('#'));
        return result;
    }

    private static void AddPairs(Dictionary<string, string> target, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            target[key] = value;
        }
    }
}
