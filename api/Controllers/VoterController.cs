using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeneditaApi.Controllers;

/// <summary>
/// Gestão de eleitores (cadastro, listagem, remoção).
/// Use este endpoint para registrar os IDs do sensor biométrico.
/// </summary>
[ApiController]
[Route("voters")]
public class VoterController : ControllerBase
{
    private readonly VoteService          _svc;
    private readonly SerialHostedService  _serial;

    public VoterController(VoteService svc, SerialHostedService serial)
    {
        _svc    = svc;
        _serial = serial;
    }

    /// <summary>Lista todos os eleitores cadastrados.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var voters = await _svc.GetAllVotersAsync();
        return Ok(voters);
    }

    /// <summary>Retorna um eleitor pelo ID biométrico.</summary>
    [HttpGet("{fingerId:int}")]
    public async Task<IActionResult> Get(int fingerId)
    {
        var voter = await _svc.GetVoterByFingerAsync(fingerId);
        return voter is null ? NotFound() : Ok(voter);
    }

    /// <summary>Cadastra um novo eleitor.</summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterVoterRequest req)
    {
        try
        {
            var voter = await _svc.RegisterVoterAsync(req.FingerId, req.Name);

            // Notifica o ESP32 via serial
            _serial.Send($"INFO:VOTER_REGISTERED:{req.FingerId}");

            return CreatedAtAction(nameof(Get), new { fingerId = voter.FingerId }, voter);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensagem = ex.Message });
        }
    }

    /// <summary>Remove um eleitor (anula cadastro).</summary>
    [HttpDelete("{fingerId:int}")]
    public async Task<IActionResult> Delete(int fingerId)
    {
        var removed = await _svc.DeleteVoterAsync(fingerId);
        return removed ? NoContent() : NotFound();
    }
}

public record RegisterVoterRequest(int FingerId, string Name);
