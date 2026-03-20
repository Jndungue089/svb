using System.IO.Ports;
using BeneditaApi.Services;

namespace BeneditaApi.Services;

/// <summary>
/// Background service que mantém a comunicação serial com o ESP32.
///
/// Protocolo (ASCII, terminado em '\n'):
///   ESP32 → API  :  CMD:AUTH:<finger_id>
///   API   → ESP32:  RES:AUTH:OK   |  RES:AUTH:DENIED:<motivo>
///
///   ESP32 → API  :  CMD:VOTE:<finger_id>:<opcao>
///   API   → ESP32:  RES:VOTE:OK   |  RES:VOTE:ERROR:<motivo>
///
///   ESP32 → API  :  CMD:PING
///   API   → ESP32:  RES:PONG
/// </summary>
public class SerialHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration       _config;
    private readonly ILogger<SerialHostedService> _logger;

    private SerialPort? _port;

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
        var portName  = _config["Serial:Port"]     ?? "/dev/ttyUSB0";
        var baudRate  = int.Parse(_config["Serial:BaudRate"] ?? "115200");

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
                        try
                        {
                            line = _port.ReadLine().Trim();
                        }
                        catch (TimeoutException)
                        {
                            continue; // nenhuma mensagem, aguarda
                        }

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        _logger.LogDebug("Serial RX: {Line}", line);
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
    //  Processamento de comandos
    // ──────────────────────────────────────────────────────────

    private async Task<string?> ProcessCommandAsync(string line)
    {
        // CMD:PING
        if (line == "CMD:PING")
            return "RES:PONG";

        var parts = line.Split(':');
        if (parts.Length < 3 || parts[0] != "CMD")
        {
            _logger.LogWarning("Serial: linha inválida '{Line}'", line);
            return "RES:ERROR:FORMATO_INVALIDO";
        }

        var command = parts[1];

        // ── AUTH ──────────────────────────────────────────────
        if (command == "AUTH")
        {
            if (!int.TryParse(parts[2], out int fingerId))
                return "RES:AUTH:DENIED:ID_INVALIDO";

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<VoteService>();
            var (authorized, reason) = await svc.AuthorizeAsync(fingerId);

            return authorized
                ? "RES:AUTH:OK"
                : $"RES:AUTH:DENIED:{Sanitize(reason)}";
        }

        // ── VOTE ──────────────────────────────────────────────
        if (command == "VOTE")
        {
            if (parts.Length < 4)
                return "RES:VOTE:ERROR:FORMATO_INVALIDO";

            if (!int.TryParse(parts[2], out int fingerId))
                return "RES:VOTE:ERROR:ID_INVALIDO";

            var option = parts[3];

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<VoteService>();
            var (success, message) = await svc.CastVoteAsync(fingerId, option);

            return success
                ? "RES:VOTE:OK"
                : $"RES:VOTE:ERROR:{Sanitize(message)}";
        }

        return $"RES:ERROR:COMANDO_DESCONHECIDO:{command}";
    }

    // ──────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>Remove caracteres que quebram o protocolo serial.</summary>
    private static string Sanitize(string s) =>
        s.Replace('\n', ' ').Replace('\r', ' ').Replace(':', '-');

    /// <summary>
    /// Permite que controllers HTTP enviem mensagens proativas ao ESP32.
    /// (ex: confirmação de cadastro)
    /// </summary>
    public void Send(string message)
    {
        if (_port is { IsOpen: true })
        {
            _port.WriteLine(message);
            _logger.LogDebug("Serial TX (manual): {Message}", message);
        }
    }
}
