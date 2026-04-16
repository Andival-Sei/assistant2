namespace Assistant.WinUI.Application.Shell;

public interface IShellNavigationService
{
    ShellState Current { get; }

    ShellSectionConfig GetCurrentConfig();

    ShellSectionConfig GetConfig(DashboardSection section, bool isRussian);

    void SetSection(DashboardSection section);

    void SetSubsection(string key);

    void SetCompact(bool isCompact);

    void SetLanguage(bool isRussian);
}
