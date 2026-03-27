using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Assistant.WinUI.Auth;
using Assistant.WinUI.Finance;
using Assistant.WinUI.Storage;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.System;

namespace Assistant.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private enum ThemeMode
        {
            System,
            Light,
            Dark
        }

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
            Settings
        }

        private ThemeMode _themeMode = ThemeMode.System;
        private bool _isRussian = true;
        private AuthMode _mode = AuthMode.Login;
        private DashboardSection _section = DashboardSection.Home;
        private FinanceTab _financeTab = FinanceTab.Overview;
        private string _activeSubsection = "summary";
        private int _financeOnboardingStep;
        private FinanceOverview? _financeOverview;
        private readonly SupabaseAuthClient _authClient;
        private readonly FinanceApiClient? _financeClient;
        private readonly SecureSessionStore _sessionStore = new();
        private AuthSession? _session;
        private bool _showPassword;
        private bool _showConfirmPassword;
        private bool _isCompactShell;

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

            ForgotButton.Click += ForgotButton_Click;
            BackToLoginButton.Click += BackToLoginButton_Click;
            SubmitButton.Click += SubmitButton_Click;
            GoogleButton.Click += GoogleButton_Click;

            ApplyText();
            ApplyTheme();
            ApplyWindowChrome();
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

        private void ThemeSystemButton_Click(object sender, RoutedEventArgs e)
        {
            _themeMode = ThemeMode.System;
            ApplyTheme();
        }

        private void ThemeLightButton_Click(object sender, RoutedEventArgs e)
        {
            _themeMode = ThemeMode.Light;
            ApplyTheme();
        }

        private void ThemeDarkButton_Click(object sender, RoutedEventArgs e)
        {
            _themeMode = ThemeMode.Dark;
            ApplyTheme();
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

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _showPassword = !_showPassword;
            SyncPasswordVisibility();
        }

        private void ToggleConfirmPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _showConfirmPassword = !_showConfirmPassword;
            SyncConfirmPasswordVisibility();
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
        private void FinanceSettingsTabButton_Click(object sender, RoutedEventArgs e) => SetFinanceTab(FinanceTab.Settings);
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

        private void ForgotButton_Click(object sender, RoutedEventArgs e)
        {
            _mode = AuthMode.Forgot;
            ApplyMode();
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
                using var listener = new LoopbackOAuthListener(AppConfig.GoogleLoopbackRedirectUri);
                var url = _authClient.BuildGoogleAuthorizeUrl(
                    AppConfig.GoogleLoopbackRedirectUri,
                    challenge,
                    forceAccountSelection: true);
                await Launcher.LaunchUriAsync(new Uri(url));
                var callbackUri = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(2));
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
                SetBusy(false);
                SetStatus(_isRussian ? "Вы вышли." : "Signed out.", false);
                _mode = AuthMode.Login;
                ApplyMode();
                UpdateShellState();
            }
        }

        private void ApplyTheme()
        {
            RootGrid.RequestedTheme = _themeMode switch
            {
                ThemeMode.Light => ElementTheme.Light,
                ThemeMode.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            ApplyThemeButtonState(ThemeSystemButton, _themeMode == ThemeMode.System);
            ApplyThemeButtonState(ThemeLightButton, _themeMode == ThemeMode.Light);
            ApplyThemeButtonState(ThemeDarkButton, _themeMode == ThemeMode.Dark);

            ApplyWindowChrome();
        }

        private void ApplyText()
        {
            LogoText.Text = "ASSISTANT";
            LangButton.Content = _isRussian ? "RU" : "EN";
            RussianMenuItem.Text = _isRussian ? "Русский" : "Russian";
            EnglishMenuItem.Text = "English";
            RussianMenuItem.IsChecked = _isRussian;
            EnglishMenuItem.IsChecked = !_isRussian;

            if (_isRussian)
            {
                LoginTab.Content = "Вход";
                RegisterTab.Content = "Регистрация";
                EmailLabel.Text = "EMAIL";
                EmailInput.PlaceholderText = "you@example.com";
                PasswordLabel.Text = "ПАРОЛЬ";
                PasswordInput.PlaceholderText = "••••••••";
                ConfirmPasswordLabel.Text = "ПОВТОРИТЕ ПАРОЛЬ";
                ConfirmPasswordInput.PlaceholderText = "••••••••";
                RememberCheck.Content = "Запомнить устройство";
                ForgotButton.Content = "Забыли пароль?";
                GoogleButton.Content = "Продолжить с Google";
                BackToLoginButton.Content = "Вернуться к входу";
                FormNote.Text = "Данные шифруются и хранятся безопасно.";
            }
            else
            {
                LoginTab.Content = "Sign in";
                RegisterTab.Content = "Register";
                EmailLabel.Text = "EMAIL";
                EmailInput.PlaceholderText = "you@example.com";
                PasswordLabel.Text = "PASSWORD";
                PasswordInput.PlaceholderText = "••••••••";
                ConfirmPasswordLabel.Text = "CONFIRM PASSWORD";
                ConfirmPasswordInput.PlaceholderText = "••••••••";
                RememberCheck.Content = "Remember this device";
                ForgotButton.Content = "Forgot password?";
                GoogleButton.Content = "Continue with Google";
                BackToLoginButton.Content = "Back to sign in";
                FormNote.Text = "We store your data securely.";
            }

            ApplyDashboardText();
            ApplyFinanceText();
            ApplyTheme();
            ApplyMode();
            UpdateShellState();
        }

        private void ApplyDashboardText()
        {
            SidebarWorkspaceLabel.Text = _isRussian ? "WORKSPACE" : "WORKSPACE";
            SidebarTitle.Text = "Assistant";
            SidebarCopy.Text = string.Empty;

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
            SettingsLogoutButton.Content = _isRussian ? "Выйти из аккаунта" : "Sign out";

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
            FinanceEyebrow.Text = _isRussian ? "FINANCE" : "FINANCE";
            FinanceTitle.Text = _isRussian ? "Финансы без шума" : "Finance without clutter";
            FinanceSubtitle.Text = _isRussian
                ? "Обзор, счета и онбординг собраны в одном быстром модуле."
                : "Overview, accounts, and onboarding live in one fast module.";

            FinanceOverviewTabButton.Content = _isRussian ? "Главная" : "Overview";
            FinanceAccountsTabButton.Content = _isRussian ? "Счета" : "Accounts";
            FinanceTransactionsTabButton.Content = _isRussian ? "Транзакции" : "Transactions";
            FinanceSettingsTabButton.Content = _isRussian ? "Настройки" : "Settings";

            FinanceLoadingText.Text = _isRussian ? "Загружаем финансовую сводку..." : "Loading finance overview...";
            FinanceErrorTitle.Text = _isRussian ? "Не удалось загрузить финансы" : "Failed to load finance";
            FinanceRetryButton.Content = _isRussian ? "Повторить" : "Retry";

            FinanceOnboardingBadge.Text = _isRussian ? "ОНБОРДИНГ" : "ONBOARDING";
            FinanceCurrencyLabel.Text = _isRussian ? "Основная валюта" : "Primary currency";
            FinanceBankLabel.Text = _isRussian ? "Банк основной карты" : "Primary card bank";
            FinanceCashLabel.Text = _isRussian ? "Наличные" : "Cash";
            FinancePrimaryBalanceInput.PlaceholderText = _isRussian ? "Баланс основной карты" : "Primary card balance";
            FinanceCashInput.PlaceholderText = _isRussian ? "Сумма наличных" : "Cash amount";
            FinanceBackButton.Content = _isRussian ? "Назад" : "Back";
            FinanceSkipButton.Content = _isRussian ? "Пропустить" : "Skip";

            FinanceBalanceLabel.Text = _isRussian ? "ТЕКУЩИЙ БАЛАНС" : "CURRENT BALANCE";
            FinanceAccountsTitle.Text = _isRussian ? "Счета" : "Accounts";
            FinanceTransactionsTitle.Text = _isRussian ? "Последние транзакции" : "Recent transactions";
            FinanceSettingsTitle.Text = _isRussian ? "Что дальше" : "What next";

            PopulateFinanceInputs();
            ApplyFinanceOnboardingStep();
            ApplyFinanceTabButtons();
            RenderFinanceContent();
        }

        private void ApplyMode()
        {
            AuthTitleText.Text = _mode switch
            {
                AuthMode.Register => _isRussian ? "Создайте аккаунт" : "Create your account",
                AuthMode.Forgot => _isRussian ? "Восстановите доступ" : "Recover access",
                AuthMode.Reset => _isRussian ? "Обновите пароль" : "Update password",
                _ => _isRussian ? "Добро пожаловать обратно" : "Welcome back"
            };

            AuthSubtitleText.Text = _mode switch
            {
                AuthMode.Register => _isRussian
                    ? "Один аккаунт для Web, Android и Windows."
                    : "One account for Web, Android, and Windows.",
                AuthMode.Forgot => _isRussian
                    ? "Отправим ссылку для восстановления на вашу почту."
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

            PasswordLabel.Visibility = _mode == AuthMode.Forgot ? Visibility.Collapsed : Visibility.Visible;
            PasswordInput.Visibility = _mode == AuthMode.Forgot ? Visibility.Collapsed : Visibility.Visible;

            RememberCheck.Visibility = _mode == AuthMode.Login ? Visibility.Visible : Visibility.Collapsed;
            ForgotButton.Visibility = _mode == AuthMode.Login ? Visibility.Visible : Visibility.Collapsed;
            GoogleButton.Visibility = (AppConfig.GoogleAuthEnabled && (_mode == AuthMode.Login || _mode == AuthMode.Register))
                ? Visibility.Visible
                : Visibility.Collapsed;
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

            var activeBrush = (Brush)Application.Current.Resources["PrimaryButtonBackgroundBrush"];
            var activeForeground = (Brush)Application.Current.Resources["PrimaryButtonForegroundBrush"];
            var idleBrush = (Brush)Application.Current.Resources["CardBackgroundBrush"];
            var idleForeground = (Brush)Application.Current.Resources["InkBrush"];

            var loginActive = _mode == AuthMode.Login;
            LoginTab.Background = loginActive ? activeBrush : idleBrush;
            LoginTab.Foreground = loginActive ? activeForeground : idleForeground;
            RegisterTab.Background = _mode == AuthMode.Register ? activeBrush : idleBrush;
            RegisterTab.Foreground = _mode == AuthMode.Register ? activeForeground : idleForeground;

            _showPassword = false;
            _showConfirmPassword = false;
            SyncPasswordVisibility();
            SyncConfirmPasswordVisibility();
            StatusText.Text = string.Empty;
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
            DashboardSummaryRow.Visibility = financeVisible ? Visibility.Collapsed : Visibility.Visible;
            PlaceholderCard.Visibility = financeVisible ? Visibility.Collapsed : Visibility.Visible;
            FinanceContent.Visibility = financeVisible ? Visibility.Visible : Visibility.Collapsed;
            SettingsActionPanel.Visibility = settingsVisible ? Visibility.Visible : Visibility.Collapsed;

            if (settingsVisible)
            {
                SettingsLogoutButton.Content = _isRussian ? "Выйти из аккаунта" : "Sign out";
            }

            if (!animated)
            {
                if (!financeVisible)
                {
                    PlaceholderCard.Opacity = 1;
                    PlaceholderTranslate.Y = 0;
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
            button.Background = active
                ? (Brush)Application.Current.Resources["StageBackgroundBrush"]
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0));
            button.Foreground = active
                ? (Brush)Application.Current.Resources["InkBrush"]
                : (Brush)Application.Current.Resources["MutedTextBrush"];
            button.BorderBrush = active
                ? (Brush)Application.Current.Resources["StrokeBrush"]
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0));

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
                    : (Brush)Application.Current.Resources["StageBackgroundBrush"];
                iconBox.BorderBrush = active
                    ? (Brush)Application.Current.Resources["StrokeBrush"]
                    : (Brush)Application.Current.Resources["StrokeBrush"];
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
                    "settings" => FinanceTab.Settings,
                    _ => FinanceTab.Overview
                });
                return;
            }

            ApplySecondaryTabs();
        }

        private void ApplyThemeButtonState(Button button, bool active)
        {
            button.Background = active
                ? (Brush)Application.Current.Resources["PillBackgroundBrush"]
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0));
            button.Foreground = active
                ? (Brush)Application.Current.Resources["InkBrush"]
                : (Brush)Application.Current.Resources["MutedTextBrush"];
        }

        private void ApplyAdaptiveShellLayout()
        {
            var width = RootGrid.ActualWidth;
            if (width <= 0 && AppWindow != null)
            {
                width = AppWindow.Size.Width;
            }

            _isCompactShell = width > 0 && width < 1040;
            var hasSession = HasSession();

            SidebarSurface.Visibility = hasSession && !_isCompactShell ? Visibility.Visible : Visibility.Collapsed;
            CompactPrimaryNavScroller.Visibility = hasSession && _isCompactShell ? Visibility.Visible : Visibility.Collapsed;
            DashboardShellLayout.ColumnDefinitions[0].Width = _isCompactShell ? new GridLength(0) : new GridLength(272);
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
                FinanceTab.Settings => "settings",
                _ => "overview"
            };
            ApplySecondaryTabs();
            ApplyFinanceTabButtons();
            RenderFinanceContent();
        }

        private void ApplyFinanceTabButtons()
        {
            ApplyNavButtonState(FinanceOverviewTabButton, _financeTab == FinanceTab.Overview);
            ApplyNavButtonState(FinanceAccountsTabButton, _financeTab == FinanceTab.Accounts);
            ApplyNavButtonState(FinanceTransactionsTabButton, _financeTab == FinanceTab.Transactions);
            ApplyNavButtonState(FinanceSettingsTabButton, _financeTab == FinanceTab.Settings);
        }

        private void PopulateFinanceInputs()
        {
            var currentCurrency = FinanceCurrencyCombo.SelectedItem as ComboBoxItem;
            var currentBank = FinanceBankCombo.SelectedItem as ComboBoxItem;

            FinanceCurrencyCombo.Items.Clear();
            FinanceBankCombo.Items.Clear();

            AddComboItem(FinanceCurrencyCombo, "RUB", _isRussian ? "Рубли" : "Rubles");
            AddComboItem(FinanceCurrencyCombo, "USD", _isRussian ? "Доллары" : "US Dollars");
            AddComboItem(FinanceCurrencyCombo, "EUR", _isRussian ? "Евро" : "Euro");

            AddComboItem(FinanceBankCombo, "", _isRussian ? "Пропустить" : "Skip");
            AddComboItem(FinanceBankCombo, "Т-Банк", "Т-Банк");
            AddComboItem(FinanceBankCombo, "Сбер", "Сбер");
            AddComboItem(FinanceBankCombo, "Альфа", "Альфа");
            AddComboItem(FinanceBankCombo, "ВТБ", "ВТБ");

            SelectComboItemByTag(FinanceCurrencyCombo, currentCurrency?.Tag as string ?? _financeOverview?.DefaultCurrency ?? "RUB");
            SelectComboItemByTag(FinanceBankCombo, currentBank?.Tag as string ?? string.Empty);
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
                    2 => "Если хотите, задайте банк основной карты и стартовый баланс.",
                    _ => "Укажите наличные или пропустите этот шаг."
                }
                : stepNumber switch
                {
                    1 => "Choose the main currency for your finance summary.",
                    2 => "Optionally set the primary card bank and opening balance.",
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
                _financeOverview = await _financeClient.GetOverviewAsync(_session.AccessToken);
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
                if (string.IsNullOrWhiteSpace(bank))
                {
                    bank = null;
                }

                _financeOverview = await _financeClient.CompleteOnboardingAsync(
                    _session.AccessToken,
                    currency,
                    bank,
                    skip ? null : ParseMinor(FinanceCashInput.Text),
                    skip ? null : ParseMinor(FinancePrimaryBalanceInput.Text));
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
                FinanceAccountsPanel.Children.Clear();
                FinanceTransactionsPanel.Children.Clear();
                return;
            }

            var currency = overview.DefaultCurrency ?? "RUB";
            FinanceBalanceText.Text = FormatMoney(overview.TotalBalanceMinor, currency);
            FinanceBalanceHint.Text = _isRussian
                ? "Баланс складывается из всех счетов и наличных."
                : "Balance is aggregated from all accounts and cash.";
            FinanceSettingsText.Text = _isRussian
                ? $"Основная валюта: {currency}. Следующим этапом добавим создание транзакций и расширенные счета."
                : $"Primary currency: {currency}. Transactions and richer account management are next.";

            RenderFinanceAccounts(overview);
            RenderFinanceTransactions(overview);

            FinanceBalanceLabel.Visibility = _financeTab == FinanceTab.Transactions ? Visibility.Collapsed : Visibility.Visible;
            FinanceAccountsCard.Visibility = (_financeTab == FinanceTab.Overview || _financeTab == FinanceTab.Accounts) ? Visibility.Visible : Visibility.Collapsed;
            FinanceTransactionsCard.Visibility = (_financeTab == FinanceTab.Overview || _financeTab == FinanceTab.Transactions) ? Visibility.Visible : Visibility.Collapsed;
            FinanceSettingsCard.Visibility = _financeTab == FinanceTab.Settings ? Visibility.Visible : Visibility.Collapsed;
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
                    Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(20),
                    Padding = new Thickness(14)
                };

                var stack = new StackPanel { Spacing = 4 };
                stack.Children.Add(new TextBlock
                {
                    Text = account.IsPrimary ? $"{account.Name} · {_isRussian switch { true => "основной", false => "primary" }}" : account.Name,
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
                stack.Children.Add(CreateMutedText($"{account.BankName ?? account.Kind} · {FormatMoney(account.BalanceMinor, account.Currency)}"));
                card.Child = stack;
                FinanceAccountsPanel.Children.Add(card);
            }
        }

        private void RenderFinanceTransactions(FinanceOverview overview)
        {
            FinanceTransactionsPanel.Children.Clear();

            if (overview.RecentTransactions.Count == 0)
            {
                FinanceTransactionsPanel.Children.Add(CreateMutedText(_isRussian ? "Транзакций пока нет. Следующим этапом подключим ввод операций." : "No transactions yet. Transaction input comes next."));
                return;
            }

            foreach (var transaction in overview.RecentTransactions)
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["PageBackgroundBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(20),
                    Padding = new Thickness(14)
                };

                var stack = new StackPanel { Spacing = 4 };
                stack.Children.Add(new TextBlock
                {
                    Text = transaction.Title,
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["InkBrush"]
                });
                var amountPrefix = string.Equals(transaction.Direction, "expense", StringComparison.OrdinalIgnoreCase) ? "-" : "+";
                stack.Children.Add(CreateMutedText($"{amountPrefix}{FormatMoney(transaction.AmountMinor, transaction.Currency)} · {transaction.HappenedAt:dd.MM.yyyy}"));
                if (!string.IsNullOrWhiteSpace(transaction.Note))
                {
                    stack.Children.Add(CreateMutedText(transaction.Note));
                }

                card.Child = stack;
                FinanceTransactionsPanel.Children.Add(card);
            }
        }

        private TextBlock CreateMutedText(string text) => new()
        {
            Text = text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
        };

        private string FormatMoney(long minor, string currencyCode)
        {
            var culture = _isRussian ? new CultureInfo("ru-RU") : new CultureInfo("en-US");
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();
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

            return (minor / 100m).ToString("C", format);
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

            var pageBackground = GetResourceColor("PageBackgroundBrush");
            var cardBackground = GetResourceColor("CardBackgroundBrush");
            var ink = GetResourceColor("InkBrush");
            var muted = GetResourceColor("MutedTextBrush");

            titleBar.BackgroundColor = pageBackground;
            titleBar.ForegroundColor = muted;
            titleBar.InactiveBackgroundColor = pageBackground;
            titleBar.InactiveForegroundColor = muted;

            titleBar.ButtonBackgroundColor = pageBackground;
            titleBar.ButtonForegroundColor = muted;
            titleBar.ButtonHoverBackgroundColor = cardBackground;
            titleBar.ButtonHoverForegroundColor = ink;
            titleBar.ButtonPressedBackgroundColor = cardBackground;
            titleBar.ButtonPressedForegroundColor = ink;
            titleBar.ButtonInactiveBackgroundColor = pageBackground;
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

        private string ExtractEmail() =>
            !string.IsNullOrWhiteSpace(_session?.UserEmail)
                ? _session.UserEmail!
                : (string.IsNullOrWhiteSpace(EmailInput.Text) ? "session@assistant" : EmailInput.Text.Trim());

        private string ExtractDisplayName()
        {
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
            StatusText.Text = message ?? string.Empty;
            StatusText.Foreground = isError
                ? (Brush)Application.Current.Resources["ErrorTextBrush"]
                : (Brush)Application.Current.Resources["AccentBrush"];
        }

        private void SetBusy(bool busy)
        {
            SubmitButton.IsEnabled = !busy;
            GoogleButton.IsEnabled = !busy;
            SettingsLogoutButton.IsEnabled = !busy;
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

            if (normalized.Contains("password should be at least"))
            {
                return _isRussian ? "Минимум 8 символов." : "Use at least 8 characters.";
            }

            return fallback;
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

        private string ReadPassword() =>
            _showPassword ? (PasswordTextInput.Text ?? string.Empty) : (PasswordInput.Password ?? string.Empty);

        private string ReadConfirmPassword() =>
            _showConfirmPassword ? (ConfirmPasswordTextInput.Text ?? string.Empty) : (ConfirmPasswordInput.Password ?? string.Empty);

        private void SyncPasswordVisibility()
        {
            if (_showPassword)
            {
                PasswordTextInput.Text = PasswordInput.Password;
            }
            else
            {
                PasswordInput.Password = PasswordTextInput.Text ?? string.Empty;
            }

            PasswordInput.Visibility = _showPassword ? Visibility.Collapsed : Visibility.Visible;
            PasswordTextInput.Visibility = _showPassword ? Visibility.Visible : Visibility.Collapsed;
            TogglePasswordButton.Opacity = _showPassword ? 1 : 0.72;
            ToolTipService.SetToolTip(TogglePasswordButton, _showPassword
                ? (_isRussian ? "Скрыть пароль" : "Hide password")
                : (_isRussian ? "Показать пароль" : "Show password"));
        }

        private void SyncConfirmPasswordVisibility()
        {
            if (_showConfirmPassword)
            {
                ConfirmPasswordTextInput.Text = ConfirmPasswordInput.Password;
            }
            else
            {
                ConfirmPasswordInput.Password = ConfirmPasswordTextInput.Text ?? string.Empty;
            }

            ConfirmPasswordInput.Visibility = _showConfirmPassword ? Visibility.Collapsed : Visibility.Visible;
            ConfirmPasswordTextInput.Visibility = _showConfirmPassword ? Visibility.Visible : Visibility.Collapsed;
            ToggleConfirmPasswordButton.Opacity = _showConfirmPassword ? 1 : 0.72;
            ToolTipService.SetToolTip(ToggleConfirmPasswordButton, _showConfirmPassword
                ? (_isRussian ? "Скрыть пароль" : "Hide password")
                : (_isRussian ? "Показать пароль" : "Show password"));
        }

        private void AnimateAuthForm()
        {
            AuthFormPanel.Opacity = 0;
            AuthFormTranslate.Y = 10;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(220))
            };
            var slideIn = new DoubleAnimation
            {
                From = 10,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(220))
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
