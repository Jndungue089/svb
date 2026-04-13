using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeneditaUI.ViewModels;

public partial class SerialLogViewModel : ObservableObject
{
    private readonly SerialMonitorService _serial;

    public SerialLogViewModel(SerialMonitorService serial)
    {
        _serial = serial;
        AvailablePorts = new();
        SelectedPort   = "COM3";
        Log            = _serial.Log;
        _ = LoadPortsAsync().ContinueWith(
            t => StatusMessage = $"Erro ao carregar portas: {t.Exception?.GetBaseException().Message}",
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public System.Collections.ObjectModel.ObservableCollection<SerialEntry> Log { get; }

    [ObservableProperty]
    private List<string> _availablePorts;

    [ObservableProperty]
    private string _selectedPort;

    [ObservableProperty]
    private int _selectedBaud = 115200;

    public List<int> BaudRates { get; } = new() { 9600, 19200, 38400, 57600, 115200 };

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _manualCommand = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Desconectado";

    // ── Connect / Disconnect ──────────────────────────────────

    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (_serial.IsOpen)
        {
            await _serial.CloseAsync();
            IsOpen        = false;
            StatusMessage = "Desconectado";
            return;
        }

        var (ok, err) = await _serial.OpenAsync(SelectedPort, SelectedBaud);
        IsOpen        = _serial.IsOpen;
        StatusMessage = ok ? $"Conectado em {SelectedPort}" : $"Erro: {err}";
    }

    // ── Send manual command ───────────────────────────────────

    [RelayCommand]
    public async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualCommand) || !_serial.IsOpen) return;
        await _serial.SendAsync(ManualCommand.Trim());
        ManualCommand = string.Empty;
    }

    // ── Ping ─────────────────────────────────────────────────

    [RelayCommand]
    public async Task PingAsync() => await _serial.SendAsync("CMD:PING");

    // ── Refresh port list ─────────────────────────────────────

    [RelayCommand]
    public async Task RefreshPortsAsync()
    {
        var ports = await _serial.GetAvailablePortsAsync();
        AvailablePorts = new(ports);
        if (AvailablePorts.Count > 0 && !AvailablePorts.Contains(SelectedPort))
            SelectedPort = AvailablePorts[0];
    }

    // ── Clear log ─────────────────────────────────────────────

    [RelayCommand]
    public void ClearLog() => Log.Clear();

    // ── Private helpers ───────────────────────────────────────

    private async Task LoadPortsAsync()
    {
        var ports = await _serial.GetAvailablePortsAsync();
        AvailablePorts = new(ports);
        SelectedPort   = AvailablePorts.FirstOrDefault() ?? "COM3";
    }
}
