using IntelliFin.Desktop.OfflineCenter.Data;
using IntelliFin.Desktop.OfflineCenter.Services;
using IntelliFin.Desktop.OfflineCenter.ViewModels;
using IntelliFin.Desktop.OfflineCenter.Views;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace IntelliFin.Desktop.OfflineCenter;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Add services
        builder.Services.AddMauiBlazorWebView();

        // Database
        builder.Services.AddSingleton<OfflineDbContext>();

        // Services
        builder.Services.AddSingleton<IOfflineDataService, OfflineDataService>();
        builder.Services.AddSingleton<IFinancialApiService, FinancialApiService>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

        // ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<LoansViewModel>();
        builder.Services.AddTransient<FinancialViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<LoansPage>();
        builder.Services.AddTransient<FinancialPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Services.AddLogging(configure => configure.AddDebug());
#endif

        return builder.Build();
    }
}
