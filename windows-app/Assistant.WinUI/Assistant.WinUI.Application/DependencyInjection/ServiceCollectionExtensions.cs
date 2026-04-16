using Assistant.WinUI.Application.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Assistant.WinUI.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAssistantApplication(this IServiceCollection services)
    {
        services.AddSingleton<IShellNavigationService, ShellNavigationService>();
        services.AddSingleton<ShellViewModel>();
        return services;
    }
}
