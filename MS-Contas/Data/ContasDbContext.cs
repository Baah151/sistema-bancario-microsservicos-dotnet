using Microsoft.EntityFrameworkCore;
using MSContas.Models;

namespace MSContas.Data;

public class ContasDbContext(DbContextOptions<ContasDbContext> options) : DbContext(options)
{
    public DbSet<Conta> Contas => Set<Conta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conta>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.CPF).IsUnique();
            e.Property(c => c.Saldo).HasColumnType("TEXT");
        });
    }
}
