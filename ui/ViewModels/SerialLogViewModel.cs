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
        AvailablePorts = new(SerialMonitorService.AvailablePorts);
        SelectedPort   = AvailablePorts.FirstOrDefault() ?? "COM3";
        Log            = _serial.Log;
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
    public void Connect()
    {
        if (_serial.IsOpen)
        {
            _serial.Close();
            IsOpen        = false;
            StatusMessage = "Desconectado";
            return;
        }

        var (ok, err) = _serial.Open(SelectedPort, SelectedBaud);
        IsOpen        = ok;
        StatusMessage = ok ? $"Conectado em {SelectedPort}" : $"Erro: {err}";
    }

    // ── Send manual command ───────────────────────────────────

    [RelayCommand]
    public void SendCommand()
    {
        if (string.IsNullOrWhiteSpace(ManualCommand)) return;
        _serial.Send(ManualCommand.Trim());
        ManualCommand = string.Empty;
    }

    // ── Ping ─────────────────────────────────────────────────

    [RelayCommand]
    public void Ping() => _serial.Send("CMD:PING");

    // ── Refresh port list ─────────────────────────────────────

    [RelayCommand]
    public void RefreshPorts()
    {
        AvailablePorts = new(SerialMonitorService.AvailablePorts);
        if (AvailablePorts.Count > 0 && !AvailablePorts.Contains(SelectedPort))
            SelectedPort = AvailablePorts[0];
    }

    // ── Clear log ─────────────────────────────────────────────

    [RelayCommand]
    public void ClearLog() => Log.Clear();
}
