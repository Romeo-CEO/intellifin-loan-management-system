using IntelliFin.Desktop.OfflineCenter.Views;

namespace IntelliFin.Desktop.OfflineCenter;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);

        const int newWidth = 1400;
        const int newHeight = 900;

        window.Width = newWidth;
        window.Height = newHeight;
        window.MinimumWidth = 1200;
        window.MinimumHeight = 800;
        window.Title = "IntelliFin CEO Offline Command Center";

        return window;
    }
}
