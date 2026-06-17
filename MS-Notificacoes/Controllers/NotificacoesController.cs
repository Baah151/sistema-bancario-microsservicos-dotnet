using Microsoft.AspNetCore.Mvc;
using MSNotificacoes.Models;
using MSNotificacoes.Services;

namespace MSNotificacoes.Controllers;

// Ponto de entrada HTTP para operações de notificação.
// Recebe os requests e os encaminha ao serviço correspondente.
[ApiController]
[Route("notificacoes")]
public class NotificacoesController(NotificacaoService service) : ControllerBase
{
    // Recebe e registra uma notificação. Chamado pelos outros microsserviços
    // (MS-Contas e MS-Transacoes) após eventos relevantes como criação de conta ou transações.
    [HttpPost("enviar")]
    public async Task<IActionResult> Enviar([FromBody] EnviarNotificacaoRequest req)
    {
        var (notificacao, erro, status) = await service.EnviarAsync(req);
        if (erro is not null) return StatusCode(status, new { erro });
        return StatusCode(201, new
        {
            mensagem = "Notificação enviada com sucesso",
            notificacaoId = notificacao.Id,
            para = notificacao.Email
        });
    }

    // Retorna o histórico de notificações de uma conta específica.
    // A resposta inclui o nome do titular, obtido em tempo real do MS-Contas.
    [HttpGet("{contaId}")]
    public async Task<IActionResult> HistoricoPorConta(string contaId, [FromQuery] int limite = 10)
    {
        var (titular, lista) = await service.HistoricoPorContaAsync(contaId, limite);
        return Ok(new { contaId, titular, total = lista.Count, notificacoes = lista });
    }

    // Lista todas as notificações do sistema. Usado principalmente para depuração.
    [HttpGet]
    public async Task<IActionResult> ListarTodas([FromQuery] int limite = 20)
    {
        var lista = await service.ListarTodasAsync(limite);
        return Ok(new { total = lista.Count, notificacoes = lista });
    }

    // Verifica se o serviço está online. Usado para monitoramento e testes de conectividade.
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { servico = "ms-notificacoes", status = "online", porta = 5003 });
}
