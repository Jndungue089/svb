using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;

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
