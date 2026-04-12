using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Text;
#if WINDOWS
using System.Management;
using System.Text.RegularExpressions;
#endif

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
            if (string.IsNullOrWhiteSpace(portName))
                return (false, "Selecione uma porta COM válida.");

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
        catch (UnauthorizedAccessException)
        {
            const string message = "Porta ocupada/permissão negada. Feche Arduino IDE, monitor serial ou backend que esteja usando esta COM e tente novamente.";
            AddEntry($"[ERROR] {message}", SerialDirection.System);
            return (false, message);
        }
        catch (IOException ex)
        {
            var message = $"Falha de E/S ao abrir a porta: {ex.Message}";
            AddEntry($"[ERROR] {message}", SerialDirection.System);
            return (false, message);
        }
        catch (Exception ex)
        {
            AddEntry($"[ERROR] {ex.Message}", SerialDirection.System);
            return (false, ex.Message);
        }
    }

    // ── Fechar porta ──────────────────────────────────────────

    public void Close()
    {
        var wasOpen = _port?.IsOpen ?? false;

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

        if (wasOpen)
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
    public static string[] AvailablePorts
    {
        get
        {
            var allPorts = SerialPort.GetPortNames()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (allPorts.Length == 0)
                return allPorts;

#if WINDOWS
            try
            {
                var likely = GetLikelyEspPorts(allPorts);
                if (likely.Length > 0)
                    return likely;
            }
            catch
            {
                // Em caso de falha no WMI, usa a lista completa sem bloquear a UI.
            }
#endif

            return allPorts;
        }
    }

#if WINDOWS
    private static string[] GetLikelyEspPorts(string[] allPorts)
    {
        var byPort = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var port in allPorts)
            byPort[port] = string.Empty;

        using var searcher = new ManagementObjectSearcher(
            "SELECT Caption FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");

        foreach (var item in searcher.Get())
        {
            var caption = item["Caption"]?.ToString() ?? string.Empty;
            var match = Regex.Match(caption, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            var com = match.Groups[1].Value;
            if (byPort.ContainsKey(com))
                byPort[com] = caption;
        }

        static bool IsLikelyEspCaption(string caption)
        {
            if (string.IsNullOrWhiteSpace(caption))
                return false;

            var c = caption.ToUpperInvariant();
            return c.Contains("USB") ||
                   c.Contains("UART") ||
                   c.Contains("CH340") ||
                   c.Contains("CH910") ||
                   c.Contains("CP210") ||
                   c.Contains("FTDI") ||
                   c.Contains("SILICON LABS") ||
                   c.Contains("ESP32") ||
                   c.Contains("ESPRESSIF");
        }

        return byPort
            .Where(kvp => IsLikelyEspCaption(kvp.Value))
            .Select(kvp => kvp.Key)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
#endif
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
