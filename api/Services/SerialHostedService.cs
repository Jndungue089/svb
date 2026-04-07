using System.IO.Ports;
using BeneditaApi.Data;
using BeneditaApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BeneditaApi.Services;

/// <summary>
/// Background service que mantém a comunicação serial com o ESP32.
///
/// Protocolo (ASCII, terminado em '\n'):
///   ESP32 → API  :  CMD:AUTH:{finger_id}
///   API   → ESP32:  RES:AUTH:OK:{nome}  |  RES:AUTH:DENIED:{motivo}
///
///   ESP32 → API  :  CMD:VOTE:{finger_id}:{entity_id}
///   API   → ESP32:  RES:VOTE:OK  |  RES:VOTE:ERROR:{motivo}
///
///   ESP32 → API  :  CMD:ENTITIES
///   API   → ESP32:  RES:ENTITIES:{n}|{id}:{sigla}|...
///
///   ESP32 → API  :  CMD:PING
///   API   → ESP32:  RES:PONG
///
///   API   → ESP32:  CMD:ENROLL:{slot}
///   ESP32 → API  :  RES:ENROLL:OK:{slot}  |  RES:ENROLL:ERROR:{motivo}
///
///   API   → ESP32:  CMD:VOTE_SCAN:{entity_id}     (votação iniciada pelo admin)
///   ESP32 → API  :  RES:VOTE_SCAN:OK:{nome}        (voto registado, nome do eleitor)
///   ESP32 → API  :  RES:VOTE_SCAN:ERROR:{motivo}
/// </summary>
public class SerialHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration       _config;
    private readonly ILogger<SerialHostedService> _logger;

    private SerialPort? _port;

    private TaskCompletionSource<string>? _enrollTcs;
    private TaskCompletionSource<string>? _voteScanTcs;

    public SerialHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<SerialHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config       = config;
        _logger       = logger;
    }

    // ──────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var portName = _config["Serial:Port"]    ?? "COM3";
        var baudRate = int.Parse(_config["Serial:BaudRate"] ?? "115200");

        _logger.LogInformation("Serial: abrindo {Port} @ {Baud}", portName, baudRate);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (_port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout  = 200,
                    WriteTimeout = 500,
                    NewLine      = "\n"
                })
                {
                    _port.Open();
                    _logger.LogInformation("Serial: porta aberta");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        string line;
                        try { line = _port.ReadLine().Trim(); }
                        catch (TimeoutException) { continue; }

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        _logger.LogDebug("Serial RX: {Line}", line);

                        // Respostas de operações assíncronas vindas do ESP32
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

                        var response = await ProcessCommandAsync(line);
                        if (response is not null)
                        {
                            _port.WriteLine(response);
                            _logger.LogDebug("Serial TX: {Response}", response);
                        }
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Serial: erro na porta — tentando novamente em 5 s");
                await Task.Delay(5_000, stoppingToken);
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Processamento de comandos recebidos do ESP32
    // ──────────────────────────────────────────────────────────

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

        // ── AUTH ──────────────────────────────────────────────
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

        // ── VOTE ──────────────────────────────────────────────
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

        // ── ENTITIES ──────────────────────────────────────────
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

    // ──────────────────────────────────────────────────────────
    //  Enrolamento biométrico
    // ──────────────────────────────────────────────────────────

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

    public async Task<string> SendEnrollAsync(int slot, CancellationToken ct = default)
    {
        if (_port is not { IsOpen: true })
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

    // ──────────────────────────────────────────────────────────
    //  Votação iniciada pelo MAUI (VOTE_SCAN)
    // ──────────────────────────────────────────────────────────

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

    /// <summary>
    /// Envia CMD:VOTE_SCAN:{entityId} ao ESP32 e aguarda o resultado (até 35 s).
    /// O ESP32 lê o dedo, autentica e regista o voto automaticamente.
    /// Retorna RES:VOTE_SCAN:OK:{nomeEleitor} ou RES:VOTE_SCAN:ERROR:{motivo}.
    /// </summary>
    public async Task<string> SendVoteScanAsync(int entityId, CancellationToken ct = default)
    {
        if (_port is not { IsOpen: true })
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

    // ──────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────

    private static string Sanitize(string s) =>
        s.Replace('\n', ' ').Replace('\r', ' ').Replace(':', '-');

    public void Send(string message)
    {
        if (_port is { IsOpen: true })
        {
            _port.WriteLine(message);
            _logger.LogDebug("Serial TX (manual): {Message}", message);
        }
    }
}
