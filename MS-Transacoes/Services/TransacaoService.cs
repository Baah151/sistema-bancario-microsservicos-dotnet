using System.Net.Http.Json;
using MSTransacoes.Data;
using MSTransacoes.Models;

namespace MSTransacoes.Services;

public class TransacaoService(TransacoesDbContext db, IHttpClientFactory httpFactory)
{
    private HttpClient ContasClient => httpFactory.CreateClient("MSContas");
    private HttpClient NotificacoesClient => httpFactory.CreateClient("MSNotificacoes");

    // ── Helpers ─────────────────────────────────────────────

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
            Console.WriteLine($"[MS-TRANSACOES] 🔔 INTERAÇÃO 3 — Disparando notificação para {email}");
            await NotificacoesClient.PostAsJsonAsync("/notificacoes/enviar", new { contaId, email, titulo, mensagem });
            Console.WriteLine($"[MS-TRANSACOES] ✅ Notificação enviada para {email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MS-TRANSACOES] ⚠️ Notificação não enviada — {ex.Message}");
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

    // ── Transferência com 3 interações explícitas ────────────

    public async Task<(object? resultado, string? erro, int status)> TransferenciaAsync(TransferenciaRequest req)
    {
        if (req.ContaOrigemId == Guid.Empty || req.ContaDestinoId == Guid.Empty || req.Valor <= 0)
            return (null, "Informe contaOrigemId, contaDestinoId e valor positivo", 400);

        if (req.ContaOrigemId == req.ContaDestinoId)
            return (null, "Conta de origem e destino não podem ser iguais", 400);

        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"[MS-TRANSACOES] ⚡ TRANSFERÊNCIA INICIADA — R${req.Valor:F2}");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // ── INTERAÇÃO 1: Consulta ambas as contas no MS-Contas ──
        Console.WriteLine($"[MS-TRANSACOES] 🔍 INTERAÇÃO 1 — Consultando contas no MS-Contas...");
        var contaOrigemTask = ConsultarContaAsync(req.ContaOrigemId);
        var contaDestinoTask = ConsultarContaAsync(req.ContaDestinoId);
        await Task.WhenAll(contaOrigemTask, contaDestinoTask);

        var contaOrigem = await contaOrigemTask;
        var contaDestino = await contaDestinoTask;

        if (contaOrigem is null) return (null, "Conta de origem não encontrada", 404);
        if (contaDestino is null) return (null, "Conta de destino não encontrada", 404);

        Console.WriteLine($"[MS-TRANSACOES] ✅ Conta origem: {contaOrigem.Titular} | Saldo atual: R${contaOrigem.Saldo:F2}");
        Console.WriteLine($"[MS-TRANSACOES] ✅ Conta destino: {contaDestino.Titular} | Saldo atual: R${contaDestino.Saldo:F2}");

        if (contaOrigem.Saldo < req.Valor)
            return (null, $"Saldo insuficiente. Saldo atual: R${contaOrigem.Saldo:F2}", 422);

        // ── INTERAÇÃO 2: Efetua a transação no MS-Contas ────────
        Console.WriteLine($"[MS-TRANSACOES] 💸 INTERAÇÃO 2 — Efetuando transferência de R${req.Valor:F2}...");
        var (saldoOrigem, erroOrigem, statusOrigem) = await AtualizarSaldoAsync(req.ContaOrigemId, "debitar", req.Valor);
        if (erroOrigem is not null) return (null, erroOrigem, statusOrigem);

        var (saldoDestino, erroDestino, statusDestino) = await AtualizarSaldoAsync(req.ContaDestinoId, "creditar", req.Valor);
        if (erroDestino is not null) return (null, erroDestino, statusDestino);

        var t = await RegistrarAsync("transferencia", req.ContaOrigemId, req.ContaDestinoId, req.Valor,
            req.Descricao ?? $"Transferência para {contaDestino.Titular}");

        Console.WriteLine($"[MS-TRANSACOES] ✅ Transação registrada: ID={t.Id}");
        Console.WriteLine($"[MS-TRANSACOES] ✅ Novo saldo origem ({contaOrigem.Titular}): R${saldoOrigem:F2}");
        Console.WriteLine($"[MS-TRANSACOES] ✅ Novo saldo destino ({contaDestino.Titular}): R${saldoDestino:F2}");

