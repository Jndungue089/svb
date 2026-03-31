using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeneditaApi.Controllers;

/// <summary>
/// Gestão de eleitores.
/// Fluxo de registo em dois passos:
///   1. POST /voters          → cria eleitor com Nome + BI (sem impressão digital)
///   2. POST /voters/{id}/enroll → inicia enrolamento biométrico via ESP32
/// </summary>
[ApiController]
[Route("voters")]
public class VoterController : ControllerBase
{
    private readonly VoteService         _svc;
    private readonly SerialHostedService _serial;

    public VoterController(VoteService svc, SerialHostedService serial)
    {
        _svc    = svc;
        _serial = serial;
    }

    /// <summary>Lista todos os eleitores.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _svc.GetAllVotersAsync());

    /// <summary>Retorna um eleitor pelo ID interno.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var voter = await _svc.GetVoterByIdAsync(id);
        return voter is null ? NotFound() : Ok(voter);
    }

    /// <summary>Passo 1: Cadastra eleitor com Nome e BI (sem impressão digital).</summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterVoterRequest req)
    {
        try
        {
            var voter = await _svc.RegisterVoterAsync(req.Name, req.BI);
            return CreatedAtAction(nameof(Get), new { id = voter.Id }, voter);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Passo 2: Inicia enrolamento biométrico.
    /// Bloqueia até o ESP32 confirmar (max 30 s).
    /// </summary>
    [HttpPost("{id:int}/enroll")]
    public async Task<IActionResult> Enroll(int id, CancellationToken ct)
    {
        try
        {
            // Determina próximo slot livre
            int slot = await _svc.NextFreeSlotAsync();

            // Envia comando ao ESP32 e aguarda resultado
            string result = await _serial.SendEnrollAsync(slot, ct);

            if (!result.StartsWith("RES:ENROLL:OK:"))
            {
                var errorPart = result.StartsWith("RES:ENROLL:ERROR:")
                    ? result["RES:ENROLL:ERROR:".Length..]
                    : result;
                return BadRequest(new { mensagem = $"Enrolamento falhou: {errorPart}" });
            }

            // Extrai o slot confirmado pelo sensor
            if (!int.TryParse(result["RES:ENROLL:OK:".Length..], out int confirmedSlot))
                confirmedSlot = slot;

            var voter = await _svc.AssignFingerAsync(id, confirmedSlot);
            return Ok(voter);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>Remove um eleitor pelo ID interno.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var removed = await _svc.DeleteVoterAsync(id);
        return removed ? NoContent() : NotFound();
    }
}

public record RegisterVoterRequest(string Name, string BI);
