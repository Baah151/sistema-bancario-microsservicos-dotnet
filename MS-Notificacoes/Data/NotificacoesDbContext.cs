using Microsoft.EntityFrameworkCore;
using MSNotificacoes.Models;

namespace MSNotificacoes.Data;

public class NotificacoesDbContext(DbContextOptions<NotificacoesDbContext> options) : DbContext(options)
{
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notificacao>(e =>
        {
            e.HasKey(n => n.Id);
        });
    }
}
