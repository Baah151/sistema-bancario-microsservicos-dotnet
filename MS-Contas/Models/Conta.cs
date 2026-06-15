using System.ComponentModel.DataAnnotations;

namespace MSContas.Models;

public class Conta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Titular { get; set; } = string.Empty;
    [Required] public string CPF { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;
    public decimal Saldo { get; set; } = 0;
    public bool Ativa { get; set; } = true;
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
