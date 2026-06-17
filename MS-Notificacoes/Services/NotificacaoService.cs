using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MSNotificacoes.Data;
using MSNotificacoes.Models;

namespace MSNotificacoes.Services;

// Responsável por registrar e "enviar" notificações aos titulares de contas.
// O envio é simulado no console (não há SMTP real), mas todos os registros
// são salvos no banco de dados para consulta posterior.
// Também se comunica com o MS-Contas para identificar o titular nas consultas.
public class NotificacaoService(NotificacoesDbContext db, IHttpClientFactory httpFactory)
{
    private HttpClient ContasClient => httpFactory.CreateClient("MSContas");

    // Registra uma notificação no banco e simula o envio do e-mail no console.
    // Chamado pelo MS-Contas (eventos de conta) e pelo MS-Transacoes (eventos financeiros).
    public async Task<(Notificacao notificacao, string? erro, int status)> EnviarAsync(EnviarNotificacaoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ContaId) || string.IsNullOrWhiteSpace(req.Email)
            || string.IsNullOrWhiteSpace(req.Titulo) || string.IsNullOrWhiteSpace(req.Mensagem))
            return (null!, "Campos obrigatórios: contaId, email, titulo, mensagem", 400);

        Console.WriteLine($"[MS-NOTIFICACOES] ← Notificação recebida para: {req.Email} | Assunto: {req.Titulo}");

        var notificacao = new Notificacao
        {
            ContaId = req.ContaId,
            Email = req.Email,
            Titulo = req.Titulo,
            Mensagem = req.Mensagem,
            Enviada = true,
        };

        db.Notificacoes.Add(notificacao);
        await db.SaveChangesAsync();

        Console.WriteLine($"[MS-NOTIFICACOES] 📧 E-mail enviado");
        Console.WriteLine($"   Para: {req.Email}");
        Console.WriteLine($"   Assunto: {req.Titulo}");
        Console.WriteLine($"   Mensagem: {req.Mensagem}");

        return (notificacao, null, 201);
    }

    // Lista as notificações de uma conta em ordem cronológica decrescente.
    // Antes de retornar, consulta o MS-Contas para identificar o nome do titular
    // e incluí-lo na resposta — tornando o resultado mais informativo.
    public async Task<(string? titular, List<object> notificacoes)> HistoricoPorContaAsync(string contaId, int limite = 10)
    {
        // Conexão 3: busca o nome do titular no MS-Contas em tempo real
        string? titular = null;
        try
        {
            var resp = await ContasClient.GetAsync($"/contas/{contaId}");
            if (resp.IsSuccessStatusCode)
            {
                var conta = await resp.Content.ReadFromJsonAsync<ContaInfo>();
                titular = conta?.Titular;
                Console.WriteLine($"[MS-NOTIFICACOES → MS-CONTAS] Titular obtido: {titular}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MS-NOTIFICACOES] Aviso: não foi possível consultar conta — {ex.Message}");
        }

        var lista = await db.Notificacoes
            .Where(n => n.ContaId == contaId)
            .OrderByDescending(n => n.EnviadaEm)
            .Take(limite)
            .Select(n => (object)new { id = n.Id, titulo = n.Titulo, mensagem = n.Mensagem, enviada = n.Enviada, enviada_em = n.EnviadaEm })
            .ToListAsync();

        return (titular, lista);
    }

    // Lista todas as notificações do sistema, independente da conta.
    // Útil para depuração e visualização geral das comunicações enviadas.
    public async Task<List<Notificacao>> ListarTodasAsync(int limite = 20) =>
        await db.Notificacoes.OrderByDescending(n => n.EnviadaEm).Take(limite).ToListAsync();
}
