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
                // Copy OpenSans fonts from a MAUI template into Resources/Fonts/
                // or replace with any other font files you prefer.
                fonts.AddFont("OpenSans-Regular.ttf",    "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf",   "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── HTTP / API ────────────────────────────────────────
        builder.Services.AddHttpClient<ApiService>(client =>
        {
            client.BaseAddress = new Uri(
                Preferences.Get("ApiBaseUrl", "http://localhost:5000/"));
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // ── Serial ────────────────────────────────────────────
        builder.Services.AddSingleton<SerialMonitorService>();

        // ── ViewModels ────────────────────────────────────────
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<VotersViewModel>();
        builder.Services.AddTransient<SerialLogViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ── Pages ─────────────────────────────────────────────
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<VotersPage>();
        builder.Services.AddTransient<SerialLogPage>();
        builder.Services.AddTransient<SettingsPage>();

        // ── Shell + App ────────────────────────────────────────
        // App is registered by UseMauiApp<App>()
        // AppShell is created directly in App constructor after resources are loaded

        return builder.Build();
    }
}
