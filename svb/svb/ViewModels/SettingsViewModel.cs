using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeneditaUI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiService _api;

    public SettingsViewModel(ApiService api)
    {
        _api   = api;
        ApiUrl = _api.CurrentBaseUrl;
    }

    [ObservableProperty]
    private string _apiUrl;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _pingResult = string.Empty;

    [ObservableProperty]
    private Color _pingColor = Colors.Gray;

    [RelayCommand]
    public void SaveUrl()
    {
        if (!string.IsNullOrWhiteSpace(ApiUrl))
        {
            _api.SetBaseUrl(ApiUrl);
            PingResult = "URL guardada!";
            PingColor  = Colors.SeaGreen;
        }
    }

    [RelayCommand]
    public async Task TestConnAsync()
    {
        IsTesting = true;
        PingResult = "A testar...";
        PingColor  = Colors.Orange;

        _api.SetBaseUrl(ApiUrl);
        bool ok = await _api.PingAsync();

        PingResult = ok ? "✅ API Online" : "❌ API Offline / Inalcançável";
        PingColor  = ok ? Colors.SeaGreen : Colors.Crimson;
        IsTesting  = false;
    }
}
