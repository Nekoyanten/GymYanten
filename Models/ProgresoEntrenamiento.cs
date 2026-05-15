// SECURITY FIX #6 — PesoUsadoKg: rango máximo reducido a 300 kg (era 1000 kg, valor irreal)

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymYanten.Models
{
    public class ProgresoEntrenamiento
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El cliente es obligatorio.")]
        public string ClienteId { get; set; } = string.Empty;

        [ForeignKey(nameof(ClienteId))]
        [Display(Name = "Cliente")]
        public virtual ApplicationUser? Cliente { get; set; }

        [Required(ErrorMessage = "La rutina es obligatoria.")]
        public int RutinaId { get; set; }

        [ForeignKey(nameof(RutinaId))]
        [Display(Name = "Rutina")]
        public virtual Rutina? Rutina { get; set; }

        [Required(ErrorMessage = "El ejercicio es obligatorio.")]
        public int EjercicioId { get; set; }

        [ForeignKey(nameof(EjercicioId))]
        [Display(Name = "Ejercicio")]
        public virtual Ejercicio? Ejercicio { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Las series realizadas son obligatorias.")]
        [Range(1, 100, ErrorMessage = "Las series deben estar entre 1 y 100.")]
        [Display(Name = "Series Realizadas")]
        public int SeriesRealizadas { get; set; }

        [Required(ErrorMessage = "Las repeticiones son obligatorias.")]
        [Range(1, 500, ErrorMessage = "Las repeticiones deben estar entre 1 y 500.")]
        [Display(Name = "Repeticiones")]
        public int Repeticiones { get; set; }

        // SECURITY FIX #6 — Máximo reducido de 1000 kg a 300 kg.
        // Justificación: el récord mundial de peso muerto es ~500 kg en competición
        // controlada; en contexto de gimnasio el techo de 300 kg es más que suficiente
        // y bloquea entradas anómalas que podrían indicar inyección de datos o errores
        // de entrada que corrompan estadísticas y reportes.
        [Range(0, 300, ErrorMessage = "El peso debe estar entre 0 y 300 kg. Si superas ese valor, contacta a tu entrenador.")]
        [Display(Name = "Peso Usado (kg)")]
        [Column(TypeName = "decimal(6,2)")]
        public decimal? PesoUsadoKg { get; set; }

        [StringLength(500)]
        [Display(Name = "Notas")]
        [DataType(DataType.MultilineText)]
        public string? Notas { get; set; }

        [Display(Name = "Fecha de Registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    }
}
