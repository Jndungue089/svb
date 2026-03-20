using BeneditaUI.Views;

namespace BeneditaUI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("dashboard", typeof(DashboardPage));
        Routing.RegisterRoute("voters",    typeof(VotersPage));
        Routing.RegisterRoute("serial",    typeof(SerialLogPage));
        Routing.RegisterRoute("settings",  typeof(SettingsPage));
    }
}
