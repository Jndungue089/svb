using BeneditaApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeneditaApi.Controllers;

[ApiController]
[Route("serial")]
public class SerialController : ControllerBase
{
    private readonly SerialHostedService _serial;

    public SerialController(SerialHostedService serial) => _serial = serial;

    [HttpGet("ports")]
    public IActionResult Ports() => Ok(_serial.GetAvailablePorts());

    [HttpGet("status")]
    public IActionResult Status() => Ok(_serial.GetStatus());

    [HttpPost("connect")]
    public IActionResult Connect([FromBody] SerialConnectRequest req)
    {
        var (ok, message) = _serial.Connect(req.PortName, req.BaudRate <= 0 ? 115200 : req.BaudRate);
        return ok
            ? Ok(new { sucesso = true, mensagem = message })
            : BadRequest(new { sucesso = false, mensagem = message });
    }

    [HttpPost("disconnect")]
    public IActionResult Disconnect()
    {
        _serial.Disconnect();
        return Ok(new { sucesso = true, mensagem = "Porta serial desconectada." });
    }
}

public record SerialConnectRequest(string PortName, int BaudRate = 115200);
