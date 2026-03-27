using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace Assistant.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            AppInstance.GetCurrent().Activated += OnAppActivated;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
            _ = HandleInitialActivationAsync();
        }

        private void OnAppActivated(object? sender, AppActivationArguments args)
        {
            var dispatcherQueue = _window?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                _window ??= new MainWindow();
                _window.Activate();
                _ = HandleActivationAsync(args);
                return;
            }

            dispatcherQueue.TryEnqueue(() =>
            {
                _window ??= new MainWindow();
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
