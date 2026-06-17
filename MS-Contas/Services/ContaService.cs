using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MSContas.Data;
using MSContas.Models;

namespace MSContas.Services;

// Contém toda a lógica de negócio das contas bancárias.
// Também se comunica com MS-Notificacoes e MS-Transacoes quando necessário.
public class ContaService(ContasDbContext db, IHttpClientFactory httpFactory)
{
    private HttpClient NotificacoesClient => httpFactory.CreateClient("MSNotificacoes");
    private HttpClient TransacoesClient => httpFactory.CreateClient("MSTransacoes");

    // Envia uma notificação ao titular da conta via MS-Notificacoes.
    // Se o serviço de notificações estiver fora do ar, apenas registra o aviso e segue normalmente.
    private async Task NotificarAsync(Guid contaId, string email, string titulo, string mensagem)
    {
        try
        {
            await NotificacoesClient.PostAsJsonAsync("/notificacoes/enviar",
                new { contaId = contaId.ToString(), email, titulo, mensagem });
            Console.WriteLine($"[MS-CONTAS → MS-NOTIFICACOES] Notificação enviada: {titulo}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MS-CONTAS] Aviso: notificação não enviada — {ex.Message}");
        }
    }

    // Cria uma nova conta bancária.
    // Valida se os campos obrigatórios foram preenchidos e se o CPF já existe no sistema.
    // Ao final, dispara uma notificação de boas-vindas ao novo titular.
    public async Task<(Conta? conta, string? erro, int status)> CriarContaAsync(CriarContaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Titular) || string.IsNullOrWhiteSpace(req.CPF) || string.IsNullOrWhiteSpace(req.Email))
            return (null, "Campos obrigatórios: titular, cpf, email", 400);

        if (await db.Contas.AnyAsync(c => c.CPF == req.CPF))
            return (null, "CPF já cadastrado", 409);

        var conta = new Conta
        {
            Titular = req.Titular,
            CPF = req.CPF,
            Email = req.Email,
            Saldo = req.SaldoInicial,
        };

        db.Contas.Add(conta);
        await db.SaveChangesAsync();

        Console.WriteLine($"[MS-CONTAS] Conta criada: {conta.Id} | Titular: {conta.Titular}");

        // Conexão 1: avisa o titular que a conta foi aberta com sucesso
        await NotificarAsync(conta.Id, conta.Email, "Bem-vindo ao Banco!",
            $"Olá {conta.Titular}, sua conta foi criada com sucesso! Saldo inicial: R${conta.Saldo:F2}.");

        return (conta, null, 201);
    }

    // Busca os dados de uma conta pelo ID.
    // Além dos dados da própria conta, consulta o MS-Transacoes em tempo real
    // para trazer o número total de transações e a data da última movimentação.
    public async Task<(Conta? conta, object? resumoTransacoes)> BuscarContaAsync(Guid id)
    {
        var conta = await db.Contas.FindAsync(id);
        if (conta is null) return (null, null);

        Console.WriteLine($"[MS-CONTAS] ← Consulta de conta recebida: {conta.Titular} (ID: {id})");

        // Conexão 2: busca o resumo de transações diretamente no MS-Transacoes
        object? resumo = null;
        try
        {
            var resp = await TransacoesClient.GetAsync($"/transacoes/{id}/resumo");
            if (resp.IsSuccessStatusCode)
            {
                resumo = await resp.Content.ReadFromJsonAsync<object>();
                Console.WriteLine($"[MS-CONTAS → MS-TRANSACOES] Resumo de transações obtido para conta {id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MS-CONTAS] Aviso: resumo de transações não obtido — {ex.Message}");
        }

        return (conta, resumo);
    }

    // Retorna apenas o saldo atual de uma conta ativa.
    // Rejeita a consulta se a conta não existir ou já estiver encerrada.
    public async Task<(object? resultado, string? erro, int status)> ConsultarSaldoAsync(Guid id)
    {
        var conta = await db.Contas.FindAsync(id);
        if (conta is null) return (null, "Conta não encontrada", 404);
        if (!conta.Ativa) return (null, "Conta encerrada", 400);
        return (new { contaId = conta.Id, saldo = conta.Saldo }, null, 200);
    }

    // Atualiza o saldo de uma conta — pode creditar (depositar) ou debitar (sacar).
    // Endpoint usado internamente pelo MS-Transacoes; não é chamado diretamente pelo cliente.
    // Impede saque se o saldo for insuficiente.
    public async Task<(object? resultado, string? erro, int status)> AtualizarSaldoAsync(Guid id, AtualizarSaldoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Operacao) || req.Valor <= 0)
            return (null, "Informe operacao (creditar/debitar) e valor positivo", 400);

        var conta = await db.Contas.FindAsync(id);
        if (conta is null) return (null, "Conta não encontrada", 404);
        if (!conta.Ativa) return (null, "Conta encerrada", 400);

        if (req.Operacao == "debitar" && conta.Saldo < req.Valor)
            return (null, "Saldo insuficiente", 422);

        Console.WriteLine($"[MS-CONTAS] ← Atualização de saldo recebida: {req.Operacao} R${req.Valor:F2} na conta de {conta.Titular}");

        conta.Saldo = req.Operacao == "creditar"
            ? conta.Saldo + req.Valor
            : conta.Saldo - req.Valor;

        await db.SaveChangesAsync();

        Console.WriteLine($"[MS-CONTAS] ✅ Saldo atualizado: {conta.Titular} | novo saldo: R${conta.Saldo:F2}");
        return (new { contaId = conta.Id, novoSaldo = conta.Saldo }, null, 200);
    }

    // Encerra uma conta bancária.
    // Só permite o encerramento se o saldo estiver zerado — caso contrário, orienta o titular a sacar antes.
    // Ao encerrar, notifica o titular confirmando o fechamento da conta.
    public async Task<(string? erro, int status)> EncerrarContaAsync(Guid id)
    {
        var conta = await db.Contas.FindAsync(id);
        if (conta is null) return ("Conta não encontrada", 404);
        if (!conta.Ativa) return ("Conta já encerrada", 400);
        if (conta.Saldo > 0)
            return ($"Não é possível encerrar conta com saldo de R${conta.Saldo:F2}. Realize o saque antes.", 422);

        conta.Ativa = false;
        await db.SaveChangesAsync();

        Console.WriteLine($"[MS-CONTAS] Conta encerrada: {conta.Id}");

        // Conexão 1: avisa o titular que a conta foi encerrada
        await NotificarAsync(conta.Id, conta.Email, "Conta encerrada",
            $"Olá {conta.Titular}, sua conta bancária foi encerrada com sucesso.");

        return (null, 200);
    }
}
