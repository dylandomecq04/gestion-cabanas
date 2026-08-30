using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Cabana> Cabanas => Set<Cabana>();
        public DbSet<FotoCabana> Fotos => Set<FotoCabana>();
        public DbSet<Reserva> Reservas => Set<Reserva>();
        public DbSet<AdminUsuario> AdminUsuarios => Set<AdminUsuario>();
        public DbSet<TarifaDia> TarifasDias => Set<TarifaDia>();
        public DbSet<InformacionSitio> InformacionSitio => Set<InformacionSitio>();
        public DbSet<PreguntaFrecuente> PreguntasFrecuentes => Set<PreguntaFrecuente>();
        public DbSet<Promocion> Promociones => Set<Promocion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cabana>()
                .HasMany(c => c.Fotos)
                .WithOne(f => f.Cabana)
                .HasForeignKey(f => f.CabanaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Cabana>()
                .HasMany(c => c.Reservas)
                .WithOne(r => r.Cabana)
                .HasForeignKey(r => r.CabanaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AdminUsuario>()
                .HasIndex(u => u.NombreUsuario)
                .IsUnique();

            modelBuilder.Entity<Cabana>()
                .HasMany(c => c.TarifasDias)
                .WithOne(t => t.Cabana)
                .HasForeignKey(t => t.CabanaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TarifaDia>()
                .HasIndex(t => new { t.CabanaId, t.Fecha })
                .IsUnique();
        }
    }
}
