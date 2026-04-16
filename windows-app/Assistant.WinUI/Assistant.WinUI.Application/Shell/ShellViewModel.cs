using CommunityToolkit.Mvvm.ComponentModel;

namespace Assistant.WinUI.Application.Shell;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IShellNavigationService _navigationService;

    [ObservableProperty]
    private DashboardSection section;

    [ObservableProperty]
    private string activeSubsection;

    [ObservableProperty]
    private bool isCompact;

    [ObservableProperty]
    private bool isRussian;

    public ShellViewModel(IShellNavigationService navigationService)
    {
        _navigationService = navigationService;
        var state = _navigationService.Current;
        section = state.Section;
        activeSubsection = state.ActiveSubsection;
        isCompact = state.IsCompact;
        isRussian = state.IsRussian;
    }

    public ShellSectionConfig CurrentConfig => _navigationService.GetCurrentConfig();

    public void SetSection(DashboardSection nextSection)
    {
        _navigationService.SetSection(nextSection);
        Sync();
    }

    public void SetSubsection(string subsection)
    {
        _navigationService.SetSubsection(subsection);
        Sync();
    }

    public void SetCompact(bool compact)
    {
        _navigationService.SetCompact(compact);
        Sync();
    }

    public void SetLanguage(bool russian)
    {
        _navigationService.SetLanguage(russian);
        Sync();
    }

    public ShellSectionConfig GetSectionConfig(DashboardSection section) => _navigationService.GetConfig(section, IsRussian);

    private void Sync()
    {
        var state = _navigationService.Current;
        Section = state.Section;
        ActiveSubsection = state.ActiveSubsection;
        IsCompact = state.IsCompact;
        IsRussian = state.IsRussian;
        OnPropertyChanged(nameof(CurrentConfig));
    }
}
