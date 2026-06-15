using Microsoft.EntityFrameworkCore;
using MSContas.Data;
using MSContas.Models;

namespace MSContas.Services;

public class ContaService(ContasDbContext db)
{
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
        return (conta, null, 201);
    }

    public async Task<Conta?> BuscarContaAsync(Guid id) =>
        await db.Contas.FindAsync(id);

    public async Task<(object? resultado, string? erro, int status)> ConsultarSaldoAsync(Guid id)
    {
        var conta = await db.Contas.FindAsync(id);
        if (conta is null) return (null, "Conta não encontrada", 404);
        if (!conta.Ativa) return (null, "Conta encerrada", 400);
        return (new { contaId = conta.Id, saldo = conta.Saldo }, null, 200);
    }

    public async Task<(object? resultado, string? erro, int status)> AtualizarSaldoAsync(Guid id, AtualizarSaldoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Operacao) || req.Valor <= 0)
            return (null, "Informe operacao (creditar/debitar) e valor positivo", 400);

        var conta = await db.Contas.FindAsync(id);
        if (conta is null) return (null, "Conta não encontrada", 404);
        if (!conta.Ativa) return (null, "Conta encerrada", 400);

        if (req.Operacao == "debitar" && conta.Saldo < req.Valor)
            return (null, "Saldo insuficiente", 422);

        conta.Saldo = req.Operacao == "creditar"
            ? conta.Saldo + req.Valor
            : conta.Saldo - req.Valor;

        await db.SaveChangesAsync();

        Console.WriteLine($"[MS-CONTAS] Saldo atualizado: conta={conta.Id} | {req.Operacao} R${req.Valor} | Novo saldo: R${conta.Saldo:F2}");
        return (new { contaId = conta.Id, novoSaldo = conta.Saldo }, null, 200);
    }

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
        return (null, 200);
    }
}
