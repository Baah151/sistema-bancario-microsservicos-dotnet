using System.Net.Http.Json;
using MSTransacoes.Data;
using MSTransacoes.Models;

namespace MSTransacoes.Services;

public class TransacaoService(TransacoesDbContext db, IHttpClientFactory httpFactory, IConfiguration config)
{
    private HttpClient ContasClient => httpFactory.CreateClient("MSContas");
    private HttpClient NotificacoesClient => httpFactory.CreateClient("MSNotificacoes");

    // ── Helpers de comunicação ──────────────────────────────

    private async Task<ContaResponse?> ConsultarContaAsync(Guid contaId)
    {
        var resp = await ContasClient.GetAsync($"/contas/{contaId}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ContaResponse>();
    }

    private async Task<(decimal novoSaldo, string? erro, int status)> AtualizarSaldoAsync(Guid contaId, string operacao, decimal valor)
    {
        var resp = await ContasClient.PatchAsJsonAsync($"/contas/{contaId}/saldo", new { operacao, valor });
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (0, err?.GetValueOrDefault("erro") ?? resp.ReasonPhrase, (int)resp.StatusCode);
        }
        var body = await resp.Content.ReadFromJsonAsync<SaldoResponse>();
        return (body!.NovoSaldo, null, 200);
    }

    private async Task NotificarAsync(Guid contaId, string email, string titulo, string mensagem)
    {
        try
        {
            await NotificacoesClient.PostAsJsonAsync("/notificacoes/enviar", new { contaId, email, titulo, mensagem });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MS-TRANSACOES] Aviso: notificação não enviada — {ex.Message}");
        }
    }

    private async Task<Transacao> RegistrarAsync(string tipo, Guid? origem, Guid? destino, decimal valor, string descricao)
    {
        var t = new Transacao
        {
            Tipo = tipo,
            ContaOrigem = origem,
            ContaDestino = destino,
            Valor = valor,
            Descricao = descricao,
        };
        db.Transacoes.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    // ── Operações ───────────────────────────────────────────

    public async Task<(object? resultado, string? erro, int status)> DepositoAsync(DepositoRequest req)
    {
        if (req.ContaId == Guid.Empty || req.Valor <= 0)
            return (null, "Informe contaId e valor positivo", 400);

        var conta = await ConsultarContaAsync(req.ContaId);
        if (conta is null) return (null, "Conta não encontrada", 404);

        var (novoSaldo, erro, status) = await AtualizarSaldoAsync(req.ContaId, "creditar", req.Valor);
        if (erro is not null) return (null, erro, status);

        var t = await RegistrarAsync("deposito", null, req.ContaId, req.Valor, req.Descricao ?? "Depósito");

        await NotificarAsync(req.ContaId, conta.Email, "Depósito realizado",
            $"Depósito de R${req.Valor:F2} realizado com sucesso. Saldo atual: R${novoSaldo:F2}");

        Console.WriteLine($"[MS-TRANSACOES] Depósito: conta={req.ContaId} | R${req.Valor} | Saldo: R${novoSaldo}");

        return (new { mensagem = "Depósito realizado com sucesso", transacaoId = t.Id, valor = req.Valor, novoSaldo }, null, 201);
    }

    public async Task<(object? resultado, string? erro, int status)> SaqueAsync(SaqueRequest req)
    {
        if (req.ContaId == Guid.Empty || req.Valor <= 0)
            return (null, "Informe contaId e valor positivo", 400);

        var conta = await ConsultarContaAsync(req.ContaId);
        if (conta is null) return (null, "Conta não encontrada", 404);

        var (novoSaldo, erro, status) = await AtualizarSaldoAsync(req.ContaId, "debitar", req.Valor);
        if (erro is not null) return (null, erro, status);

        var t = await RegistrarAsync("saque", req.ContaId, null, req.Valor, req.Descricao ?? "Saque");

        await NotificarAsync(req.ContaId, conta.Email, "Saque realizado",
            $"Saque de R${req.Valor:F2} realizado. Saldo atual: R${novoSaldo:F2}");

        Console.WriteLine($"[MS-TRANSACOES] Saque: conta={req.ContaId} | R${req.Valor} | Saldo: R${novoSaldo}");

        return (new { mensagem = "Saque realizado com sucesso", transacaoId = t.Id, valor = req.Valor, novoSaldo }, null, 201);
    }

    public async Task<(object? resultado, string? erro, int status)> TransferenciaAsync(TransferenciaRequest req)
    {
        if (req.ContaOrigemId == Guid.Empty || req.ContaDestinoId == Guid.Empty || req.Valor <= 0)
            return (null, "Informe contaOrigemId, contaDestinoId e valor positivo", 400);

        if (req.ContaOrigemId == req.ContaDestinoId)
            return (null, "Conta de origem e destino não podem ser iguais", 400);

        var contaOrigemTask = ConsultarContaAsync(req.ContaOrigemId);
        var contaDestinoTask = ConsultarContaAsync(req.ContaDestinoId);
        await Task.WhenAll(contaOrigemTask, contaDestinoTask);

        var contaOrigem = await contaOrigemTask;
        var contaDestino = await contaDestinoTask;

        if (contaOrigem is null) return (null, "Conta de origem não encontrada", 404);
        if (contaDestino is null) return (null, "Conta de destino não encontrada", 404);

        var (saldoOrigem, erroOrigem, statusOrigem) = await AtualizarSaldoAsync(req.ContaOrigemId, "debitar", req.Valor);
        if (erroOrigem is not null) return (null, erroOrigem, statusOrigem);

        var (saldoDestino, erroDestino, statusDestino) = await AtualizarSaldoAsync(req.ContaDestinoId, "creditar", req.Valor);
        if (erroDestino is not null) return (null, erroDestino, statusDestino);

        var t = await RegistrarAsync("transferencia", req.ContaOrigemId, req.ContaDestinoId, req.Valor,
            req.Descricao ?? $"Transferência para {contaDestino.Titular}");

        await Task.WhenAll(
            NotificarAsync(req.ContaOrigemId, contaOrigem.Email, "Transferência enviada",
                $"Transferência de R${req.Valor:F2} para {contaDestino.Titular}. Seu saldo: R${saldoOrigem:F2}"),
            NotificarAsync(req.ContaDestinoId, contaDestino.Email, "Transferência recebida",
                $"Você recebeu R${req.Valor:F2} de {contaOrigem.Titular}. Seu saldo: R${saldoDestino:F2}")
        );

        Console.WriteLine($"[MS-TRANSACOES] Transferência: {req.ContaOrigemId} → {req.ContaDestinoId} | R${req.Valor}");

        return (new { mensagem = "Transferência realizada com sucesso", transacaoId = t.Id, valor = req.Valor, saldoOrigem, saldoDestino }, null, 201);
    }

    public async Task<object> ExtratoAsync(Guid contaId, int limite = 20)
    {
        var lista = db.Transacoes
            .Where(t => t.ContaOrigem == contaId || t.ContaDestino == contaId)
            .OrderByDescending(t => t.RealizadaEm)
            .Take(limite)
            .ToList();

        return new { contaId, total = lista.Count, transacoes = lista };
    }
}
