using Microsoft.EntityFrameworkCore;
using MSTransacoes.Models;

namespace MSTransacoes.Data;

public class TransacoesDbContext(DbContextOptions<TransacoesDbContext> options) : DbContext(options)
{
    public DbSet<Transacao> Transacoes => Set<Transacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transacao>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Valor).HasColumnType("TEXT");
        });
    }
}
