namespace MSNotificacoes.Models;

public record EnviarNotificacaoRequest(string ContaId, string Email, string Titulo, string Mensagem);

// Resposta do MS-Contas usada para enriquecer o histórico de notificações
public record ContaInfo(Guid Id, string Titular, string Email, decimal Saldo, bool Ativa);
