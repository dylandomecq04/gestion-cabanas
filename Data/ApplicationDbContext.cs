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
        }
    }
}
