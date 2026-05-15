using System.ComponentModel.DataAnnotations;

namespace GymYanten.Models.ViewModels
{
    // ── DTOs para API de Dashboard ─────────────────────────

    public class VolumenDiarioDto
    {
        public DateTime Fecha { get; set; }
        public decimal VolumenTotal { get; set; }
        public int Entrenamientos { get; set; }
    }

    public class TopEjercicioDto
    {
        public int EjercicioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string GrupoMuscular { get; set; } = string.Empty;
        public int TotalSesiones { get; set; }
        public decimal? MaxPeso { get; set; }
        public decimal VolumenTotal { get; set; }
    }

    public class DistribucionMuscularDto
    {
        public string GrupoMuscular { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public double Porcentaje { get; set; }
    }

    public class RachaDto
    {
        public int RachaActual { get; set; }
        public int RachaMaxima { get; set; }
        public DateTime? UltimaFecha { get; set; }
        public int TotalDiasEntrenados { get; set; }
    }

    public class PrRecordDto
    {
        public int EjercicioId { get; set; }
        public string NombreEjercicio { get; set; } = string.Empty;
        public decimal? MaxPeso { get; set; }
        public int MaxReps { get; set; }
        public decimal? MaxVolumen { get; set; }
        public DateTime? FechaPR { get; set; }
    }

    // ── ViewModels para Vistas ─────────────────────────────

    public class DashboardClienteViewModel
    {
        public List<VolumenDiarioDto> VolumenDiario { get; set; } = new();
        public List<TopEjercicioDto> TopEjercicios { get; set; } = new();
        public List<DistribucionMuscularDto> DistribucionMuscular { get; set; } = new();
        public RachaDto Racha { get; set; } = new();
        public List<PrRecordDto> RecordsPersonales { get; set; } = new();
        public List<ProgresoEntrenamiento> UltimosRegistros { get; set; } = new();
        public string NombreCliente { get; set; } = string.Empty;
    }

    public class DashboardEntrenadorViewModel
    {
        public List<ClienteActivoDto> ClientesMasActivos { get; set; } = new();
        public List<VolumenSemanalDto> VolumenPromedioSemanal { get; set; } = new();
        public List<ClienteInactivoDto> ClientesInactivos { get; set; } = new();
        public int TotalClientes { get; set; }
        public int ClientesActivosEstaSemana { get; set; }
    }

    public class ClienteActivoDto
    {
        public string ClienteId { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public int TotalEntrenamientos { get; set; }
        public decimal VolumenTotal { get; set; }
        public DateTime? UltimaActividad { get; set; }
    }

    public class VolumenSemanalDto
    {
        public int Anio { get; set; }
        public int Semana { get; set; }
        public DateTime FechaInicioSemana { get; set; }
        public decimal VolumenPromedio { get; set; }
        public int TotalClientes { get; set; }
    }

    public class ClienteInactivoDto
    {
        public string ClienteId { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime? UltimaActividad { get; set; }
        public int DiasSinActividad { get; set; }
    }
}