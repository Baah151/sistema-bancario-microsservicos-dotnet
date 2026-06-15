using Microsoft.AspNetCore.Mvc;
using MSNotificacoes.Models;
using MSNotificacoes.Services;

namespace MSNotificacoes.Controllers;

[ApiController]
[Route("notificacoes")]
public class NotificacoesController(NotificacaoService service) : ControllerBase
{
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

    [HttpGet("{contaId}")]
    public async Task<IActionResult> HistoricoPorConta(string contaId, [FromQuery] int limite = 10)
    {
        var lista = await service.HistoricoPorContaAsync(contaId, limite);
        return Ok(new { contaId, total = lista.Count, notificacoes = lista });
    }

    [HttpGet]
    public async Task<IActionResult> ListarTodas([FromQuery] int limite = 20)
    {
        var lista = await service.ListarTodasAsync(limite);
        return Ok(new { total = lista.Count, notificacoes = lista });
    }

    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { servico = "ms-notificacoes", status = "online", porta = 5003 });
}
