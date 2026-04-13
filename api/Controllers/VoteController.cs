using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace BeneditaApi.Controllers;

[ApiController]
[Route("vote")]
public class VoteController : ControllerBase
{
    private readonly VoteService         _svc;
    private readonly SerialHostedService _serial;

    public VoteController(VoteService svc, SerialHostedService serial)
    {
        _svc    = svc;
        _serial = serial;
    }

    /// <summary>Registra o voto de um eleitor numa entidade (chamado pelo ESP32 autónomo).</summary>
    [HttpPost]
    public async Task<IActionResult> CastVote([FromBody] VoteRequest req)
    {
        var (success, message) = await _svc.CastVoteAsync(req.FingerId, req.EntityId);
        if (!success)
            return BadRequest(new { sucesso = false, mensagem = message });

        return Ok(new { sucesso = true, mensagem = message });
    }

    /// <summary>
    /// Inicia uma sessão de votação a partir do painel MAUI.
    /// O eleitor escolhe a entidade no ecrã e coloca o dedo no sensor.
    /// Bloqueia até o ESP32 confirmar o voto (max 35 s).
    /// </summary>
    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiateVoteRequest req, CancellationToken ct)
    {
        // Verifica se a entidade existe
        var entities = await _svc.GetAllEntitiesAsync();
        if (!entities.Any(e => e.Id == req.EntityId))
            return BadRequest(new { sucesso = false, mensagem = "Entidade não encontrada." });

        string result = await _serial.SendVoteScanAsync(req.EntityId, ct);

        if (result.StartsWith("RES:VOTE_SCAN:OK:"))
        {
            var voterName = result["RES:VOTE_SCAN:OK:".Length..];
            return Ok(new { sucesso = true, nomeEleitor = voterName });
        }

        var error = result.StartsWith("RES:VOTE_SCAN:ERROR:")
            ? result["RES:VOTE_SCAN:ERROR:".Length..]
            : result;

        return BadRequest(new { sucesso = false, mensagem = error });
    }

    /// <summary>
    /// Identifica o eleitor apenas pela impressão digital.
    /// Não escolhe entidade nem regista voto nesta etapa.
    /// </summary>
    [HttpPost("identify")]
    public async Task<IActionResult> Identify(CancellationToken ct)
    {
        var result = await _serial.SendIdentifyScanAsync(ct);

        if (!result.StartsWith("RES:IDENTIFY_SCAN:OK:"))
        {
            var error = result.StartsWith("RES:IDENTIFY_SCAN:ERROR:")
                ? result["RES:IDENTIFY_SCAN:ERROR:".Length..]
                : result;
            return BadRequest(new { sucesso = false, mensagem = error });
        }

        var payload = result["RES:IDENTIFY_SCAN:OK:".Length..];
        var split = payload.Split(':', 2);
        if (split.Length < 1 || !int.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fingerId))
            return BadRequest(new { sucesso = false, mensagem = "Resposta biométrica inválida." });

        var voterByFinger = await _svc.GetVoterByFingerAsync(fingerId);
        if (voterByFinger is null)
            return BadRequest(new { sucesso = false, mensagem = "Impressão digital não cadastrada." });

        if (!voterByFinger.CanVote || voterByFinger.Vote is not null)
            return BadRequest(new { sucesso = false, mensagem = "Este eleitor já votou." });

        return Ok(new
        {
            sucesso = true,
            fingerId,
            nomeEleitor = voterByFinger.Name,
            mensagem = "Eleitor identificado. Escolha a entidade e confirme o voto."
        });
    }

    /// <summary>
    /// Faz a validação inicial por BI + cartão e dispara apenas a leitura biométrica.
    /// Não regista voto nesta etapa.
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanVoteRequest req, CancellationToken ct)
    {
        var bi = (req.BI ?? string.Empty).Trim();
        var voterCard = (req.VoterCard ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(bi))
            return BadRequest(new { sucesso = false, mensagem = "Informe o BI do eleitor." });

        if (string.IsNullOrWhiteSpace(voterCard))
            return BadRequest(new { sucesso = false, mensagem = "Informe o cartão do eleitor (processo/QR)." });

        var voterByBi = await _svc.GetVoterByBiAsync(bi);
        if (voterByBi is null)
            return BadRequest(new { sucesso = false, mensagem = "BI não encontrado no cadastro." });

        if (!voterByBi.CanVote || voterByBi.Vote is not null)
            return BadRequest(new { sucesso = false, mensagem = "Este eleitor já votou." });

        var result = await _serial.SendIdentifyScanAsync(ct);

        if (!result.StartsWith("RES:IDENTIFY_SCAN:OK:"))
        {
            var error = result.StartsWith("RES:IDENTIFY_SCAN:ERROR:")
                ? result["RES:IDENTIFY_SCAN:ERROR:".Length..]
                : result;
            return BadRequest(new { sucesso = false, mensagem = error });
        }

        var payload = result["RES:IDENTIFY_SCAN:OK:".Length..];
        var split = payload.Split(':', 2);
        if (split.Length < 2 || !int.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fingerId))
            return BadRequest(new { sucesso = false, mensagem = "Resposta biométrica inválida." });

        var voterByFinger = await _svc.GetVoterByFingerAsync(fingerId);
        if (voterByFinger is null)
            return BadRequest(new { sucesso = false, mensagem = "Impressão digital não cadastrada." });

        if (!string.Equals(voterByFinger.BI, voterByBi.BI, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { sucesso = false, mensagem = "BI informado não corresponde à biometria lida." });

        if (!voterByFinger.CanVote || voterByFinger.Vote is not null)
            return BadRequest(new { sucesso = false, mensagem = "Este eleitor já votou." });

        return Ok(new
        {
            sucesso = true,
            fingerId,
            nomeEleitor = voterByFinger.Name,
            mensagem = "Biometria validada. Confirme ou cancele o voto."
        });
    }

    /// <summary>
    /// Confirma o voto após a biometria.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmVoteRequest req)
    {
        var entities = await _svc.GetAllEntitiesAsync();
        if (!entities.Any(e => e.Id == req.EntityId))
            return BadRequest(new { sucesso = false, mensagem = "Entidade não encontrada." });

        var (success, message) = await _svc.CastVoteAsync(req.FingerId, req.EntityId);
        if (!success)
            return BadRequest(new { sucesso = false, mensagem = message });

        return Ok(new { sucesso = true, mensagem = "Voto confirmado com sucesso." });
    }

    /// <summary>
    /// Cancela a operação atual sem registar voto.
    /// </summary>
    [HttpPost("cancel")]
    public IActionResult Cancel() =>
        Ok(new { sucesso = true, mensagem = "Voto cancelado." });

    /// <summary>Retorna a contagem de votos por entidade.</summary>
    [HttpGet("results")]
    public async Task<IActionResult> Results()
    {
        var results = await _svc.GetResultsAsync();
        return Ok(results);
    }
}

public record VoteRequest(int FingerId, int EntityId);
public record InitiateVoteRequest(int EntityId);
public record ScanVoteRequest(string BI, string VoterCard);
public record ConfirmVoteRequest(int FingerId, int EntityId);
