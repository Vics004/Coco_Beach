using Microsoft.AspNetCore.Mvc;
using Coco_Beach.Servicios;
using Coco_Beach.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

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

                if (string.IsNullOrEmpty(txtUsuario) || string.IsNullOrEmpty(txtClave))
                {
                    ViewData["ErrorMessage"] = "Debe ingresar usuario y contraseña";
                    return View("Autenticar", "_Layout_Login");
                }

                // Buscar usuario por correo, contraseña Y estado ACTIVO
                var usuarioInfo = await (from u in _context.usuario
                                         join p in _context.persona on u.personaid equals p.personaid
                                         join r in _context.rol on p.rolid equals r.rolid
                                         where p.correo == txtUsuario                                   
                                         && p.estado == true   // <-- Solo usuarios activos pueden ingresar
                                         select new
                                         {
                                             usuario = u,
                                             persona = p,
                                             rolNombre = r.nombre,
                                             rolId = r.rolid
                                         }).FirstOrDefaultAsync();

                _logger.LogInformation($"Resultado de la consulta: {(usuarioInfo != null ? "Usuario encontrado y activo" : "Usuario no encontrado o inactivo")}");

                if (usuarioInfo != null)
                {
                    var passwordHasher = new PasswordHasher<object>();

                    var resultado = passwordHasher.VerifyHashedPassword(
                        null,
                        usuarioInfo.usuario.password,
                        txtClave
                    );

                    if (resultado == PasswordVerificationResult.Success)
                    {
                        // LOGIN CORRECTO

                        HttpContext.Session.SetInt32("usuarioId", usuarioInfo.usuario.usuarioid);
                        HttpContext.Session.SetInt32("personaId", usuarioInfo.persona.personaid);
                        HttpContext.Session.SetString("correo", usuarioInfo.persona.correo ?? "");
                        HttpContext.Session.SetString("nombre", usuarioInfo.persona.nombre ?? "");
                        HttpContext.Session.SetString("apellido", usuarioInfo.persona.apellido ?? "");
                        HttpContext.Session.SetString("telefono", usuarioInfo.persona.telefono ?? "");
                        HttpContext.Session.SetString("tipoUsuario", usuarioInfo.rolNombre ?? "");
                        HttpContext.Session.SetInt32("rolId", usuarioInfo.rolId);

                        _logger.LogInformation(
                            $"Usuario {usuarioInfo.persona.correo} ha iniciado sesión correctamente"
                        );

                        return RedirectToAction("Index", "Login");
                    }
                }

                // Verificar si el usuario existe pero está desactivado para mensaje específico
                var usuarioExistente = await (from u in _context.usuario
                                              join p in _context.persona on u.personaid equals p.personaid
                                              where p.correo == txtUsuario
                                              select p).FirstOrDefaultAsync();

                if (usuarioExistente != null && usuarioExistente.estado != true)
                {
                    ViewData["ErrorMessage"] = "Su cuenta está desactivada. Contacte al administrador.";
                }
                else
                {
                    ViewData["ErrorMessage"] = "Credenciales inválidas. Verifica tu correo y contraseña.";
                }

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