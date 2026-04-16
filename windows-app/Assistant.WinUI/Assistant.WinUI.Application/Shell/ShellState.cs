namespace Assistant.WinUI.Application.Shell;

public sealed record ShellState(
    DashboardSection Section,
    string ActiveSubsection,
    bool IsCompact,
    bool IsRussian);
