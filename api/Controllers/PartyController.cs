using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeneditaApi.Controllers;

/// <summary>Gestão de entidades (partidos, coligações, etc.).</summary>
[ApiController]
[Route("entities")]
public class EntityController : ControllerBase
{
    private readonly VoteService _svc;

    public EntityController(VoteService svc) => _svc = svc;

    /// <summary>Lista todas as entidades.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _svc.GetAllEntitiesAsync());

    /// <summary>Cadastra uma nova entidade.</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddEntityRequest req)
    {
        try
        {
            var entity = await _svc.AddEntityAsync(req.Name, req.Acronym, req.Description);
            return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, entity);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensagem = ex.Message });
        }
    }

    /// <summary>Remove uma entidade.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var removed = await _svc.DeleteEntityAsync(id);
        return removed ? NoContent() : NotFound();
    }
}

public record AddEntityRequest(string Name, string Acronym, string? Description);
