using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Assistant.WinUI.Application.Abstractions;
using Assistant.WinUI.Application.DependencyInjection;
using Assistant.WinUI.Application.Shell;
using Assistant.WinUI.Auth;
using Assistant.WinUI.Finance;
using Assistant.WinUI.Infrastructure.DependencyInjection;
using Assistant.WinUI.Settings;
using Assistant.WinUI.Storage;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace Assistant.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : global::Microsoft.UI.Xaml.Application
    {
        private readonly IHost _host;
        private Window? _window;

        public App()
        {
            InitializeComponent();
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddAssistantApplication();
            builder.Services.AddAssistantInfrastructure();
            builder.Services.AddSingleton(sp => new MainWindow(
                sp.GetRequiredService<ShellViewModel>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<SupabaseAuthClient>(),
                sp.GetService<FinanceApiClient>(),
                sp.GetService<SettingsApiClient>(),
                sp.GetRequiredService<SecureSessionStore>(),
                sp.GetRequiredService<SecureGeminiSettingsStore>(),
                sp.GetRequiredService<DisplayNameStore>()));
            _host = builder.Build();
            AppInstance.GetCurrent().Activated += OnAppActivated;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = _host.Services.GetRequiredService<MainWindow>();
            _window.Activate();
            _ = HandleInitialActivationAsync();
        }

        private void OnAppActivated(object? sender, AppActivationArguments args)
        {
            var dispatcherQueue = _window?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                _window ??= _host.Services.GetRequiredService<MainWindow>();
                _window.Activate();
                _ = HandleActivationAsync(args);
                return;
            }

            dispatcherQueue.TryEnqueue(() =>
            {
                _window ??= _host.Services.GetRequiredService<MainWindow>();
                _window.Activate();
                _ = HandleActivationAsync(args);
            });
        }

        private async Task HandleInitialActivationAsync()
        {
            await HandleActivationAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
        }

        private async Task HandleActivationAsync(AppActivationArguments args)
        {
            if (args.Kind != ExtendedActivationKind.Protocol ||
                args.Data is not ProtocolActivatedEventArgs protocolArgs ||
                _window is not MainWindow mainWindow)
            {
                return;
            }

            await mainWindow.HandleProtocolActivationAsync(protocolArgs.Uri);
        }
    }
}

