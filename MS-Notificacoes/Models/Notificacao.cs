using System.ComponentModel.DataAnnotations;

namespace MSNotificacoes.Models;

public class Notificacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string ContaId { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Titulo { get; set; } = string.Empty;
    [Required] public string Mensagem { get; set; } = string.Empty;
    public bool Enviada { get; set; } = true;
    public DateTime EnviadaEm { get; set; } = DateTime.UtcNow;
}
