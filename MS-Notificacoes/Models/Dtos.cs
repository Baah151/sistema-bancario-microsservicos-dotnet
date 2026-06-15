namespace MSNotificacoes.Models;

public record EnviarNotificacaoRequest(string ContaId, string Email, string Titulo, string Mensagem);
