using Microsoft.EntityFrameworkCore;
using MSNotificacoes.Data;
using MSNotificacoes.Models;

namespace MSNotificacoes.Services;

public class NotificacaoService(NotificacoesDbContext db)
{
    public async Task<(Notificacao notificacao, string? erro, int status)> EnviarAsync(EnviarNotificacaoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ContaId) || string.IsNullOrWhiteSpace(req.Email)
            || string.IsNullOrWhiteSpace(req.Titulo) || string.IsNullOrWhiteSpace(req.Mensagem))
            return (null!, "Campos obrigatórios: contaId, email, titulo, mensagem", 400);

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

    public async Task<List<object>> HistoricoPorContaAsync(string contaId, int limite = 10)
    {
        var lista = await db.Notificacoes
            .Where(n => n.ContaId == contaId)
            .OrderByDescending(n => n.EnviadaEm)
            .Take(limite)
            .Select(n => (object)new { id = n.Id, titulo = n.Titulo, mensagem = n.Mensagem, enviada = n.Enviada, enviada_em = n.EnviadaEm })
            .ToListAsync();
        return lista;
    }

    public async Task<List<Notificacao>> ListarTodasAsync(int limite = 20) =>
        await db.Notificacoes.OrderByDescending(n => n.EnviadaEm).Take(limite).ToListAsync();
}
