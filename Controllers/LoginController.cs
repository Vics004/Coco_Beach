using Microsoft.AspNetCore.Mvc;
using Coco_Beach.Servicios;
using Coco_Beach.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Coco_Beach.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;
        private readonly Coco_BeachDbContext _context;
        private readonly IMemoryCache _cache;

        // Constantes para el rate limiting
        private const int MaxIntentos = 5;
        private static readonly TimeSpan TiempoBloqueo = TimeSpan.FromMinutes(1);

        public LoginController(ILogger<LoginController> logger, Coco_BeachDbContext context, IMemoryCache cache)
        {
            _logger = logger;
            _context = context;
            _cache = cache;
        }

        [AutenticationAttribute.Autenticacion]
        public IActionResult Index()
        {
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");
            var rolNombre = HttpContext.Session.GetString("tipoUsuario");
            var nombreUsuario = HttpContext.Session.GetString("nombre");
            var apellidoUsuario = HttpContext.Session.GetString("apellido");

            if (usuarioId == null)
                return RedirectToAction("Autenticar", "Login");

            ViewBag.nombre = nombreUsuario;
            ViewBag.apellido = apellidoUsuario;
            ViewBag.rol = rolNombre;

            return View();
        }

        public IActionResult Autenticar()
        {
            if (HttpContext.Session.GetInt32("usuarioId") != null)
                return RedirectToAction("Index", "Login");

            ViewData["ErrorMessage"] = "";
            return View("Autenticar", "_Layout_Login");
        }

        [HttpPost]
        public async Task<IActionResult> Autenticar(string txtUsuario, string txtClave)
        {
            try
            {
                _logger.LogInformation($"Intento de login - Usuario: {txtUsuario}");

                // ── 1. Obtener IP del cliente ──────────────────────────────────────
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var cacheKeyIntentos = $"login_intentos_{ip}";
                var cacheKeyBloqueo = $"login_bloqueado_{ip}";

                // ── 2. Verificar si está bloqueado ─────────────────────────────────
                if (_cache.TryGetValue(cacheKeyBloqueo, out DateTime desbloqueaEn))
                {
                    var restante = desbloqueaEn - DateTime.UtcNow;
                    if (restante > TimeSpan.Zero)
                    {
                        var minutos = (int)restante.TotalMinutes;
                        var segundos = restante.Seconds;
                        ViewData["ErrorMessage"] =
                            $"Demasiados intentos fallidos. Espera {minutos}m {segundos}s antes de intentarlo de nuevo.";
                        ViewData["EstaBlockeado"] = true;
                        // ── Timestamp exacto para el countdown del cliente ──
                        ViewData["DesbloqueaEn"] = new DateTimeOffset(desbloqueaEn, TimeSpan.Zero).ToUnixTimeMilliseconds();
                        return View("Autenticar", "_Layout_Login");
                    }
                    // El tiempo ya pasó — limpiar AMBAS claves
                    _cache.Remove(cacheKeyBloqueo);
                    _cache.Remove(cacheKeyIntentos);
                }

                // ── 3. Validar campos vacíos ───────────────────────────────────────
                if (string.IsNullOrEmpty(txtUsuario) || string.IsNullOrEmpty(txtClave))
                {
                    ViewData["ErrorMessage"] = "Debe ingresar usuario y contraseña.";
                    return View("Autenticar", "_Layout_Login");
                }

                // ── 4. Consultar usuario activo ────────────────────────────────────
                var usuarioInfo = await (from u in _context.usuario
                                         join p in _context.persona on u.personaid equals p.personaid
                                         join r in _context.rol on p.rolid equals r.rolid
                                         where p.correo == txtUsuario
                                            && p.estado == true
                                         select new
                                         {
                                             usuario = u,
                                             persona = p,
                                             rolNombre = r.nombre,
                                             rolId = r.rolid
                                         }).FirstOrDefaultAsync();

                bool credencialesValidas = false;

                if (usuarioInfo != null)
                {
                    var hasher = new PasswordHasher<object>();
                    var resultado = hasher.VerifyHashedPassword(null, usuarioInfo.usuario.password, txtClave);
                    credencialesValidas = resultado == PasswordVerificationResult.Success;
                }

                // ── 5. Login exitoso ───────────────────────────────────────────────
                if (credencialesValidas)
                {
                    _cache.Remove(cacheKeyIntentos);
                    _cache.Remove(cacheKeyBloqueo);

                    HttpContext.Session.SetInt32("usuarioId", usuarioInfo!.usuario.usuarioid);
                    HttpContext.Session.SetInt32("personaId", usuarioInfo.persona.personaid);
                    HttpContext.Session.SetString("correo", usuarioInfo.persona.correo ?? "");
                    HttpContext.Session.SetString("nombre", usuarioInfo.persona.nombre ?? "");
                    HttpContext.Session.SetString("apellido", usuarioInfo.persona.apellido ?? "");
                    HttpContext.Session.SetString("telefono", usuarioInfo.persona.telefono ?? "");
                    HttpContext.Session.SetString("tipoUsuario", usuarioInfo.rolNombre ?? "");
                    HttpContext.Session.SetInt32("rolId", usuarioInfo.rolId);

                    _logger.LogInformation($"Login exitoso: {usuarioInfo.persona.correo}");
                    return RedirectToAction("Index", "Login");
                }

                // ── 6. Credenciales inválidas — registrar intento ──────────────────
                var intentos = _cache.TryGetValue(cacheKeyIntentos, out int intentosActuales)
                    ? intentosActuales + 1
                    : 1;

                _cache.Set(cacheKeyIntentos, intentos, TiempoBloqueo + TimeSpan.FromSeconds(30));

                var intentosRestantes = MaxIntentos - intentos;
                _logger.LogWarning($"Login fallido IP={ip} usuario={txtUsuario} intento={intentos}/{MaxIntentos}");

                if (intentos >= MaxIntentos)
                {
                    var desbloqueaEn2 = DateTime.UtcNow.Add(TiempoBloqueo);
                    _cache.Set(cacheKeyBloqueo, desbloqueaEn2, TiempoBloqueo + TimeSpan.FromSeconds(30));
                    _cache.Remove(cacheKeyIntentos);

                    ViewData["ErrorMessage"] =
                        $"Has superado el límite de {MaxIntentos} intentos. Tu acceso está bloqueado por {(int)TiempoBloqueo.TotalMinutes} minutos.";
                    ViewData["EstaBlockeado"] = true;
                    // ── Timestamp exacto para el countdown del cliente ──
                    ViewData["DesbloqueaEn"] = new DateTimeOffset(desbloqueaEn2, TimeSpan.Zero).ToUnixTimeMilliseconds();
                    _logger.LogWarning($"IP {ip} bloqueada por {TiempoBloqueo.TotalMinutes} minutos.");
                }
                else
                {
                    var usuarioExiste = await (from u in _context.usuario
                                               join p in _context.persona on u.personaid equals p.personaid
                                               where p.correo == txtUsuario
                                               select p).FirstOrDefaultAsync();

                    ViewData["ErrorMessage"] = (usuarioExiste != null && usuarioExiste.estado != true)
                        ? "Su cuenta está desactivada. Contacte al administrador."
                        : $"Credenciales inválidas. Te quedan {intentosRestantes} intento(s) antes del bloqueo.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la autenticación");
                var msg = "Error en el servidor. Intenta nuevamente.";
#if DEBUG
                msg = $"Error: {ex.Message}";
                if (ex.InnerException != null) msg += $" — {ex.InnerException.Message}";
#endif
                ViewData["ErrorMessage"] = msg;
            }

            return View("Autenticar", "_Layout_Login");
        }

        public IActionResult Logout()
        {
            var id = HttpContext.Session.GetInt32("usuarioId");
            if (id.HasValue) _logger.LogInformation($"Usuario ID {id} cerró sesión");
            HttpContext.Session.Clear();
            return RedirectToAction("Autenticar", "Login");
        }
    }
}