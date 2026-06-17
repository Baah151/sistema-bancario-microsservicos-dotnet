using Microsoft.AspNetCore.Mvc;
using MSTransacoes.Models;
using MSTransacoes.Services;

namespace MSTransacoes.Controllers;

// Ponto de entrada HTTP para todas as operações financeiras.
// Recebe os requests, repassa ao serviço e devolve a resposta ao cliente.
[ApiController]
[Route("transacoes")]
public class TransacoesController(TransacaoService service) : ControllerBase
{
    // Realiza um depósito em uma conta. Recebe o ID da conta e o valor a depositar.
    [HttpPost("deposito")]
    public async Task<IActionResult> Deposito([FromBody] DepositoRequest req)
    {
        var (resultado, erro, status) = await service.DepositoAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, resultado);
    }

    // Realiza um saque de uma conta. Recusa se o saldo for insuficiente.
    [HttpPost("saque")]
    public async Task<IActionResult> Saque([FromBody] SaqueRequest req)
    {
        var (resultado, erro, status) = await service.SaqueAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, resultado);
    }

    // Realiza uma transferência entre duas contas distintas.
    [HttpPost("transferencia")]
    public async Task<IActionResult> Transferencia([FromBody] TransferenciaRequest req)
    {
        var (resultado, erro, status) = await service.TransferenciaAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, resultado);
    }

    // Transferência consolidada: executa a movimentação e retorna, em uma única resposta,
    // o saldo atualizado de ambas as contas e a confirmação de notificação para os dois titulares.
    [HttpPost("transferencia-consolidada")]
    public async Task<IActionResult> TransferenciaConsolidada([FromBody] TransferenciaRequest req)
    {
        var (resultado, erro, status) = await service.TransferenciaConsolidadaAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, resultado);
    }

    // Retorna o extrato de uma conta: lista das últimas transações em ordem cronológica.
    [HttpGet("{contaId:guid}")]
    public async Task<IActionResult> Extrato(Guid contaId, [FromQuery] int limite = 20)
    {
        var resultado = await service.ExtratoAsync(contaId, limite);
        return Ok(resultado);
    }

    // Retorna um resumo rápido: total de transações e data da última movimentação.
    // Consumido pelo MS-Contas ao buscar os dados de uma conta.
    [HttpGet("{contaId:guid}/resumo")]
    public async Task<IActionResult> Resumo(Guid contaId)
    {
        var resultado = await service.ResumoAsync(contaId);
        return Ok(resultado);
    }

    // Verifica se o serviço está online. Usado para monitoramento e testes de conectividade.
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { servico = "ms-transacoes", status = "online", porta = 5002 });
}
