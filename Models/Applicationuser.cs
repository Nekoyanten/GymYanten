using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace GymYanten.Models
{
    public class ApplicationUser : IdentityUser
    {

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Apellido")]
        public string Apellido { get; set; } = string.Empty;

        [Display(Name = "Fecha de Nacimiento")]
        [DataType(DataType.Date)]
        public DateTime? FechaNacimiento { get; set; }

        [StringLength(20)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [StringLength(250)]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }

        [Display(Name = "Foto de Perfil (URL)")]
        public string? FotoPerfil { get; set; }

        [Display(Name = "Fecha de Registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string NombreCompleto => $"{Nombre} {Apellido}";



        public virtual ICollection<ProgresoEntrenamiento> Progresos { get; set; }
            = new List<ProgresoEntrenamiento>();
    }
}
