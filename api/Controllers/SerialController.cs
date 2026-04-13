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

    [HttpGet("log")]
    public IActionResult Log([FromQuery] long? since = null)
        => Ok(_serial.GetLog(since));

    [HttpPost("send")]
    public IActionResult SendCommand([FromBody] SerialSendRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Command))
            return BadRequest(new { sucesso = false, mensagem = "Comando vazio." });

        _serial.Send(req.Command.Trim());
        return Ok(new { sucesso = true, mensagem = "Comando enviado." });
    }
}

public record SerialConnectRequest(string PortName, int BaudRate = 115200);
public record SerialSendRequest(string Command);
