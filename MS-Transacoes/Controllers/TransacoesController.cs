using Microsoft.AspNetCore.Mvc;
using MSTransacoes.Models;
using MSTransacoes.Services;

namespace MSTransacoes.Controllers;

[ApiController]
[Route("transacoes")]
public class TransacoesController(TransacaoService service) : ControllerBase
{
    [HttpPost("deposito")]
    public async Task<IActionResult> Deposito([FromBody] DepositoRequest req)
    {
        var (resultado, erro, status) = await service.DepositoAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, resultado);
    }

    [HttpPost("saque")]
    public async Task<IActionResult> Saque([FromBody] SaqueRequest req)
    {
        var (resultado, erro, status) = await service.SaqueAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, resultado);
    }

    [HttpPost("transferencia")]
    public async Task<IActionResult> Transferencia([FromBody] TransferenciaRequest req)
    {
        var (resultado, erro, status) = await service.TransferenciaAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, resultado);
    }

    [HttpGet("{contaId:guid}")]
    public async Task<IActionResult> Extrato(Guid contaId, [FromQuery] int limite = 20)
    {
        var resultado = await service.ExtratoAsync(contaId, limite);
        return Ok(resultado);
    }

    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { servico = "ms-transacoes", status = "online", porta = 5002 });
}
