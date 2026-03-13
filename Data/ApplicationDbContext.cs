using GymYanten.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GymYanten.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Ejercicio> Ejercicios { get; set; }
        public DbSet<Rutina> Rutinas { get; set; }
        public DbSet<ProgresoEntrenamiento> Progresos { get; set; }

        // ApplicationUser ya está incluido por IdentityDbContext → tabla AspNetUsers

        protected override void OnModelCreating(ModelBuilder builder)
        {
          
            base.OnModelCreating(builder);

            // ── Ejercicio ──────────────────────────
            builder.Entity<Ejercicio>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Nombre)
                      .IsRequired()
                      .HasMaxLength(150);

                entity.Property(e => e.Descripcion)
                      .HasMaxLength(1000);

                // Índice único: no puede haber dos ejercicios con el mismo nombre
                entity.HasIndex(e => e.Nombre).IsUnique();
            });

            // ── Rutina ─────────────────────────────
            builder.Entity<Rutina>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Nombre)
                      .IsRequired()
                      .HasMaxLength(150);

                entity.Property(r => r.Descripcion)
                      .HasMaxLength(1000);

                // Relación: Rutina → Entrenador (ApplicationUser)
                // Si se elimina un Entrenador, se restringe (no se borra en cascada)
                entity.HasOne(r => r.Entrenador)
                      .WithMany()
                      .HasForeignKey(r => r.EntrenadorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── ProgresoEntrenamiento ──────────────
            builder.Entity<ProgresoEntrenamiento>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.PesoUsadoKg)
                      .HasColumnType("decimal(6,2)");

                entity.Property(p => p.Notas)
                      .HasMaxLength(500);

                // Relación: Progreso → Cliente (ApplicationUser)
                entity.HasOne(p => p.Cliente)
                      .WithMany(u => u.Progresos)
                      .HasForeignKey(p => p.ClienteId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relación: Progreso → Rutina
                entity.HasOne(p => p.Rutina)
                      .WithMany(r => r.Progresos)
                      .HasForeignKey(p => p.RutinaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relación: Progreso → Ejercicio
                entity.HasOne(p => p.Ejercicio)
                      .WithMany(e => e.Progresos)
                      .HasForeignKey(p => p.EjercicioId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
