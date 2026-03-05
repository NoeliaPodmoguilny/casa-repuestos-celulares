using CasaRepuestos.Models;

namespace CasaRepuestos.Services
{
    public static class SesionService
    {
        // Propiedad estática para almacenar la información del empleado logueado
        public static Empleado EmpleadoLogueado { get; private set; }

        // Método para iniciar sesión, asignando la información del empleado logueado
        public static void IniciarSesion(int idEmpleado, string usuario, string rol)
        {
            EmpleadoLogueado = new Empleado
            {
                IdEmpleado = idEmpleado,
                Usuario = usuario,
                Rol = rol
            };
        }
        // Método para cerrar sesión, limpiando la información del empleado logueado
        public static void CerrarSesion()
        {
            EmpleadoLogueado = null;
        }
        // Método para verificar si hay un empleado logueado
        public static bool EstaLogueado()
        {
            return EmpleadoLogueado != null;
        }
        // Métodos para obtener información del empleado logueado
        public static int ObtenerIdEmpleadoLogueado()
        {
            return EstaLogueado() ? EmpleadoLogueado.IdEmpleado : 0;
        }
        // Método para obtener el nombre de usuario del empleado logueado
        public static string ObtenerUsuarioLogueado()
        {
            return EstaLogueado() ? EmpleadoLogueado.Usuario : string.Empty;
        }
        // Método para obtener el rol del empleado logueado
        public static string ObtenerRolLogueado()
        {
            return EstaLogueado() ? EmpleadoLogueado.Rol : string.Empty;
        }
    }
}