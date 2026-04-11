using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private string _baudRateInput = "115200";

    [ObservableProperty]
    private bool _isSerialBusy;

    [ObservableProperty]
    private bool _isSerialConnected;

    [ObservableProperty]
    private string _serialStatus = "Serial não conectado.";

    [ObservableProperty]
    private Color _serialStatusColor = Colors.Gray;

    public bool CanConnectSerial =>
        !IsSerialBusy &&
        !string.IsNullOrWhiteSpace(SelectedPort);

    public bool CanDisconnectSerial =>
        !IsSerialBusy &&
        IsSerialConnected;

    partial void OnIsSerialBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConnectSerial));
        OnPropertyChanged(nameof(CanDisconnectSerial));
    }

    partial void OnSelectedPortChanged(string? value)
    {
        OnPropertyChanged(nameof(CanConnectSerial));
    }

    partial void OnIsSerialConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDisconnectSerial));
    }

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

    [RelayCommand]
    public async Task LoadSerialAsync()
    {
        IsSerialBusy = true;

        var ports = await _api.GetSerialPortsAsync() ?? new List<string>();
        AvailablePorts.Clear();
        foreach (var port in ports.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            AvailablePorts.Add(port);

        var status = await _api.GetSerialStatusAsync();
        if (status is null)
        {
            IsSerialConnected = false;
            SerialStatus = "Não foi possível consultar estado da serial no backend.";
            SerialStatusColor = Colors.Crimson;
            IsSerialBusy = false;
            return;
        }

        IsSerialConnected = status.IsConnected;

        if (!string.IsNullOrWhiteSpace(status.DesiredPort))
            SelectedPort = status.DesiredPort;
        else if (AvailablePorts.Count > 0 && string.IsNullOrWhiteSpace(SelectedPort))
            SelectedPort = AvailablePorts[0];

        BaudRateInput = status.BaudRate > 0 ? status.BaudRate.ToString() : "115200";

        if (status.IsConnected)
        {
            SerialStatus = $"Conectado em {status.ActivePort} @ {status.BaudRate}.";
            SerialStatusColor = Colors.SeaGreen;
        }
        else if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            SerialStatus = $"Desconectado. Erro: {status.LastError}";
            SerialStatusColor = Colors.Crimson;
        }
        else
        {
            SerialStatus = "Serial desconectado. Selecione a COM e conecte.";
            SerialStatusColor = Colors.DarkOrange;
        }

        IsSerialBusy = false;
    }

    [RelayCommand]
    public async Task ConnectSerialAsync()
    {
        if (!CanConnectSerial)
            return;

        if (!int.TryParse(BaudRateInput.Trim(), out int baudRate) || baudRate <= 0)
        {
            SerialStatus = "Baud rate inválido. Use um número como 115200.";
            SerialStatusColor = Colors.Crimson;
            return;
        }

        IsSerialBusy = true;
        var (ok, message) = await _api.ConnectSerialAsync(SelectedPort!, baudRate);

        SerialStatus = message;
        SerialStatusColor = ok ? Colors.DodgerBlue : Colors.Crimson;

        IsSerialBusy = false;
        await LoadSerialAsync();
    }

    [RelayCommand]
    public async Task DisconnectSerialAsync()
    {
        if (!CanDisconnectSerial)
            return;

        IsSerialBusy = true;
        var (ok, message) = await _api.DisconnectSerialAsync();
        SerialStatus = message;
        SerialStatusColor = ok ? Colors.DarkOrange : Colors.Crimson;
        IsSerialBusy = false;

        await LoadSerialAsync();
    }
}
