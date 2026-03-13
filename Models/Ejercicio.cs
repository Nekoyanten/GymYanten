using GymYanten.Models;
using System.ComponentModel.DataAnnotations;

namespace GymYanten.Models
{
    
    public class Ejercicio
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del ejercicio es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "La descripción no puede superar 1000 caracteres.")]
        [Display(Name = "Descripción")]
        [DataType(DataType.MultilineText)]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El grupo muscular es obligatorio.")]
        [Display(Name = "Grupo Muscular")]
        public GrupoMuscular GrupoMuscular { get; set; }

        [Required]
        [Display(Name = "¿Requiere Equipo?")]
        public bool RequiereEquipo { get; set; }



        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;


        /// <summary>Registros de progreso que incluyen este ejercicio.</summary>
        public virtual ICollection<ProgresoEntrenamiento> Progresos { get; set; }
            = new List<ProgresoEntrenamiento>();
    }


    public enum GrupoMuscular
    {
        [Display(Name = "Pecho")] Pecho,
        [Display(Name = "Espalda")] Espalda,
        [Display(Name = "Hombros")] Hombros,
        [Display(Name = "Bíceps")] Biceps,
        [Display(Name = "Tríceps")] Triceps,
        [Display(Name = "Piernas")] Piernas,
        [Display(Name = "Glúteos")] Gluteos,
        [Display(Name = "Abdomen")] Abdomen,
        [Display(Name = "Cardio")] Cardio,
        [Display(Name = "Cuerpo Completo")] CuerpoCompleto
    }
}