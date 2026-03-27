using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Assistant.WinUI
{
    public static class Program
    {
        [System.STAThread]
        public static async Task Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (await DecideRedirectionAsync())
            {
                return;
            }

            Application.Start(_ =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }

        private static async Task<bool> DecideRedirectionAsync()
        {
            var mainInstance = AppInstance.FindOrRegisterForKey("main");
            var currentInstance = AppInstance.GetCurrent();

            if (mainInstance.IsCurrent)
            {
                return false;
            }

            await mainInstance.RedirectActivationToAsync(currentInstance.GetActivatedEventArgs()).AsTask();
            return true;
        }
    }
}
