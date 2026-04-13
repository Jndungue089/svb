using System.Collections.ObjectModel;

namespace BeneditaUI.Services;

/// <summary>
/// Serviço singleton que monitora a comunicação serial indiretamente,
/// através do backend (API). Não abre a porta serial diretamente —
/// toda a comunicação com o ESP32 é responsabilidade exclusiva da API.
/// </summary>
public class SerialMonitorService : IDisposable
{
    private readonly ApiService _api;
    private CancellationTokenSource? _cts;
    private long _lastPolledUnixMs;

    public ObservableCollection<SerialEntry> Log { get; } = new();

    public bool IsOpen { get; private set; }

    public SerialMonitorService(ApiService api) => _api = api;

    // ── Abrir (conectar via backend) ──────────────────────────

    public async Task<(bool Ok, string Error)> OpenAsync(string portName, int baud = 115200)
    {
        await CloseAsync();

        SyncApiUrl();
        var (ok, message) = await _api.ConnectSerialAsync(portName, baud);

        if (!ok)
        {
            AddEntry($"[ERROR] {message}", SerialDirection.System);
            return (false, message);
        }

        IsOpen = true;
        // Start 2 seconds back so the connection handshake appears in the log.
        _lastPolledUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 2_000;
        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(_cts.Token);

        AddEntry($"[CONNECTED] {portName} @ {baud}", SerialDirection.System);
        return (true, string.Empty);
    }

    // ── Fechar (desconectar via backend) ──────────────────────

    public async Task CloseAsync()
    {
        var wasOpen = IsOpen;
        IsOpen = false;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (wasOpen)
        {
            SyncApiUrl();
            await _api.DisconnectSerialAsync();
            AddEntry("[DISCONNECTED]", SerialDirection.System);
        }
    }

    // ── Enviar comando via backend ────────────────────────────

    public async Task SendAsync(string line)
    {
        if (!IsOpen) return;
        SyncApiUrl();
        await _api.SendSerialRawAsync(line);
    }

    // ── Listar portas disponíveis (via backend) ───────────────

    public async Task<string[]> GetAvailablePortsAsync()
    {
        SyncApiUrl();
        var ports = await _api.GetSerialPortsAsync() ?? new List<string>();
        return ports.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ── Loop de polling do log ────────────────────────────────

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var entries = await _api.GetSerialLogAsync(_lastPolledUnixMs);
                if (entries is not null)
                {
                    foreach (var e in entries)
                    {
                        var dir = e.Direction switch
                        {
                            "Tx"     => SerialDirection.Tx,
                            "Rx"     => SerialDirection.Rx,
                            _        => SerialDirection.System
                        };
                        var time = DateTimeOffset.FromUnixTimeMilliseconds(e.UnixMs).LocalDateTime;
                        AddEntry(e.Text, dir, time);
                        if (e.UnixMs >= _lastPolledUnixMs)
                            _lastPolledUnixMs = e.UnixMs + 1;
                    }
                }

                await Task.Delay(250, token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                AddEntry($"[ERROR] {ex.Message}", SerialDirection.System);
                await Task.Delay(500, token);
            }
        }
    }

    // ── Helper ────────────────────────────────────────────────

    private void SyncApiUrl()
    {
        var url = Preferences.Get("ApiBaseUrl", "http://localhost:5000/");
        _api.SetBaseUrl(url);
    }

    private void AddEntry(string text, SerialDirection dir, DateTime? time = null)
    {
        var entry = new SerialEntry(time ?? DateTime.Now, text, dir);
        MainThread.InvokeOnMainThreadAsync(() => Log.Add(entry));
    }

    public void Dispose()
    {
        IsOpen = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}

public record SerialEntry(DateTime Time, string Text, SerialDirection Direction)
{
    public string TimeLabel => Time.ToString("HH:mm:ss.fff");
    public Color  Color     => Direction switch
    {
        SerialDirection.Rx     => Colors.LightGreen,
        SerialDirection.Tx     => Colors.DodgerBlue,
        SerialDirection.System => Colors.Orange,
        _                      => Colors.White
    };
    public string Prefix => Direction switch
    {
        SerialDirection.Rx     => "← RX",
        SerialDirection.Tx     => "→ TX",
        SerialDirection.System => "ℹ  SYS",
        _                      => "   "
    };
}

public enum SerialDirection { Rx, Tx, System }
