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
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwRestore = 9;
        private const int SwShow = 5;

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
        private readonly SecureSessionStore _sessionStore;
        private readonly SecureGeminiSettingsStore _geminiSettingsStore;
        private readonly DisplayNameStore _displayNameStore;
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
        private readonly AppShellViewModel _shellViewModel;
        private readonly AppLocalizationService _localizationService;
        private static readonly string[] OverviewCardOrder =
        {
            "total_balance",
            "card_balance",
            "cash_balance",
            "credit_debt",
            "loan_debt",
            "loan_full_debt",
            "credit_spend",
            "month_income",
            "month_expense",
            "month_result",
            "recent_transactions"
        };

        internal MainWindow(
            AppShellViewModel shellViewModel,
            AppLocalizationService localizationService,
            SupabaseAuthClient authClient,
            FinanceApiClient? financeClient,
            SettingsApiClient? settingsClient,
            SecureSessionStore sessionStore,
            SecureGeminiSettingsStore geminiSettingsStore,
            DisplayNameStore displayNameStore)
        {
            _shellViewModel = shellViewModel;
            _localizationService = localizationService;
            _authClient = authClient;
            _financeClient = financeClient;
            _settingsClient = settingsClient;
            _sessionStore = sessionStore;
            _geminiSettingsStore = geminiSettingsStore;
            _displayNameStore = displayNameStore;
            InitializeComponent();
            _isRussian = _localizationService.IsRussian;
            _section = ParseDashboardSection(_shellViewModel.Section);
            _activeSubsection = _shellViewModel.ActiveSubsection;
            _isCompactShell = _shellViewModel.IsCompact;
            RememberCheck.IsChecked = _sessionStore.LoadRememberDevice();

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

            if (!AppConfig.HasSupabaseConfiguration)
            {
                SetStatus(_isRussian ? "Нет настроек Supabase." : "Supabase config is missing.", true);
                SetBusy(true);
            }
        }

    }
}

