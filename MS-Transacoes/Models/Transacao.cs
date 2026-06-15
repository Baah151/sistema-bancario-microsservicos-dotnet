namespace MSTransacoes.Models;

public class Transacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Tipo { get; set; } = string.Empty;
    public Guid? ContaOrigem { get; set; }
    public Guid? ContaDestino { get; set; }
    public decimal Valor { get; set; }
    public string? Descricao { get; set; }
    public string Status { get; set; } = "concluida";
    public DateTime RealizadaEm { get; set; } = DateTime.UtcNow;
}
