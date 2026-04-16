using Assistant.WinUI.Application.Abstractions;

namespace Assistant.WinUI.Infrastructure;

internal sealed class LocalizationService : ILocalizationService
{
    public bool IsRussian { get; private set; } = true;

    public void SetLanguage(bool isRussian)
    {
        IsRussian = isRussian;
    }
}
