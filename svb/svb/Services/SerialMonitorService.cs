using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;

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
    private readonly StringBuilder _rxBuffer = new();

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
                ReadTimeout  = SerialPort.InfiniteTimeout,
                WriteTimeout = 500,
                NewLine      = "\n"
            };
            _port.Open();
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
            _rxBuffer.Clear();

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
        _cts?.Dispose();
        _cts = null;

        try
        {
            if (_port is { IsOpen: true })
                _port.Close();
        }
        catch
        {
            // Ignora erros de fecho para não derrubar a UI.
        }

        _port?.Dispose();
        _port = null;
        _rxBuffer.Clear();
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
                if (_port is not { IsOpen: true })
                    break;

                if (_port.BytesToRead == 0)
                {
                    await Task.Delay(20, token);
                    continue;
                }

                var chunk = _port.ReadExisting();
                if (string.IsNullOrEmpty(chunk))
                {
                    await Task.Delay(10, token);
                    continue;
                }

                _rxBuffer.Append(chunk);

                while (TryExtractLine(out var line))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    AddEntry(line, SerialDirection.Rx);
                    LineReceived?.Invoke(line);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                AddEntry($"[ERROR] {ex.Message}", SerialDirection.System);
                break;
            }
        }
    }

    private bool TryExtractLine(out string line)
    {
        line = string.Empty;
        if (_rxBuffer.Length == 0)
            return false;

        for (int i = 0; i < _rxBuffer.Length; i++)
        {
            if (_rxBuffer[i] != '\n')
                continue;

            line = _rxBuffer.ToString(0, i).Trim('\r', '\n', ' ');
            _rxBuffer.Remove(0, i + 1);
            return true;
        }

        return false;
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
