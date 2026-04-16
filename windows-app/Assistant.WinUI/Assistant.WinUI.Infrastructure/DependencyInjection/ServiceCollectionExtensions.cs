using Assistant.WinUI.Application.Abstractions;
using Assistant.WinUI.Auth;
using Assistant.WinUI.Finance;
using Assistant.WinUI.Settings;
using Assistant.WinUI.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Assistant.WinUI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAssistantInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton(_ =>
            AppConfig.HasSupabaseConfiguration
                ? new SupabaseAuthClient(AppConfig.SupabaseUrl, AppConfig.SupabaseAnonKey)
                : new SupabaseAuthClient("http://localhost", "missing"));
        services.AddSingleton(typeof(FinanceApiClient), _ => AppConfig.HasSupabaseConfiguration ? new FinanceApiClient() : null!);
        services.AddSingleton(typeof(SettingsApiClient), _ => AppConfig.HasSupabaseConfiguration ? new SettingsApiClient() : null!);
        services.AddSingleton<SecureSessionStore>();
        services.AddSingleton<SecureGeminiSettingsStore>();
        services.AddSingleton<DisplayNameStore>();
        return services;
    }
}
