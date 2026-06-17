using Microsoft.AspNetCore.Mvc;
using MSContas.Models;
using MSContas.Services;

namespace MSContas.Controllers;

// Ponto de entrada HTTP para todas as operações de conta.
// Recebe as requisições, chama o serviço e devolve a resposta formatada.
[ApiController]
[Route("contas")]
public class ContasController(ContaService service) : ControllerBase
{
    // Cria uma nova conta com os dados enviados no corpo da requisição.
    // Retorna os dados da conta criada ou uma mensagem de erro.
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

    // Retorna os dados completos de uma conta pelo ID.
    // A resposta inclui um resumo de transações obtido em tempo real do MS-Transacoes.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> BuscarConta(Guid id)
    {
        var (conta, resumoTransacoes) = await service.BuscarContaAsync(id);
        if (conta is null) return NotFound(new { erro = "Conta não encontrada" });
        return Ok(new
        {
            id = conta.Id,
            titular = conta.Titular,
            email = conta.Email,
            saldo = conta.Saldo,
            ativa = conta.Ativa,
            criada_em = conta.CriadaEm,
            resumo_transacoes = resumoTransacoes
        });
    }

    // Retorna apenas o saldo atual da conta, sem os demais dados cadastrais.
    [HttpGet("{id:guid}/saldo")]
    public async Task<IActionResult> ConsultarSaldo(Guid id)
    {
        var (resultado, erro, status) = await service.ConsultarSaldoAsync(id);
        if (erro is not null) return StatusCode(status, new { erro });
        return Ok(resultado);
    }

    // Ajusta o saldo da conta (creditar ou debitar).
    // Chamado internamente pelo MS-Transacoes após processar uma operação financeira.
    [HttpPatch("{id:guid}/saldo")]
    public async Task<IActionResult> AtualizarSaldo(Guid id, [FromBody] AtualizarSaldoRequest req)
    {
        var (resultado, erro, status) = await service.AtualizarSaldoAsync(id, req);
        if (erro is not null) return StatusCode(status, new { erro });
        return Ok(resultado);
    }

    // Encerra a conta bancária. Só funciona se o saldo estiver zerado.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> EncerrarConta(Guid id)
    {
        var (erro, status) = await service.EncerrarContaAsync(id);
        if (erro is not null) return StatusCode(status, new { erro });
        return Ok(new { mensagem = "Conta encerrada com sucesso" });
    }

    // Verifica se o serviço está online. Usado para monitoramento e testes de conectividade.
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { servico = "ms-contas", status = "online", porta = 5001 });
}
