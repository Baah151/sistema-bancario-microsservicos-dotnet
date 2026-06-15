namespace MSContas.Models;

public record CriarContaRequest(string Titular, string CPF, string Email, decimal SaldoInicial = 0);
public record AtualizarSaldoRequest(string Operacao, decimal Valor);
