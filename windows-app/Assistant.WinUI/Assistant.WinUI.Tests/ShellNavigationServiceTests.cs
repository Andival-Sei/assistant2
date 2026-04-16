using Assistant.WinUI.Application.Shell;

namespace Assistant.WinUI.Tests;

[TestClass]
public sealed class ShellNavigationServiceTests
{
    [TestMethod]
    public void SetSection_ResetsActiveSubsection_ToDefault()
    {
        var service = new ShellNavigationService();
        service.SetSection(DashboardSection.Finance);
        service.SetSubsection("analytics");

        service.SetSection(DashboardSection.Settings);

        Assert.AreEqual(DashboardSection.Settings, service.Current.Section);
        Assert.AreEqual("profile", service.Current.ActiveSubsection);
    }

    [TestMethod]
    public void SetLanguage_PreservesActiveSubsection_WhenKeyExists()
    {
        var service = new ShellNavigationService();
        service.SetSection(DashboardSection.Tasks);
        service.SetSubsection("board");

        service.SetLanguage(false);

        Assert.IsFalse(service.Current.IsRussian);
        Assert.AreEqual("board", service.Current.ActiveSubsection);
    }

    [TestMethod]
    public void SetCompact_UpdatesShellState()
    {
        var service = new ShellNavigationService();

        service.SetCompact(true);

        Assert.IsTrue(service.Current.IsCompact);
    }
}
