using IntelliFin.Desktop.OfflineCenter.ViewModels;

namespace IntelliFin.Desktop.OfflineCenter.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
