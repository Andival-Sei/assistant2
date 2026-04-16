namespace Assistant.WinUI.Application.Abstractions;

public interface ILocalizationService
{
    bool IsRussian { get; }

    void SetLanguage(bool isRussian);
}
