using System;
using System.Linq;

namespace Assistant.WinUI.Application.Shell;

public sealed class ShellNavigationService : IShellNavigationService
{
    private ShellState _current = new(DashboardSection.Home, "summary", false, true);

    public ShellState Current => _current;

    public ShellSectionConfig GetCurrentConfig() => GetConfig(_current.Section, _current.IsRussian);

    public ShellSectionConfig GetConfig(DashboardSection section, bool isRussian)
    {
        var catalog = ShellNavigationCatalog.Create(isRussian);
        return catalog[section.ToString()];
    }

    public void SetSection(DashboardSection section)
    {
        var config = GetConfig(section, _current.IsRussian);
        _current = _current with
        {
            Section = section,
            ActiveSubsection = config.DefaultSubsection
        };
    }

    public void SetSubsection(string key)
    {
        var config = GetCurrentConfig();
        var next = config.Subsections.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (next is null)
        {
            return;
        }

        _current = _current with { ActiveSubsection = next.Key };
    }

    public void SetCompact(bool isCompact)
    {
        _current = _current with { IsCompact = isCompact };
    }

    public void SetLanguage(bool isRussian)
    {
        if (_current.IsRussian == isRussian)
        {
            return;
        }

        var config = GetConfig(_current.Section, isRussian);
        _current = _current with
        {
            IsRussian = isRussian,
            ActiveSubsection = NormalizeSubsection(_current.ActiveSubsection, config)
        };
    }

    private static string NormalizeSubsection(string activeSubsection, ShellSectionConfig config)
    {
        return config.Subsections.Any(item => string.Equals(item.Key, activeSubsection, StringComparison.OrdinalIgnoreCase))
            ? activeSubsection
            : config.DefaultSubsection;
    }
}
