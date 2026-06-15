using Microsoft.AspNetCore.Mvc;
using MSContas.Models;
using MSContas.Services;

namespace MSContas.Controllers;

[ApiController]
[Route("contas")]
public class ContasController(ContaService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CriarConta([FromBody] CriarContaRequest req)
    {
        var (conta, erro, status) = await service.CriarContaAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, new
        {
            mensagem = "Conta criada com sucesso",
            conta = new { id = conta!.Id, titular = conta.Titular, email = conta.Email, saldo = conta.Saldo }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> BuscarConta(Guid id)
    {
        var conta = await service.BuscarContaAsync(id);
        if (conta is null) return NotFound(new { erro = "Conta não encontrada" });
        return Ok(new
        {
            id = conta.Id,
            titular = conta.Titular,
            email = conta.Email,
            saldo = conta.Saldo,
            ativa = conta.Ativa,
            criada_em = conta.CriadaEm
        });
    }

    [HttpGet("{id:guid}/saldo")]
    public async Task<IActionResult> ConsultarSaldo(Guid id)
    {
        var (resultado, erro, status) = await service.ConsultarSaldoAsync(id);
        if (erro is not null) return StatusCode(status, new { erro });
        return Ok(resultado);
    }

    [HttpPatch("{id:guid}/saldo")]
    public async Task<IActionResult> AtualizarSaldo(Guid id, [FromBody] AtualizarSaldoRequest req)
    {
        var (resultado, erro, status) = await service.AtualizarSaldoAsync(id, req);
        if (erro is not null) return StatusCode(status, new { erro });
        return Ok(resultado);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> EncerrarConta(Guid id)
    {
        var (erro, status) = await service.EncerrarContaAsync(id);
        if (erro is not null) return StatusCode(status, new { erro });
        return Ok(new { mensagem = "Conta encerrada com sucesso" });
    }

    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { servico = "ms-contas", status = "online", porta = 5001 });
}
