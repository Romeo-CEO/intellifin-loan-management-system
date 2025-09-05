using IntelliFin.Desktop.OfflineCenter.ViewModels;

namespace IntelliFin.Desktop.OfflineCenter.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
