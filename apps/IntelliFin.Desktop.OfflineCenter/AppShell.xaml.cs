using IntelliFin.Desktop.OfflineCenter.Views;

namespace IntelliFin.Desktop.OfflineCenter;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("dashboard", typeof(DashboardPage));
        Routing.RegisterRoute("loans", typeof(LoansPage));
        Routing.RegisterRoute("financial", typeof(FinancialPage));
        Routing.RegisterRoute("reports", typeof(ReportsPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }
}
