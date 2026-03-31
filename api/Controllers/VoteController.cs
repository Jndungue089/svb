using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeneditaApi.Controllers;

[ApiController]
[Route("vote")]
public class VoteController : ControllerBase
{
    private readonly VoteService _svc;

    public VoteController(VoteService svc) => _svc = svc;

    /// <summary>Registra o voto de um eleitor numa entidade.</summary>
    [HttpPost]
    public async Task<IActionResult> CastVote([FromBody] VoteRequest req)
    {
        var (success, message) = await _svc.CastVoteAsync(req.FingerId, req.EntityId);
        if (!success)
            return BadRequest(new { sucesso = false, mensagem = message });

        return Ok(new { sucesso = true, mensagem = message });
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