        // ── INTERAÇÃO 3: Notifica ambas as partes ───────────────
        Console.WriteLine($"[MS-TRANSACOES] 🔔 INTERAÇÃO 3 — Notificando ambas as partes via MS-Notificacoes...");
        await Task.WhenAll(
            NotificarAsync(req.ContaOrigemId, contaOrigem.Email, "Transferência enviada",
                $"Transferência de R${req.Valor:F2} para {contaDestino.Titular}. Seu saldo: R${saldoOrigem:F2}"),
            NotificarAsync(req.ContaDestinoId, contaDestino.Email, "Transferência recebida",
                $"Você recebeu R${req.Valor:F2} de {contaOrigem.Titular}. Seu saldo: R${saldoDestino:F2}")
        );
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        return (new
        {
            mensagem = "Transferência realizada com sucesso",
            transacaoId = t.Id,
            valor = req.Valor,
            origem = new { conta = contaOrigem.Titular, saldoAnterior = contaOrigem.Saldo, saldoAtual = saldoOrigem },
            destino = new { conta = contaDestino.Titular, saldoAnterior = contaDestino.Saldo, saldoAtual = saldoDestino },
            notificacoes = "disparadas"
        }, null, 201);
    }

    // ── Depósito ─────────────────────────────────────────────

    public async Task<(object? resultado, string? erro, int status)> DepositoAsync(DepositoRequest req)
    {
        if (req.ContaId == Guid.Empty || req.Valor <= 0)
            return (null, "Informe contaId e valor positivo", 400);

        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"[MS-TRANSACOES] ⚡ DEPÓSITO INICIADO — R${req.Valor:F2}");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"[MS-TRANSACOES] 🔍 INTERAÇÃO 1 — Consultando conta no MS-Contas...");
        var conta = await ConsultarContaAsync(req.ContaId);
        if (conta is null) return (null, "Conta não encontrada", 404);
        Console.WriteLine($"[MS-TRANSACOES] ✅ Conta: {conta.Titular} | Saldo atual: R${conta.Saldo:F2}");

        Console.WriteLine($"[MS-TRANSACOES] 💸 INTERAÇÃO 2 — Efetuando depósito de R${req.Valor:F2}...");
        var (novoSaldo, erro, status) = await AtualizarSaldoAsync(req.ContaId, "creditar", req.Valor);
        if (erro is not null) return (null, erro, status);

        var t = await RegistrarAsync("deposito", null, req.ContaId, req.Valor, req.Descricao ?? "Depósito");
        Console.WriteLine($"[MS-TRANSACOES] ✅ Depósito registrado. Novo saldo: R${novoSaldo:F2}");

        Console.WriteLine($"[MS-TRANSACOES] 🔔 INTERAÇÃO 3 — Notificando titular via MS-Notificacoes...");
        await NotificarAsync(req.ContaId, conta.Email, "Depósito realizado",
            $"Depósito de R${req.Valor:F2} realizado. Saldo atual: R${novoSaldo:F2}");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        return (new { mensagem = "Depósito realizado com sucesso", transacaoId = t.Id, valor = req.Valor, novoSaldo }, null, 201);
    }

    // ── Saque ────────────────────────────────────────────────

    public async Task<(object? resultado, string? erro, int status)> SaqueAsync(SaqueRequest req)
    {
        if (req.ContaId == Guid.Empty || req.Valor <= 0)
            return (null, "Informe contaId e valor positivo", 400);

        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"[MS-TRANSACOES] ⚡ SAQUE INICIADO — R${req.Valor:F2}");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"[MS-TRANSACOES] 🔍 INTERAÇÃO 1 — Consultando conta no MS-Contas...");
        var conta = await ConsultarContaAsync(req.ContaId);
        if (conta is null) return (null, "Conta não encontrada", 404);
        Console.WriteLine($"[MS-TRANSACOES] ✅ Conta: {conta.Titular} | Saldo atual: R${conta.Saldo:F2}");

        Console.WriteLine($"[MS-TRANSACOES] 💸 INTERAÇÃO 2 — Efetuando saque de R${req.Valor:F2}...");
        var (novoSaldo, erro, status) = await AtualizarSaldoAsync(req.ContaId, "debitar", req.Valor);
        if (erro is not null) return (null, erro, status);

        var t = await RegistrarAsync("saque", req.ContaId, null, req.Valor, req.Descricao ?? "Saque");
        Console.WriteLine($"[MS-TRANSACOES] ✅ Saque registrado. Novo saldo: R${novoSaldo:F2}");

        Console.WriteLine($"[MS-TRANSACOES] 🔔 INTERAÇÃO 3 — Notificando titular via MS-Notificacoes...");
        await NotificarAsync(req.ContaId, conta.Email, "Saque realizado",
            $"Saque de R${req.Valor:F2} realizado. Saldo atual: R${novoSaldo:F2}");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        return (new { mensagem = "Saque realizado com sucesso", transacaoId = t.Id, valor = req.Valor, novoSaldo }, null, 201);
    }

    // ── Extrato ──────────────────────────────────────────────

    public async Task<object> ExtratoAsync(Guid contaId, int limite = 20)
    {
        var lista = db.Transacoes
            .Where(t => t.ContaOrigem == contaId || t.ContaDestino == contaId)
            .OrderByDescending(t => t.RealizadaEm)
            .Take(limite)
            .ToList();

        return new { contaId, total = lista.Count, transacoes = lista };
    }

    // ── Transferência Consolidada ─────────────────────────────
    // Igual à transferência comum, mas aguarda confirmação das notificações
    // e retorna saldos atualizados + status de notificação de forma explícita.

    public async Task<(object? resultado, string? erro, int status)> TransferenciaConsolidadaAsync(TransferenciaRequest req)
    {
        if (req.ContaOrigemId == Guid.Empty || req.ContaDestinoId == Guid.Empty || req.Valor <= 0)
            return (null, "Informe contaOrigemId, contaDestinoId e valor positivo", 400);

        if (req.ContaOrigemId == req.ContaDestinoId)
            return (null, "Conta de origem e destino não podem ser iguais", 400);

        Console.WriteLine($"[MS-TRANSACOES] 🔍 INTERAÇÃO 1 — Consultando contas no MS-Contas...");
        var contaOrigemTask = ConsultarContaAsync(req.ContaOrigemId);
        var contaDestinoTask = ConsultarContaAsync(req.ContaDestinoId);
        await Task.WhenAll(contaOrigemTask, contaDestinoTask);

        var contaOrigem = await contaOrigemTask;
        var contaDestino = await contaDestinoTask;

        if (contaOrigem is null) return (null, "Conta de origem não encontrada", 404);
        if (contaDestino is null) return (null, "Conta de destino não encontrada", 404);

        if (contaOrigem.Saldo < req.Valor)
            return (null, $"Saldo insuficiente. Saldo atual: R${contaOrigem.Saldo:F2}", 422);

        Console.WriteLine($"[MS-TRANSACOES] 💸 INTERAÇÃO 2 — Efetuando transferência consolidada de R${req.Valor:F2}...");
        var (saldoOrigem, erroOrigem, statusOrigem) = await AtualizarSaldoAsync(req.ContaOrigemId, "debitar", req.Valor);
        if (erroOrigem is not null) return (null, erroOrigem, statusOrigem);

        var (saldoDestino, erroDestino, statusDestino) = await AtualizarSaldoAsync(req.ContaDestinoId, "creditar", req.Valor);
        if (erroDestino is not null) return (null, erroDestino, statusDestino);

        var t = await RegistrarAsync("transferencia", req.ContaOrigemId, req.ContaDestinoId, req.Valor,
            req.Descricao ?? $"Transferência para {contaDestino.Titular}");

        Console.WriteLine($"[MS-TRANSACOES] 🔔 INTERAÇÃO 3 — Enviando notificações consolidadas...");
        await Task.WhenAll(
            NotificarAsync(req.ContaOrigemId, contaOrigem.Email, "Transferência enviada",
                $"Transferência de R${req.Valor:F2} para {contaDestino.Titular}. Seu saldo: R${saldoOrigem:F2}"),
            NotificarAsync(req.ContaDestinoId, contaDestino.Email, "Transferência recebida",
                $"Você recebeu R${req.Valor:F2} de {contaOrigem.Titular}. Seu saldo: R${saldoDestino:F2}")
        );

        return (new
        {
            mensagem = "Transferência consolidada realizada com sucesso",
            transacaoId = t.Id,
            valor = req.Valor,
            origem = new { contaId = req.ContaOrigemId, titular = contaOrigem.Titular, saldoAnterior = contaOrigem.Saldo, saldoAtual = saldoOrigem },
            destino = new { contaId = req.ContaDestinoId, titular = contaDestino.Titular, saldoAnterior = contaDestino.Saldo, saldoAtual = saldoDestino },
            notificacoes = new { origem = contaOrigem.Email, destino = contaDestino.Email, status = "enviadas" }
        }, null, 201);
    }

    // ── Resumo ────────────────────────────────────────────────

    public Task<object> ResumoAsync(Guid contaId)
    {
        var transacoes = db.Transacoes
            .Where(t => t.ContaOrigem == contaId || t.ContaDestino == contaId)
            .OrderByDescending(t => t.RealizadaEm)
            .ToList();

        object resumo = new
        {
            contaId,
            totalTransacoes = transacoes.Count,
            ultimaMovimentacao = transacoes.FirstOrDefault()?.RealizadaEm
        };

        return Task.FromResult(resumo);
    }
}
