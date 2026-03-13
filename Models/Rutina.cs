using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymYanten.Models
{
    public class Rutina
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de la rutina es obligatorio.")]
        [StringLength(150)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Descripción")]
        [DataType(DataType.MultilineText)]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El nivel es obligatorio.")]
        [Display(Name = "Nivel")]
        public NivelRutina Nivel { get; set; }

        [Required(ErrorMessage = "La duración estimada es obligatoria.")]
        [Range(1, 300, ErrorMessage = "La duración debe estar entre 1 y 300 minutos.")]
        [Display(Name = "Duración Estimada (minutos)")]
        public int DuracionEstimadaMinutos { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [Display(Name = "Activa")]
        public bool Activa { get; set; } = true;

        [Required]
        public string EntrenadorId { get; set; } = string.Empty;

        [ForeignKey(nameof(EntrenadorId))]
        [Display(Name = "Entrenador")]
        public virtual ApplicationUser? Entrenador { get; set; }

        public virtual ICollection<ProgresoEntrenamiento> Progresos { get; set; }
            = new List<ProgresoEntrenamiento>();
    }
    public enum NivelRutina
    {
        [Display(Name = "Principiante")] Principiante,
        [Display(Name = "Intermedio")] Intermedio,
        [Display(Name = "Avanzado")] Avanzado
    }
}

