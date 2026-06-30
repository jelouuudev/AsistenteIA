using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Data;

public class AsistenteDbContext : DbContext, IUnitOfWork
{
    public AsistenteDbContext(DbContextOptions<AsistenteDbContext> options) : base(options)
    {
    }

    public DbSet<Conversacion> Conversaciones => Set<Conversacion>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsistenteDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
