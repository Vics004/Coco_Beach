using Microsoft.AspNetCore.Mvc;
using Coco_Beach.Servicios;
using Coco_Beach.Models;
using Microsoft.EntityFrameworkCore;

namespace Coco_Beach.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;
        private readonly Coco_BeachDbContext _context;

        public LoginController(ILogger<LoginController> logger, Coco_BeachDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [AutenticationAttribute.Autenticacion]
        public IActionResult Index()
        {
            // Obtener datos de sesión
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");
            var rolNombre = HttpContext.Session.GetString("tipoUsuario");
            var nombreUsuario = HttpContext.Session.GetString("nombre");
            var apellidoUsuario = HttpContext.Session.GetString("apellido");

            if (usuarioId == null)
            {
                return RedirectToAction("Autenticar", "Login");
            }

            // Asignar el layout correspondiente según el rol
            switch (rolNombre)
            {
                case "Administrador":
                    ViewBag.Layout = "_Layout_Administrador";
                    break;
                case "Dueño":
                    ViewBag.Layout = "_Layout_Dueño";
                    break;
                case "Encargado":
                    ViewBag.Layout = "_Layout_Encargado";
                    break;
                case "Gerente de Hotel":
                    ViewBag.Layout = "_Layout_GerenteHotel";
                    break;
                case "Gerente de Rancho":
                    ViewBag.Layout = "_Layout_GerenteRancho";
                    break;
                default:
                    ViewBag.Layout = "_Layout";
                    break;
            }

            ViewBag.nombre = nombreUsuario;
            ViewBag.apellido = apellidoUsuario;
            ViewBag.rol = rolNombre;

            return View();
        }

        public IActionResult Autenticar()
        {
            // Si ya hay sesión activa, redirigir al Index
            if (HttpContext.Session.GetInt32("usuarioId") != null)
            {
                return RedirectToAction("Index", "Login");
            }

            ViewData["ErrorMessage"] = "";
            return View("Autenticar", "_Layout_Login");
        }

        [HttpPost]
        public async Task<IActionResult> Autenticar(string txtUsuario, string txtClave)
        {
            try
            {
                _logger.LogInformation($"Intento de login - Usuario: {txtUsuario}");

                // Verificar que los parámetros no sean nulos
                if (string.IsNullOrEmpty(txtUsuario) || string.IsNullOrEmpty(txtClave))
                {
                    ViewData["ErrorMessage"] = "Debe ingresar usuario y contraseña";
                    return View("Autenticar", "_Layout_Login");
                }

                // Buscar usuario por correo y contraseña - CORREGIDO: usando minúsculas para los nombres de columna
                var usuarioInfo = await (from u in _context.usuario
                                         join p in _context.persona on u.personaid equals p.personaid  // Cambiado: personaId -> personaid
                                         join r in _context.rol on p.rolid equals r.rolid  // Cambiado: rolId -> rolid
                                         where p.correo == txtUsuario
                                         && u.password == txtClave
                                         select new
                                         {
                                             usuario = u,
                                             persona = p,
                                             rolNombre = r.nombre,
                                             rolId = r.rolid  // Cambiado: rolId -> rolid
                                         }).FirstOrDefaultAsync();

                _logger.LogInformation($"Resultado de la consulta: {(usuarioInfo != null ? "Usuario encontrado" : "Usuario no encontrado")}");

                if (usuarioInfo != null)
                {

                    // Guardar datos en sesión - CORREGIDO: usando los nombres correctos de propiedades
                    HttpContext.Session.SetInt32("usuarioId", usuarioInfo.usuario.usuarioid);  // Cambiado: usuarioId -> usuarioid
                    HttpContext.Session.SetInt32("personaId", usuarioInfo.persona.personaid);  // Cambiado: personaId -> personaid
                    HttpContext.Session.SetString("correo", usuarioInfo.persona.correo ?? "");
                    HttpContext.Session.SetString("nombre", usuarioInfo.persona.nombre ?? "");
                    HttpContext.Session.SetString("apellido", usuarioInfo.persona.apellido ?? "");
                    HttpContext.Session.SetString("telefono", usuarioInfo.persona.telefono ?? "");
                    HttpContext.Session.SetString("tipoUsuario", usuarioInfo.rolNombre ?? "");
                    HttpContext.Session.SetInt32("rolId", usuarioInfo.rolId);  // Este sí está bien porque es la variable que creamos

                    _logger.LogInformation($"Usuario {usuarioInfo.persona.correo} ha iniciado sesión correctamente");
                    return RedirectToAction("Index", "Login");
                }

                ViewData["ErrorMessage"] = "Credenciales inválidas. Verifica tu correo y contraseña.";
                _logger.LogWarning($"Intento de login fallido para el usuario: {txtUsuario}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el proceso de autenticación");
                var errorMessage = "Error en el servidor. Intenta nuevamente.";

#if DEBUG
                errorMessage = $"Error: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" - Inner: {ex.InnerException.Message}";
                }
#endif

                ViewData["ErrorMessage"] = errorMessage;
            }

            return View("Autenticar", "_Layout_Login");
        }

        public IActionResult Logout()
        {
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioId.HasValue)
            {
                _logger.LogInformation($"Usuario ID {usuarioId} ha cerrado sesión");
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Autenticar", "Login");
        }
    }
}
