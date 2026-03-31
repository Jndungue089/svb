using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeneditaApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly VoteService _svc;

    public AuthController(VoteService svc) => _svc = svc;

    /// <summary>
    /// Verifica se o eleitor está autorizado a votar.
    /// Chamado pelo ESP32 (WiFi) ou testado manualmente.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Auth([FromBody] AuthRequest req)
    {
        var (authorized, reason, _) = await _svc.AuthorizeAsync(req.FingerId);
        return Ok(new { autorizado = authorized, motivo = reason });
    }
}

public record AuthRequest(int FingerId);
