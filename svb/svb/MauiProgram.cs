using BeneditaUI.Services;
using BeneditaUI.ViewModels;
using BeneditaUI.Views;
using Microsoft.Extensions.Logging;

namespace BeneditaUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",  "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── HTTP / API ────────────────────────────────────────
        builder.Services.AddHttpClient<ApiService>(client =>
        {
            client.BaseAddress = new Uri(
                Preferences.Get("ApiBaseUrl", "http://localhost:5000/"));
            client.Timeout = TimeSpan.FromSeconds(40);
        });

        // ── ViewModels ────────────────────────────────────────
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<VotersViewModel>();
        builder.Services.AddTransient<EntitiesViewModel>();
        builder.Services.AddTransient<VotingViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ── Pages ─────────────────────────────────────────────
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<VotersPage>();
        builder.Services.AddTransient<EntitiesPage>();
        builder.Services.AddTransient<VotingPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
