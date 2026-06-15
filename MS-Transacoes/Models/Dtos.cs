namespace MSTransacoes.Models;

public record DepositoRequest(Guid ContaId, decimal Valor, string? Descricao);
public record SaqueRequest(Guid ContaId, decimal Valor, string? Descricao);
public record TransferenciaRequest(Guid ContaOrigemId, Guid ContaDestinoId, decimal Valor, string? Descricao);

// Resposta de consulta de conta do MS-Contas
public record ContaResponse(Guid Id, string Titular, string Email, decimal Saldo, bool Ativa);
public record SaldoResponse(Guid ContaId, decimal NovoSaldo);
