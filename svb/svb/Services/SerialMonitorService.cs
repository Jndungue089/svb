using System.Collections.ObjectModel;
using System.IO.Ports;

namespace BeneditaUI.Services;

/// <summary>
/// Serviço singleton que monitora a porta serial diretamente da UI.
/// Permite visualizar o protocolo ESP32 ↔ API em tempo real
/// e também enviar comandos manuais.
/// </summary>
public class SerialMonitorService : IDisposable
{
    private SerialPort? _port;
    private CancellationTokenSource? _cts;

    public ObservableCollection<SerialEntry> Log { get; } = new();

    public bool IsOpen => _port?.IsOpen ?? false;

    public event Action<string>? LineReceived;

    // ── Abrir porta ───────────────────────────────────────────

    public (bool Ok, string Error) Open(string portName, int baud = 115200)
    {
        try
        {
            Close();
            _port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
            {
                ReadTimeout  = 300,
                WriteTimeout = 500,
                NewLine      = "\n"
            };
            _port.Open();

            _cts = new CancellationTokenSource();
            _ = ReadLoopAsync(_cts.Token);

            AddEntry($"[CONNECTED] {portName} @ {baud}", SerialDirection.System);
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Fechar porta ──────────────────────────────────────────

    public void Close()
    {
        _cts?.Cancel();
        _port?.Close();
        _port?.Dispose();
        _port = null;
        AddEntry("[DISCONNECTED]", SerialDirection.System);
    }

    // ── Enviar ────────────────────────────────────────────────

    public void Send(string line)
    {
        if (_port is { IsOpen: true })
        {
            _port.WriteLine(line);
            AddEntry(line, SerialDirection.Tx);
        }
    }

    // ── Loop de leitura ───────────────────────────────────────

    private async Task ReadLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && (_port?.IsOpen ?? false))
        {
            try
            {
                var line = await Task.Run(() => _port!.ReadLine(), token);
                AddEntry(line.Trim(), SerialDirection.Rx);
                LineReceived?.Invoke(line.Trim());
            }
            catch (TimeoutException) { }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                AddEntry($"[ERROR] {ex.Message}", SerialDirection.System);
                break;
            }
        }
    }

    // ── Helper ────────────────────────────────────────────────

    private void AddEntry(string text, SerialDirection dir)
    {
        var entry = new SerialEntry(DateTime.Now, text, dir);
        MainThread.InvokeOnMainThreadAsync(() => Log.Add(entry));
    }

    public void Dispose() => Close();

    // ── Listar portas disponíveis ─────────────────────────────
    public static string[] AvailablePorts => SerialPort.GetPortNames();
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
