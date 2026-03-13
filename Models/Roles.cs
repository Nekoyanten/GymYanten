namespace GymYanten.Models
{
    public class Roles
    {
        public const string Administrador = "Administrador";
        public const string Entrenador = "Entrenador";
        public const string Cliente = "Cliente";

        /// <summary>Arreglo útil para iterar al hacer seed de roles en la BD.</summary>
        public static readonly string[] Todos = { Administrador, Entrenador, Cliente };
    }
}
