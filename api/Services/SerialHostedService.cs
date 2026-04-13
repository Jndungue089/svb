using System.Collections.Concurrent;
using System.IO.Ports;
using BeneditaApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BeneditaApi.Services;

/// <summary>
/// Background service que mantém a comunicação serial com o ESP32.
/// Agora a porta é configurável em runtime via API.
/// </summary>
public class SerialHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<SerialHostedService> _logger;

    private readonly object _stateLock = new();
    private SerialPort? _port;
    private string? _desiredPortName;
    private int _desiredBaudRate;
    private bool _reconnectRequested;
    private string _lastError = string.Empty;

    private readonly ConcurrentQueue<SerialLogEntry> _logBuffer = new();
    private const int MaxLogEntries = 500;

    private TaskCompletionSource<string>? _enrollTcs;
    private TaskCompletionSource<string>? _voteScanTcs;
    private TaskCompletionSource<string>? _identifyScanTcs;

    public SerialHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<SerialHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;

        _desiredPortName = _config["Serial:Port"];
        _desiredBaudRate = int.TryParse(_config["Serial:BaudRate"], out var baud) ? baud : 115200;
        _reconnectRequested = !string.IsNullOrWhiteSpace(_desiredPortName);

        if (string.IsNullOrWhiteSpace(_desiredPortName))
        {
            _desiredPortName = "AUTO";
            _reconnectRequested = true;
        }
    }

    public string[] GetAvailablePorts() =>
        SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

    public SerialStatus GetStatus()
    {
        lock (_stateLock)
        {
            return new SerialStatus(
                IsConnected: _port is { IsOpen: true },
                ActivePort: _port?.IsOpen == true ? _port.PortName : null,
                DesiredPort: _desiredPortName,
                BaudRate: _desiredBaudRate,
                LastError: _lastError);
        }
    }

    public (bool Ok, string Message) Connect(string portName, int baudRate)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return (false, "Porta COM inválida.");

        if (baudRate <= 0)
            return (false, "Baud rate inválido.");

        lock (_stateLock)
        {
            _desiredPortName = portName.Trim();
            _desiredBaudRate = baudRate;
            _reconnectRequested = true;
            _lastError = string.Empty;
            ClosePortUnsafe();
        }

        TryEnsureConnection();

        var status = GetStatus();
        if (status.IsConnected)
            return (true, $"Conectado em {status.ActivePort} @ {status.BaudRate}.");

        var reason = string.IsNullOrWhiteSpace(status.LastError)
            ? "Não foi possível abrir a porta serial."
            : status.LastError;

        return (false, $"Falha ao conectar: {reason}");
    }

    public void Disconnect()
    {
        lock (_stateLock)
        {
            _desiredPortName = null;
            _reconnectRequested = false;
            ClosePortUnsafe();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            TryEnsureConnection();

            SerialPort? activePort;
            lock (_stateLock)
                activePort = _port is { IsOpen: true } ? _port : null;

            if (activePort is null)
            {
                await Task.Delay(250, stoppingToken);
                continue;
            }

            try
            {
                var line = activePort.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                _logger.LogDebug("Serial RX: {Line}", line);
                LogSerial(line, "Rx");

                if (line.StartsWith("RES:ENROLL:"))
                {
                    HandleEnrollResponse(line);
                    continue;
                }

                if (line.StartsWith("RES:VOTE_SCAN:"))
                {
                    HandleVoteScanResponse(line);
                    continue;
                }

                if (line.StartsWith("RES:IDENTIFY_SCAN:"))
                {
                    HandleIdentifyScanResponse(line);
                    continue;
                }

                var response = await ProcessCommandAsync(line);
                if (response is not null)
                    Send(response);
            }
            catch (TimeoutException)
            {
                // Timeout esperado para polling.
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Serial: erro durante leitura — reconectando.");
                lock (_stateLock)
                {
                    _lastError = ex.Message;
                    _reconnectRequested = true;
                    ClosePortUnsafe();
                }
            }
        }

        lock (_stateLock)
            ClosePortUnsafe();
    }

    private void TryEnsureConnection()
    {
        string? desiredPort;
        int desiredBaud;
        bool shouldReconnect;

        lock (_stateLock)
        {
            desiredPort = _desiredPortName;
            desiredBaud = _desiredBaudRate;
            shouldReconnect = _reconnectRequested;

            if (!shouldReconnect && _port is { IsOpen: true })
                return;

            if (string.IsNullOrWhiteSpace(desiredPort))
                return;
        }

        var availablePorts = GetAvailablePorts();
        var useAuto = string.Equals(desiredPort, "AUTO", StringComparison.OrdinalIgnoreCase);
        var candidates = useAuto
            ? availablePorts
            : new[] { desiredPort! };

        if (!useAuto && !availablePorts.Contains(desiredPort!, StringComparer.OrdinalIgnoreCase))
        {
            lock (_stateLock)
                _lastError = $"Porta {desiredPort} não encontrada. Disponíveis: {string.Join(", ", availablePorts)}";

            _logger.LogWarning("Serial: porta {Port} não encontrada. Disponíveis: {Ports}",
                desiredPort, string.Join(", ", availablePorts));
            return;
        }

        foreach (var candidate in candidates)
        {
            try
            {
                var newPort = new SerialPort(candidate, desiredBaud, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 200,
                    WriteTimeout = 500,
                    NewLine = "\n",
                    DtrEnable = true,
                    RtsEnable = false,
                    Handshake = Handshake.None
                };

                newPort.Open();
                newPort.DiscardInBuffer();
                newPort.DiscardOutBuffer();
                Thread.Sleep(250);

                lock (_stateLock)
                {
                    ClosePortUnsafe();
                    _port = newPort;
                    _reconnectRequested = false;
                    _lastError = string.Empty;
                }

                _logger.LogInformation("Serial: porta aberta em {Port} @ {Baud}", candidate, desiredBaud);
                return;
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                    _lastError = ex.Message;

                _logger.LogWarning(ex, "Serial: falha ao abrir {Port} @ {Baud}", candidate, desiredBaud);
            }
        }
    }

    private async Task<string?> ProcessCommandAsync(string line)
    {
        if (line == "CMD:PING")
            return "RES:PONG";

        var parts = line.Split(':');
        if (parts.Length < 2 || parts[0] != "CMD")
        {
            _logger.LogWarning("Serial: linha inválida '{Line}'", line);
            return null;
        }

        var command = parts[1];

        if (command == "AUTH")
        {
            if (parts.Length < 3 || !int.TryParse(parts[2], out int fingerId))
                return "RES:AUTH:DENIED:ID_INVALIDO";

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<VoteService>();
            var (authorized, reason, voterName) = await svc.AuthorizeAsync(fingerId);

            return authorized
                ? $"RES:AUTH:OK:{Sanitize(voterName)}"
                : $"RES:AUTH:DENIED:{Sanitize(reason)}";
        }

        if (command == "VOTE")
        {
            if (parts.Length < 4)
                return "RES:VOTE:ERROR:FORMATO_INVALIDO";

            if (!int.TryParse(parts[2], out int fingerId))
                return "RES:VOTE:ERROR:ID_INVALIDO";

            if (!int.TryParse(parts[3], out int entityId))
                return "RES:VOTE:ERROR:ENTIDADE_INVALIDA";

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<VoteService>();
            var (success, message) = await svc.CastVoteAsync(fingerId, entityId);

            return success
                ? "RES:VOTE:OK"
                : $"RES:VOTE:ERROR:{Sanitize(message)}";
        }

        if (command == "ENTITIES")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entities = await db.Entities.ToListAsync();

            if (!entities.Any())
                return "RES:ENTITIES:0";

            var list = string.Join("|", entities.Select(e => $"{e.Id}:{Sanitize(e.Acronym)}"));
            return $"RES:ENTITIES:{entities.Count}|{list}";
        }

        return $"RES:ERROR:COMANDO_DESCONHECIDO:{command}";
    }

    private void HandleEnrollResponse(string line)
    {
        var after = line["RES:ENROLL:".Length..];
        if (after.StartsWith("OK:") || after.StartsWith("ERROR:"))
        {
            _enrollTcs?.TrySetResult(line);
            _enrollTcs = null;
        }

        _logger.LogInformation("Enrolamento: {Line}", line);
    }

    private void HandleVoteScanResponse(string line)
    {
        var after = line["RES:VOTE_SCAN:".Length..];
        if (after.StartsWith("OK:") || after.StartsWith("ERROR:"))
        {
            _voteScanTcs?.TrySetResult(line);
            _voteScanTcs = null;
        }

        _logger.LogInformation("VoteScan: {Line}", line);
    }

    private void HandleIdentifyScanResponse(string line)
    {
        var after = line["RES:IDENTIFY_SCAN:".Length..];
        if (after.StartsWith("OK:") || after.StartsWith("ERROR:"))
        {
            _identifyScanTcs?.TrySetResult(line);
            _identifyScanTcs = null;
        }

        _logger.LogInformation("IdentifyScan: {Line}", line);
    }

    public async Task<string> SendEnrollAsync(int slot, CancellationToken ct = default)
    {
        if (!IsConnected())
            return "RES:ENROLL:ERROR:PORTA_SERIAL_FECHADA";

        _enrollTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send($"CMD:ENROLL:{slot}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            return await _enrollTcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _enrollTcs = null;
            return "RES:ENROLL:ERROR:TIMEOUT";
        }
    }

    public async Task<string> SendVoteScanAsync(int entityId, CancellationToken ct = default)
    {
        if (!IsConnected())
            return "RES:VOTE_SCAN:ERROR:PORTA_SERIAL_FECHADA";

        _voteScanTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send($"CMD:VOTE_SCAN:{entityId}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(35));

        try
        {
            return await _voteScanTcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _voteScanTcs = null;
            return "RES:VOTE_SCAN:ERROR:TIMEOUT";
        }
    }

    public async Task<string> SendIdentifyScanAsync(CancellationToken ct = default)
    {
        if (!IsConnected())
            return "RES:IDENTIFY_SCAN:ERROR:PORTA_SERIAL_FECHADA";

        _identifyScanTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send("CMD:IDENTIFY_SCAN");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(35));

        try
        {
            return await _identifyScanTcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _identifyScanTcs = null;
            return "RES:IDENTIFY_SCAN:ERROR:TIMEOUT";
        }
    }

    public void Send(string message)
    {
        lock (_stateLock)
        {
            if (_port is not { IsOpen: true })
                return;

            _port.WriteLine(message);
            _logger.LogDebug("Serial TX: {Message}", Sanitize(message));
        }
        LogSerial(message, "Tx");
    }

    private bool IsConnected()
    {
        lock (_stateLock)
            return _port is { IsOpen: true };
    }

    private void LogSerial(string text, string direction)
    {
        _logBuffer.Enqueue(new SerialLogEntry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), text, direction));
        int excess = _logBuffer.Count - MaxLogEntries;
        for (int i = 0; i < excess; i++)
            _logBuffer.TryDequeue(out _);
    }

    public SerialLogEntry[] GetLog(long? sinceUnixMs = null)
    {
        var entries = _logBuffer.ToArray();
        return sinceUnixMs.HasValue
            ? entries.Where(e => e.UnixMs > sinceUnixMs.Value).ToArray()
            : entries;
    }

    private static string Sanitize(string s) =>
        s.Replace('\n', ' ').Replace('\r', ' ').Replace(':', '-');

    private void ClosePortUnsafe()
    {
        try
        {
            if (_port is { IsOpen: true })
                _port.Close();
            _port?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Serial: erro ao fechar porta.");
        }
        finally
        {
            _port = null;
        }
    }
}

public record SerialStatus(bool IsConnected, string? ActivePort, string? DesiredPort, int BaudRate, string LastError);
public record SerialLogEntry(long UnixMs, string Text, string Direction);
