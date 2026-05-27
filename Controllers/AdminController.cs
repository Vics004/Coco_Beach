using Coco_Beach.Models;
using Coco_Beach.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Coco_Beach.Controllers
{
    public class AdminController : Controller
    {
        private readonly Coco_BeachDbContext _context;


        public AdminController(Coco_BeachDbContext context)
        {
            _context = context;
        }
        // GET: usuario
        // GET: Admin
        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> UsuarioIndex(string search, string rol, bool? estado)
        {
            var query = _context.persona
                .Include(p => p.rol)
                .AsQueryable();

            var personasConUsuario = await query.ToListAsync();

            // Formatear teléfono
            foreach (var persona in personasConUsuario)
            {
                if (!string.IsNullOrEmpty(persona.telefono))
                    persona.telefono = persona.telefono.Replace("|", " ");
            }

            // Serializar solo lo necesario para el JS del cliente
            var paraJS = personasConUsuario.Select(u => new
            {
                u.personaid,
                u.nombre,
                u.apellido,
                u.correo,
                u.estado,
                u.rolid,
                telefono = u.telefono ?? "",
                rolNombre = u.rol?.nombre ?? ""
            }).ToList();

            ViewBag.Roles = await _context.rol.ToListAsync();
            ViewBag.UsuariosJson = System.Text.Json.JsonSerializer.Serialize(paraJS);

            return View(personasConUsuario);
        }



        [AuthorizeRole("Administrador")]
        // GET: Admin/Create
        public IActionResult UsuarioCreate()
        {
            ViewBag.RolSelect = new SelectList(_context.rol, "rolid", "nombre");
            return View();
        }

        [AuthorizeRole("Administrador")]
        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UsuarioCreate(
           [Bind("nombre,apellido,correo,rolid,estado,telefono")] persona persona,
           string password)
        {
            // 🔥 IMPORTANTE: Extraer código y número ANTES de la validación
            string codigoPais = "";
            string numeroTelefono = "";
            if (!string.IsNullOrWhiteSpace(persona.telefono) && persona.telefono.Contains("|"))
            {
                var parts = persona.telefono.Split('|');
                if (parts.Length == 2)
                {
                    codigoPais = parts[0];
                    numeroTelefono = parts[1];
                }
            }

            // Validación adicional del teléfono en el servidor
            if (!string.IsNullOrWhiteSpace(persona.telefono) && persona.telefono.Contains("|"))
            {
                var parts = persona.telefono.Split('|');
                if (parts.Length == 2)
                {
                    string codigo = parts[0];
                    string numero = parts[1];

                    // Definir los mismos requisitos que en el cliente
                    var digitosRequeridos = new Dictionary<string, int>
            {
                 {"+1", 10}, {"+52", 10},

                {"+501", 7}, {"+502", 8}, {"+503", 8}, {"+504", 8},
                {"+505", 8}, {"+506", 8}, {"+507", 8},

                {"+53", 8}, {"+509", 8}, {"+1809", 10},
                {"+1876", 10}, {"+1787", 10},

                {"+54", 10}, {"+55", 11}, {"+56", 9},
                {"+57", 10}, {"+58", 10}, {"+51", 9},

                {"+591", 8}, {"+593", 9}, {"+595", 9},
                {"+598", 8}, {"+592", 7}, {"+597", 7}
            };

                    if (digitosRequeridos.ContainsKey(codigo))
                    {
                        int digitosNecesarios = digitosRequeridos[codigo];
                        if (numero.Length != digitosNecesarios)
                        {
                            ModelState.AddModelError("telefono", $"El número debe tener exactamente {digitosNecesarios} dígitos para {codigo}");
                        }
                    }
                }
            }

            // Obtener el rol Cliente de la base de datos
            var rolCliente = await _context.rol.FirstOrDefaultAsync(r => r.nombre == "Cliente");

            // Determinar si el rol seleccionado es Cliente
            bool esRolCliente = (rolCliente != null && persona.rolid == rolCliente.rolid);

            // 🔥 IMPORTANTE: Limpiar el error de contraseña ANTES de validar ModelState
            // Si es cliente, removemos cualquier error relacionado con la contraseña
            if (esRolCliente)
            {
                ModelState.Remove("password"); // Elimina password del ModelState
                password = null; // Forzamos a null
            }

            // Validar que el rol existe en la DB
            var rolExiste = await _context.rol.AnyAsync(r => r.rolid == persona.rolid);
            if (!rolExiste)
            {
                ModelState.AddModelError("rolid", "El rol seleccionado no es válido.");
            }

            // Solo validar contraseña si NO es cliente
            if (!esRolCliente)
            {
                ModelState.Remove("password");

                if (string.IsNullOrWhiteSpace(password))
                {
                    ModelState.AddModelError("password", "La contraseña es obligatoria para este rol.");
                }
                else if (password.Length < 8)
                {
                    ModelState.AddModelError("password", "La contraseña debe tener al menos 8 caracteres.");
                }
            }
            // Validar correo único
            bool correoExiste = await _context.persona
                .AnyAsync(p => p.correo == persona.correo);

            if (correoExiste)
            {
                ModelState.AddModelError("correo", "Este correo ya está registrado.");
            }
            // Ahora sí verificamos si el modelo es válido
            if (ModelState.IsValid)
            {
                // Guardar Persona

                _context.persona.Add(persona);

                await _context.SaveChangesAsync();

                // Crear usuario solo si NO es cliente
                if (!esRolCliente)
                {
                    var passwordHasher = new PasswordHasher<object>();

                    var usuario = new usuario
                    {
                        personaid = persona.personaid,
                        password = passwordHasher.HashPassword(null, password)
                    };

                    _context.usuario.Add(usuario);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(UsuarioIndex));
            }

            // 🔥 IMPORTANTE: Guardar los valores de teléfono para restaurarlos en la vista
            ViewBag.CodigoPais = codigoPais;
            ViewBag.NumeroTelefono = numeroTelefono;
            ViewBag.Password = password; // Guardar contraseña para restaurarla (solo si no es cliente)
            ViewBag.RolSelect = new SelectList(_context.rol, "rolid", "nombre", persona.rolid);

            return View(persona);
        }

        [AuthorizeRole("Administrador")]
        // GET: Admin/Edit/5
        public async Task<IActionResult> UsuarioEdit(int? id)
        {
            if (id == null) return NotFound();

            var persona = await _context.persona.FindAsync(id);
            if (persona == null)
                return NotFound();

            ViewBag.RolSelect = new SelectList(_context.rol.ToList(), "rolid", "nombre", persona.rolid);
            return View(persona);
        }


        [AuthorizeRole("Administrador")]
        // POST: Admin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UsuarioEdit(int id,
    [Bind("personaid,nombre,apellido,correo,rolid,estado,telefono")] persona persona,
    string password)
        {
            if (id != persona.personaid) return NotFound();

            // Validación adicional del teléfono en el servidor
            if (!string.IsNullOrWhiteSpace(persona.telefono) && persona.telefono.Contains("|"))
            {
                var parts = persona.telefono.Split('|');
                if (parts.Length == 2)
                {
                    string codigo = parts[0];
                    string numero = parts[1];

                    // Definir los mismos requisitos que en el cliente
                    var digitosRequeridos = new Dictionary<string, int>
            {
                {"+1", 10}, {"+52", 10},

                {"+501", 7}, {"+502", 8}, {"+503", 8}, {"+504", 8},
                {"+505", 8}, {"+506", 8}, {"+507", 8},

                {"+53", 8}, {"+509", 8}, {"+1809", 10},
                {"+1876", 10}, {"+1787", 10},

                {"+54", 10}, {"+55", 11}, {"+56", 9},
                {"+57", 10}, {"+58", 10}, {"+51", 9},

                {"+591", 8}, {"+593", 9}, {"+595", 9},
                {"+598", 8}, {"+592", 7}, {"+597", 7}

            };

                    if (digitosRequeridos.ContainsKey(codigo))
                    {
                        int digitosNecesarios = digitosRequeridos[codigo];
                        if (numero.Length != digitosNecesarios)
                        {
                            ModelState.AddModelError("telefono", $"El número debe tener exactamente {digitosNecesarios} dígitos para {codigo}");
                        }
                    }
                }
            }

            // Obtener el rol Cliente
            var rolCliente = await _context.rol.FirstOrDefaultAsync(r => r.nombre == "Cliente");

            // Determinar si el rol seleccionado es Cliente
            bool esRolCliente = (rolCliente != null && persona.rolid == rolCliente.rolid);

            // Si es cliente, eliminamos la validación de password
            if (esRolCliente)
            {
                ModelState.Remove("password");
                password = null;
            }
            else
            {
                // Si no es cliente, validamos la contraseña SOLO si es requerida
                var usuarioExistente = await _context.usuario.FirstOrDefaultAsync(u => u.personaid == id);

                // Solo requerimos contraseña si NO existe un usuario previo
                if (string.IsNullOrEmpty(password))
                {
                    ModelState.Remove("password");

                }
                else if (usuarioExistente == null && string.IsNullOrWhiteSpace(password))
                {
                    ModelState.AddModelError("password", "La contraseña es obligatoria para este rol.");
                }
                else if (!string.IsNullOrWhiteSpace(password) && password.Length < 8)
                {
                    ModelState.AddModelError("password", "La contraseña debe tener al menos 8 caracteres.");
                }
            }
            // Validar correo único
            bool correoExiste = await _context.persona
                .AnyAsync(p => p.correo == persona.correo && p.personaid != id);

            if (correoExiste)
            {
                ModelState.AddModelError("correo", "Este correo ya está registrado.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Actualizar la persona
                    _context.Update(persona);
                    await _context.SaveChangesAsync();

                    // Manejar la tabla usuario según el rol
                    if (esRolCliente)
                    {
                        // Si es cliente, eliminamos su usuario si existe
                        var usuarioExistente = await _context.usuario.FirstOrDefaultAsync(u => u.personaid == id);
                        if (usuarioExistente != null)
                        {
                            _context.usuario.Remove(usuarioExistente);
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        // Si no es cliente
                        if (!string.IsNullOrWhiteSpace(password))
                        {
                            // Si proporcionó contraseña, actualizamos o creamos
                            var usuarioExistente = await _context.usuario.FirstOrDefaultAsync(u => u.personaid == id);
                            if (usuarioExistente != null)
                            {
                                var passwordHasher = new PasswordHasher<object>();

                                usuarioExistente.password =
                                    passwordHasher.HashPassword(null, password);

                                _context.usuario.Update(usuarioExistente);
                            }
                            else
                            {
                                var passwordHasher = new PasswordHasher<object>();

                                var nuevoUsuario = new usuario
                                {
                                    personaid = persona.personaid,
                                    password = passwordHasher.HashPassword(null, password)
                                };

                                _context.usuario.Add(nuevoUsuario);
                            }
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PersonaExists(id)) return NotFound();
                    throw;
                }

                return RedirectToAction(nameof(UsuarioIndex));
            }

            // Si falló, recargar el SelectList
            ViewBag.RolSelect = new SelectList(_context.rol.ToList(), "rolid", "nombre", persona.rolid);
            return View(persona);
        }

        // GET
        [AutenticationAttribute.Autenticacion]
        public async Task<IActionResult> MiPerfil(int id)
        {
            // Obtener ID del usuario logueado

            var persona = await _context.persona
                .Include(p => p.rol)
                .FirstOrDefaultAsync(p => p.personaid == id);

            if (persona == null)
                return NotFound();

            return View(persona);
        }

        // POST
        [AutenticationAttribute.Autenticacion]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MiPerfil(int id, string password)
        {


            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("password", "La contraseña es obligatoria.");
            }
            else if (password.Length < 8)
            {
                ModelState.AddModelError("password", "La contraseña debe tener al menos 8 caracteres.");
            }

            var persona = await _context.persona
                .FirstOrDefaultAsync(p => p.personaid == id);

            if (persona == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                var usuario = await _context.usuario
                    .FirstOrDefaultAsync(u => u.personaid == id);

                if (usuario != null)
                {
                    var passwordHasher = new PasswordHasher<object>();

                    usuario.password = passwordHasher.HashPassword(null, password);

                    _context.usuario.Update(usuario);

                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Contraseña actualizada correctamente.";
                }

                return RedirectToAction(nameof(Dashboard));
            }

            return View(persona);
        }

        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> UsuarioDelete(int? id)
        {
            if (id == null) return NotFound();

            var persona = await _context.persona
                .Include(p => p.rol)
                .FirstOrDefaultAsync(p => p.personaid == id);

            if (persona == null) return NotFound();

            // Opcional: evitar desactivar a alguien ya inactivo
            if (!persona.estado) return RedirectToAction(nameof(UsuarioIndex));

            return View(persona);
        }
        // POST: Admin/Delete/5
        [AuthorizeRole("Administrador")]
        [HttpPost]
        [ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var persona = await _context.persona.FindAsync(id);
            if (persona == null) return NotFound();

            persona.estado = false;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(UsuarioIndex));
        }

        [AutenticationAttribute.Autenticacion]
        private bool PersonaExists(int id)
        {
            return _context.persona.Any(e => e.personaid == id);
        }


        // ==============================================
        // GESTIÓN DE RECURSOS (Habitaciones)
        // ==============================================

        // GET: Admin/RecursoIndex
        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> RecursoIndex()
        {
            var recursos = await _context.recurso
                .OrderBy(r => r.recursoid)   // ← Orden ascendente por ID
                .ToListAsync();

            // Serializar solo lo necesario para el JS del cliente
            var paraJS = recursos.Select(r => new
            {
                r.recursoid,
                r.nombre,
                r.descripcion,
                r.capacidad,
                r.precio,
                libre = r.libre ?? false
            }).ToList();

            ViewBag.RecursosJson = System.Text.Json.JsonSerializer.Serialize(paraJS);

            return View(recursos);
        }

        // GET: Admin/RecursoCreate
        [AuthorizeRole("Administrador")]
        public IActionResult RecursoCreate()
        {
            return View();
        }

        // POST: Admin/RecursoCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> RecursoCreate([Bind("nombre,descripcion,capacidad,precio")] recurso recurso)
        {
            // Establecemos valores por defecto
            recurso.libre = true; // Por defecto, una habitación nueva está libre/disponible

            if (ModelState.IsValid)
            {
                _context.Add(recurso);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Habitación creada exitosamente.";
                return RedirectToAction(nameof(RecursoIndex));
            }
            return View(recurso);
        }

        // GET: Admin/RecursoEdit/5
        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> RecursoEdit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recurso = await _context.recurso.FindAsync(id);
            if (recurso == null)
            {
                return NotFound();
            }
            return View(recurso);
        }

        // POST: Admin/RecursoEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> RecursoEdit(int id, [Bind("recursoid,nombre,descripcion,capacidad,precio,libre")] recurso recurso)
        {
            if (id != recurso.recursoid)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(recurso);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Habitación actualizada correctamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecursoExists(recurso.recursoid))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(RecursoIndex));
            }
            return View(recurso);
        }

        // GET: Admin/RecursoDelete/5
        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> RecursoDelete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recurso = await _context.recurso
                .FirstOrDefaultAsync(m => m.recursoid == id);
            if (recurso == null)
            {
                return NotFound();
            }

            return View(recurso);
        }

        // POST: Admin/RecursoDelete/5 (ya no elimina, solo deshabilita)
        [HttpPost, ActionName("RecursoDelete")]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> RecursoDeleteConfirmed(int id)
        {
            var recurso = await _context.recurso.FindAsync(id);
            if (recurso != null)
            {
                // En lugar de eliminar, marcamos como No Habilitado
                recurso.libre = false;
                _context.Update(recurso);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "El recurso ha sido deshabilitado correctamente.";
            }
            return RedirectToAction(nameof(RecursoIndex));
        }

        // Método auxiliar para verificar existencia
        private bool RecursoExists(int id)
        {
            return _context.recurso.Any(e => e.recursoid == id);
        }


        // ==============================================
        // FINANZAS - REPORTES Y GRÁFICOS
        // ==============================================

        // GET: Admin/Finanzas
        [AuthorizeRole("Administrador", "Dueño")]
        public async Task<IActionResult> Finanzas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            // Establecer fechas por defecto (últimos 30 días)
            if (!fechaInicio.HasValue)
                fechaInicio = DateTime.Now.AddDays(-30);

            if (!fechaFin.HasValue)
                fechaFin = DateTime.Now;

            var datosFinanzas = await ObtenerDatosFinanzas(fechaInicio.Value, fechaFin.Value);
            return View(datosFinanzas);
        }

        // POST: Admin/Finanzas (para filtrar)
        [HttpPost]
        [AuthorizeRole("Administrador", "Dueño")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finanzas(DateTime fechaInicio, DateTime fechaFin)
        {
            if (fechaInicio > fechaFin)
            {
                TempData["ErrorMessage"] = "La fecha de inicio no puede ser mayor a la fecha de fin.";
                fechaFin = fechaInicio;
            }

            var datosFinanzas = await ObtenerDatosFinanzas(fechaInicio, fechaFin);
            return View(datosFinanzas);
        }

        // Método privado para obtener datos de finanzas (excluye reservas CANCELADAS)
        private async Task<dynamic> ObtenerDatosFinanzas(DateTime fechaInicio, DateTime fechaFin)
        {
            var fechaInicioUtc = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            var fechaFinUtc = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59, DateTimeKind.Utc);

            var todasLasHabitaciones = await _context.recurso.ToListAsync();

            var reservasEnRango = await _context.reserva
                .Where(r => r.fecha_fin >= fechaInicioUtc &&   // ← fecha_fin en lugar de fecha_inicio
                            r.fecha_fin <= fechaFinUtc &&        // ← fecha_fin en lugar de fecha_inicio
                            r.estadoid != 4)                     // ← excluir canceladas
                .ToListAsync();

            // Agrupar por habitación
            var reservasPorHabitacion = reservasEnRango
                .GroupBy(r => r.recursoid)
                .Select(g => new
                {
                    RecursoId = g.Key,
                    TotalReservas = g.Count(),
                    GananciasTotales = g.Sum(r => r.preciofinal ?? 0),
                    PromedioDiasEstancia = g.Average(r => (r.fecha_fin - r.fecha_inicio)?.TotalDays ?? 0)
                })
                .ToDictionary(k => k.RecursoId, v => v);

            // Combinar habitaciones con sus reservas
            var resultado = todasLasHabitaciones.Select(hab => new
            {
                hab.recursoid,
                hab.nombre,
                hab.capacidad,
                hab.precio,
                TotalReservas = reservasPorHabitacion.ContainsKey(hab.recursoid) ? reservasPorHabitacion[hab.recursoid].TotalReservas : 0,
                GananciasTotales = reservasPorHabitacion.ContainsKey(hab.recursoid) ? reservasPorHabitacion[hab.recursoid].GananciasTotales : 0,
                PromedioDiasEstancia = reservasPorHabitacion.ContainsKey(hab.recursoid) ? reservasPorHabitacion[hab.recursoid].PromedioDiasEstancia : 0
            })
            .Where(r => r.TotalReservas > 0)
            .OrderByDescending(r => r.GananciasTotales)
            .ToList();

            var totalGanancias = resultado.Sum(r => r.GananciasTotales);
            var totalReservas = resultado.Sum(r => r.TotalReservas);
            var totalHabitaciones = resultado.Count();

            var viewData = new
            {
                ResumenHabitaciones = resultado,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                TotalGanancias = totalGanancias,
                TotalReservas = totalReservas,
                TotalHabitacionesConReservas = totalHabitaciones
            };

            return viewData;
        }

        // ==============================================
        // EXPORTAR FINANZAS A PDF
        // ==============================================

        // GET: Admin/ExportarFinanzasPDF
        [AuthorizeRole("Administrador", "Dueño")]
        public async Task<IActionResult> ExportarFinanzasPDF(DateTime fechaInicio, DateTime fechaFin)
        {
            // Validar fechas
            if (fechaInicio == DateTime.MinValue)
                fechaInicio = DateTime.Now.AddDays(-30);
            if (fechaFin == DateTime.MinValue)
                fechaFin = DateTime.Now;

            if (fechaInicio > fechaFin)
            {
                var temp = fechaInicio;
                fechaInicio = fechaFin;
                fechaFin = temp;
            }

            // Obtener datos
            var datosFinanzas = await ObtenerDatosFinanzas(fechaInicio, fechaFin);

            // Extraer datos a objetos fuertemente tipados para evitar dynamic
            var resumen = new List<ResumenFinanzasPDF>();
            double totalGanancias = 0;
            int totalReservas = 0;
            int totalHabitaciones = 0;

            foreach (var item in datosFinanzas.ResumenHabitaciones)
            {
                var r = new ResumenFinanzasPDF
                {
                    RecursoId = item.recursoid,
                    Nombre = item.nombre,
                    Capacidad = item.capacidad,
                    Precio = item.precio,
                    TotalReservas = item.TotalReservas,
                    GananciasTotales = item.GananciasTotales,
                    PromedioDiasEstancia = item.PromedioDiasEstancia
                };
                resumen.Add(r);
            }

            totalGanancias = datosFinanzas.TotalGanancias;
            totalReservas = datosFinanzas.TotalReservas;
            totalHabitaciones = datosFinanzas.TotalHabitacionesConReservas;

            // Generar PDF
            var pdfBytes = CrearPDFFinanzas(resumen, totalGanancias, totalReservas, totalHabitaciones, fechaInicio, fechaFin);

            return File(pdfBytes, "application/pdf", $"Reporte_Finanzas_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}.pdf");
        }

        // Clase auxiliar para datos fuertemente tipados
        private class ResumenFinanzasPDF
        {
            public int RecursoId { get; set; }
            public string Nombre { get; set; }
            public int? Capacidad { get; set; }
            public double? Precio { get; set; }
            public int TotalReservas { get; set; }
            public double GananciasTotales { get; set; }
            public double PromedioDiasEstancia { get; set; }
        }

        private byte[] CrearPDFFinanzas(List<ResumenFinanzasPDF> resumen, double totalGanancias, int totalReservas, int totalHabitaciones, DateTime fechaInicio, DateTime fechaFin)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                    // Header: se aplica padding al contenedor de la columna
                    page.Header()
                        .ShowOnce()
                        .PaddingBottom(15)
                        .Column(col =>
                        {
                            col.Spacing(5);
                            col.Item().Text("Coco Beach - Reporte de Finanzas")
                                .FontSize(16).Bold().FontColor("F5A623");
                            col.Item().Text($"Período: {fechaInicio:dd/MM/yyyy} al {fechaFin:dd/MM/yyyy}")
                                .FontSize(12).Italic();
                            col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                                .FontSize(10).FontColor("666666");
                        });

                    // Content
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // KPIs
                        col.Item()
                            .PaddingBottom(15)
                            .Row(row =>
                            {
                                row.RelativeItem().Border(0.5f).BorderColor("CCCCCC").Padding(5).AlignCenter().Column(c =>
                                {
                                    c.Item().Text($"${totalGanancias:N2}").FontSize(14).Bold();
                                    c.Item().Text("Ganancias Totales").FontSize(10);
                                });
                                row.RelativeItem().Border(0.5f).BorderColor("CCCCCC").Padding(5).AlignCenter().Column(c =>
                                {
                                    c.Item().Text(totalReservas.ToString()).FontSize(14).Bold();
                                    c.Item().Text("Reservas Realizadas").FontSize(10);
                                });
                                row.RelativeItem().Border(0.5f).BorderColor("CCCCCC").Padding(5).AlignCenter().Column(c =>
                                {
                                    c.Item().Text(totalHabitaciones.ToString()).FontSize(14).Bold();
                                    c.Item().Text("Recursos con Reservas").FontSize(10);
                                });
                            });



                        // Título tabla detallada
                        col.Item().PaddingBottom(8)
                            .Text("Detalle por Recurso")
                            .FontSize(12).Bold().Underline();

                        // Tabla detallada
                        col.Item()
                            .PaddingBottom(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1f);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background("29B6D2").Padding(5).Text("ID").Bold().FontColor("FFFFFF");
                                    header.Cell().Background("29B6D2").Padding(5).Text("Recurso").Bold().FontColor("FFFFFF");
                                    header.Cell().Background("29B6D2").Padding(5).Text("Capacidad").Bold().FontColor("FFFFFF");
                                    header.Cell().Background("29B6D2").Padding(5).Text("Precio/Noche").Bold().FontColor("FFFFFF");
                                    header.Cell().Background("29B6D2").Padding(5).Text("Total Reservas").Bold().FontColor("FFFFFF");
                                    header.Cell().Background("29B6D2").Padding(5).Text("Promedio Días").Bold().FontColor("FFFFFF");
                                    header.Cell().Background("29B6D2").Padding(5).Text("Ganancias").Bold().FontColor("FFFFFF");
                                    header.Cell().Background("29B6D2").Padding(5).Text("% Contribución").Bold().FontColor("FFFFFF");
                                });

                                foreach (var item in resumen)
                                {
                                    double porcentaje = totalGanancias > 0 ? (item.GananciasTotales / totalGanancias * 100) : 0;
                                    table.Cell().Padding(3).Text(item.RecursoId.ToString());
                                    table.Cell().Padding(3).Text(item.Nombre);
                                    table.Cell().Padding(3).Text($"{item.Capacidad} personas");
                                    table.Cell().Padding(3).Text($"${item.Precio:N2}");
                                    table.Cell().Padding(3).Text(item.TotalReservas.ToString());
                                    table.Cell().Padding(3).Text($"{item.PromedioDiasEstancia:F1} días");
                                    table.Cell().Padding(3).Text($"${item.GananciasTotales:N2}");
                                    table.Cell().Padding(3).Text($"{porcentaje:F1}%");
                                }
                            });

                        if (!resumen.Any())
                        {
                            col.Item().Padding(10).AlignCenter()
                                .Text("No hay reservas en el período seleccionado.")
                                .FontColor("E05C6B");
                        }
                    });

                    // Footer
                    page.Footer().PaddingTop(10).AlignCenter().Text(text =>
                    {
                        text.Span("Coco Beach - Reporte generado automáticamente. ");
                        text.Span("Página ");
                        text.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();
        }




        // ==============================================
        // CALENDARIO — HOTEL (todos los recursos excepto Rancho)
        // ==============================================

        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Encargado")]
        public async Task<IActionResult> CalendarioHotel()
        {
            // ✅ Se pasan TODOS los recursos (incluidos los no habilitados),
            //    para que el JS los muestre bloqueados con estilo especial.
            var recursos = await _context.recurso
                .Where(r => r.recursoid != 15)
                .OrderBy(r => r.nombre)
                .ToListAsync();

            return View(recursos);
        }

        // ==============================================
        // CALENDARIO — RANCHO (solo recurso ID 15)
        // ==============================================

        [AuthorizeRole("Administrador", "Dueño", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> CalendarioRancho()
        {
            var recursos = await _context.recurso
                .Where(r => r.recursoid == 15)
                .OrderBy(r => r.nombre)
                .ToListAsync();

            return View("CalendarioHotel", recursos);
        }

        // ==============================================
        // AJAX — Obtener reservas en rango de fechas
        // ==============================================

        [HttpGet]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> GetReservas(DateTime fechaInicio, DateTime fechaFin, string tipo = "hotel")
        {
            var fechaInicioUtc = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            var fechaFinUtc = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59, DateTimeKind.Utc);

            IQueryable<reserva> query = _context.reserva
                .Where(r => r.fecha_inicio.HasValue && r.fecha_fin.HasValue)
                .Where(r => r.fecha_inicio.Value <= fechaFinUtc && r.fecha_fin.Value >= fechaInicioUtc)
                .Where(r => r.estadoid != 4);   // ← EXCLUIR CANCELADAS del calendario

            if (tipo == "rancho")
                query = query.Where(r => r.recursoid == 15);
            else
                query = query.Where(r => r.recursoid != 15);

            var reservas = await query.ToListAsync();

            // Obtener IDs únicos de clientes
            var clienteIds = reservas.Select(r => r.clienteid).Distinct().ToList();
            var clientes = await _context.persona
                .Where(p => clienteIds.Contains(p.personaid))
                .ToDictionaryAsync(p => p.personaid);

            // ✅ También incluir info de qué recursos están deshabilitados (libre = false)
            IQueryable<recurso> recursosQuery = _context.recurso;
            if (tipo == "rancho")
                recursosQuery = recursosQuery.Where(r => r.recursoid == 15);
            else
                recursosQuery = recursosQuery.Where(r => r.recursoid != 15);

            var recursosDeshabilitados = await recursosQuery
                .Where(r => r.libre == false)
                .Select(r => r.recursoid)
                .ToListAsync();

            var resultado = new
            {
                reservas = reservas.Select(r => new
                {
                    r.reservaid,
                    r.recursoid,
                    r.estadoid,
                    r.preciofinal,
                    r.comentario,
                    fecha_inicio = r.fecha_inicio,
                    fecha_fin = r.fecha_fin,
                    cliente = clientes.ContainsKey(r.clienteid) ? new
                    {
                        clientes[r.clienteid].personaid,
                        clientes[r.clienteid].nombre,
                        clientes[r.clienteid].apellido,
                        clientes[r.clienteid].correo,
                        clientes[r.clienteid].telefono
                    } : null
                }),
                recursosDeshabilitados
            };

            return Json(resultado);
        }

        // ==============================================
        // AJAX — Buscar clientes por nombre
        // ==============================================

        [HttpGet]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> BuscarClientes(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new List<object>());

            var termino = q.ToLower();
            var clientes = await _context.persona
                .Where(p => (p.nombre + " " + p.apellido).ToLower().Contains(termino)
                         || (p.correo != null && p.correo.ToLower().Contains(termino)))
                .Take(8)
                .Select(p => new { p.personaid, p.nombre, p.apellido, p.correo, p.telefono })
                .ToListAsync();

            return Json(clientes);
        }

        // ==============================================
        // AJAX — Crear cliente rápido desde el modal
        // ==============================================

        [HttpPost]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> CrearClienteRapido([FromBody] PersonaCreateDto dto)
        {
            // ── Campos obligatorios ──────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(dto.nombre))
                return BadRequest(new { error = "El nombre es requerido." });

            if (string.IsNullOrWhiteSpace(dto.apellido))
                return BadRequest(new { error = "El apellido es requerido." });

            if (string.IsNullOrWhiteSpace(dto.correo))
                return BadRequest(new { error = "El correo es requerido." });

            if (string.IsNullOrWhiteSpace(dto.telefono))
                return BadRequest(new { error = "El teléfono es requerido." });

            // ── Solo letras en nombre y apellido ────────────────────────────────
            var soloLetras = new System.Text.RegularExpressions.Regex(
                @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$");

            if (!soloLetras.IsMatch(dto.nombre.Trim()) || dto.nombre.Trim().Length < 2)
                return BadRequest(new { error = "El nombre solo puede contener letras y debe tener al menos 2 caracteres." });

            if (!soloLetras.IsMatch(dto.apellido.Trim()) || dto.apellido.Trim().Length < 2)
                return BadRequest(new { error = "El apellido solo puede contener letras y debe tener al menos 2 caracteres." });

            // ── Formato de correo ────────────────────────────────────────────────
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailRegex.IsMatch(dto.correo))
                return BadRequest(new { error = "El formato del correo no es válido." });

            // ── Correo único ─────────────────────────────────────────────────────
            bool correoExiste = await _context.persona.AnyAsync(p => p.correo == dto.correo);
            if (correoExiste)
                return BadRequest(new { error = "Este correo ya está registrado." });

            // ── Teléfono: formato obligatorio +prefijo|numero ────────────────────
            if (!dto.telefono.Contains("|"))
                return BadRequest(new { error = "El teléfono debe tener el formato +prefijo|número." });

            var partesTel = dto.telefono.Split('|');
            if (partesTel.Length != 2
                || string.IsNullOrWhiteSpace(partesTel[0])
                || string.IsNullOrWhiteSpace(partesTel[1]))
                return BadRequest(new { error = "El teléfono debe tener el formato +prefijo|número." });

            string codigoPais = partesTel[0];
            string numeroTelefono = partesTel[1];

            if (!numeroTelefono.All(char.IsDigit))
                return BadRequest(new { error = "El número de teléfono solo debe contener dígitos." });

            var digitosRequeridos = new Dictionary<string, int>
    {
        {"+1", 10}, {"+52", 10},
        {"+501", 7}, {"+502", 8}, {"+503", 8}, {"+504", 8},
        {"+505", 8}, {"+506", 8}, {"+507", 8},
        {"+53", 8}, {"+509", 8}, {"+1809", 10},
        {"+1876", 10}, {"+1787", 10},
        {"+54", 10}, {"+55", 11}, {"+56", 9},
        {"+57", 10}, {"+58", 10}, {"+51", 9},
        {"+591", 8}, {"+593", 9}, {"+595", 9},
        {"+598", 8}, {"+592", 7}, {"+597", 7}
    };

            if (!digitosRequeridos.ContainsKey(codigoPais))
                return BadRequest(new { error = $"Código de país '{codigoPais}' no reconocido." });

            int digitosNecesarios = digitosRequeridos[codigoPais];
            if (numeroTelefono.Length != digitosNecesarios)
                return BadRequest(new { error = $"El número debe tener exactamente {digitosNecesarios} dígitos para {codigoPais}." });

            // ── Crear persona ─────────────────────────────────────────────────────
            var rolCliente = await _context.rol.FirstOrDefaultAsync(r => r.nombre.ToLower().Contains("cliente"));

            var nuevaPersona = new persona
            {
                nombre = dto.nombre.Trim(),
                apellido = dto.apellido.Trim(),
                correo = dto.correo.Trim(),
                telefono = dto.telefono,   // ya viene como +prefijo|numero desde el JS
                rolid = rolCliente?.rolid,
                estado = true
            };

            _context.persona.Add(nuevaPersona);
            await _context.SaveChangesAsync();

            return Json(new
            {
                nuevaPersona.personaid,
                nuevaPersona.nombre,
                nuevaPersona.apellido,
                nuevaPersona.correo,
                nuevaPersona.telefono
            });
        }

        // DTO para CrearClienteRapido
        public class PersonaCreateDto
        {
            public string nombre { get; set; } = "";
            public string apellido { get; set; } = "";
            public string correo { get; set; } = "";
            public string telefono { get; set; } = "";
        }

        // ==============================================
        // AJAX — Crear reserva desde el calendario
        // ==============================================

        [HttpPost]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> CrearReservaCalendario([FromBody] ReservaCreateDto dto)
        {
            if (dto.recursoid <= 0 || dto.clienteid <= 0)
                return BadRequest(new { error = "Habitación y cliente son requeridos." });

            // ✅ Verificar que el recurso está habilitado
            var recurso = await _context.recurso.FindAsync(dto.recursoid);
            if (recurso == null)
                return NotFound(new { error = "Habitación no encontrada." });
            if (recurso.libre == false)
                return BadRequest(new { error = "Esta habitación no está habilitada y no puede recibir reservas." });

            var inicioUtc = DateTime.SpecifyKind(dto.fecha_inicio, DateTimeKind.Utc);
            var finUtc = DateTime.SpecifyKind(dto.fecha_fin, DateTimeKind.Utc);

            if (inicioUtc >= finUtc)
                return BadRequest(new { error = "La fecha de inicio debe ser anterior a la fecha de fin." });

            // Validar traslape (excluir canceladas y disponibles)
            var traslape = await _context.reserva
                .AnyAsync(r => r.recursoid == dto.recursoid
                            && r.estadoid != 3
                            && r.estadoid != 4   // ← excluir canceladas
                            && r.fecha_inicio.HasValue && r.fecha_fin.HasValue
                            && r.fecha_inicio.Value < finUtc
                            && r.fecha_fin.Value > inicioUtc);

            if (traslape)
                return Conflict(new { error = "Ya existe una reserva en ese rango de fechas para esta habitación." });

            int empleadoId = HttpContext.Session.GetInt32("personaId") ?? 0;
            if (empleadoId == 0)
                return Unauthorized(new { error = "Sesión no válida. Por favor inicia sesión nuevamente." });

            var nuevaReserva = new reserva
            {
                clienteid = dto.clienteid,
                empleadoid = empleadoId,
                recursoid = dto.recursoid,
                estadoid = dto.estadoid > 0 ? dto.estadoid : 1,
                fecha_inicio = inicioUtc,
                fecha_fin = finUtc,
                fecha_creacion = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-6), DateTimeKind.Utc),
                preciofinal = dto.preciofinal,
                comentario = string.IsNullOrWhiteSpace(dto.comentario) ? null : dto.comentario.Trim()
            };

            _context.reserva.Add(nuevaReserva);
            await _context.SaveChangesAsync();

            return Json(new { success = true, reservaid = nuevaReserva.reservaid });
        }

        // DTO para CrearReservaCalendario
        public class ReservaCreateDto
        {
            public int clienteid { get; set; }
            public int recursoid { get; set; }
            public int estadoid { get; set; }
            public DateTime fecha_inicio { get; set; }
            public DateTime fecha_fin { get; set; }
            public double? preciofinal { get; set; }
            public string? comentario { get; set; }
        }

        [HttpGet]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> ListaReservas(string tipo = "hotel")
        {
            var query = _context.reserva.AsQueryable();

            if (tipo == "rancho")
                query = query.Where(r => r.recursoid == 15);
            else
                query = query.Where(r => r.recursoid != 15);

            var reservas = await query
                .OrderByDescending(r => r.fecha_creacion)
                .ToListAsync();

            var clienteIds = reservas.Select(r => r.clienteid).Distinct().ToList();
            var empleadoIds = reservas.Where(r => r.empleadoid > 0).Select(r => r.empleadoid).Distinct().ToList();
            var recursoIds = reservas.Select(r => r.recursoid).Distinct().ToList();
            var estadoIds = reservas.Select(r => r.estadoid).Distinct().ToList();

            var clientes = await _context.persona.Where(p => clienteIds.Contains(p.personaid)).ToDictionaryAsync(p => p.personaid);
            var empleados = await _context.persona.Where(p => empleadoIds.Contains(p.personaid)).ToDictionaryAsync(p => p.personaid);
            var recursos = await _context.recurso.Where(r => recursoIds.Contains(r.recursoid)).ToDictionaryAsync(r => r.recursoid);
            var estados = await _context.estado.Where(e => estadoIds.Contains(e.estadoid)).ToDictionaryAsync(e => e.estadoid);

            var filas = reservas.Select(r => new
            {
                r.reservaid,
                r.comentario,
                cliente = clientes.ContainsKey(r.clienteid) ? $"{clientes[r.clienteid].nombre} {clientes[r.clienteid].apellido}".Trim() : $"ID {r.clienteid}",
                empleado = r.empleadoid > 0 && empleados.ContainsKey(r.empleadoid)
            ? $"{empleados[r.empleadoid].nombre} {empleados[r.empleadoid].apellido}".Trim()
            : "—",
                habitacion = recursos.ContainsKey(r.recursoid) ? recursos[r.recursoid].nombre : $"ID {r.recursoid}",
                estado = r.estadoid == 3
                    ? "Finalizado"
                    : (estados.ContainsKey(r.estadoid) ? estados[r.estadoid].nombre : $"ID {r.estadoid}"),
                r.estadoid,
                fecha_inicio = r.fecha_inicio.HasValue ? r.fecha_inicio.Value.ToString("dd/MM/yyyy HH:mm") : "—",
                fecha_fin = r.fecha_fin.HasValue ? r.fecha_fin.Value.ToString("dd/MM/yyyy HH:mm") : "—",
                fecha_creacion = r.fecha_creacion.HasValue ? r.fecha_creacion.Value.ToString("dd/MM/yyyy HH:mm") : "—",
                preciofinal = r.preciofinal.HasValue ? r.preciofinal.Value : (double?)null,
                preciofinalTexto = r.preciofinal.HasValue ? $"${r.preciofinal.Value:N2}" : "—"
            }).ToList();

            ViewBag.Tipo = tipo;
            ViewBag.FilasJson = System.Text.Json.JsonSerializer.Serialize(filas);
            return View();
        }

        // ==============================================
        // EXPORTAR .ICS — Feed de reservas de una habitación
        // ==============================================

        [HttpGet]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> ExportarReserva(int reservaid)
        {
            var reserva = await _context.reserva.FindAsync(reservaid);
            if (reserva == null) return NotFound();

            var recurso = await _context.recurso.FindAsync(reserva.recursoid);
            var cliente = await _context.persona.FindAsync(reserva.clienteid);
            var nombreCliente = cliente != null ? $"{cliente.nombre} {cliente.apellido}".Trim() : "Huésped";
            var nombreRecurso = recurso?.nombre ?? "Habitación";

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//CocoBeach//Calendario//ES");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");
            sb.AppendLine($"X-WR-CALNAME:CocoBeach - Reserva #{reservaid}");
            sb.AppendLine("X-WR-TIMEZONE:America/El_Salvador");

            var dtStart = reserva.fecha_inicio!.Value.ToString("yyyyMMdd");
            var dtEnd = reserva.fecha_fin!.Value.AddDays(1).ToString("yyyyMMdd");
            var uid = $"reserva-{reserva.reservaid}@cocobeach";
            var created = (reserva.fecha_creacion ?? DateTime.UtcNow).ToString("yyyyMMdd'T'HHmmss'Z'");

            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{uid}");
            sb.AppendLine($"DTSTAMP:{created}");
            sb.AppendLine($"DTSTART;VALUE=DATE:{dtStart}");
            sb.AppendLine($"DTEND;VALUE=DATE:{dtEnd}");
            sb.AppendLine($"SUMMARY:{EscapeICS(nombreCliente)} - {EscapeICS(nombreRecurso)}");
            sb.AppendLine($"DESCRIPTION:Reserva #{reserva.reservaid}. Habitación: {EscapeICS(nombreRecurso)}. Precio: ${reserva.preciofinal:N2}");
            sb.AppendLine($"LOCATION:{EscapeICS(nombreRecurso)}");
            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return File(bytes, "text/calendar; charset=utf-8", $"{nombreRecurso.Replace(" ", "_")}_Reserva{reservaid}_{ts}.ics");
        }

        [HttpGet]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> ExportarICS(int recursoid, DateTime? desde = null, DateTime? hasta = null)
        {
            {

                var desdeUtc = desde.HasValue ? DateTime.SpecifyKind(desde.Value, DateTimeKind.Utc) : DateTime.MinValue;
                var hastaUtc = hasta.HasValue ? DateTime.SpecifyKind(hasta.Value.AddDays(1), DateTimeKind.Utc) : DateTime.MaxValue;

                var recurso = await _context.recurso.FindAsync(recursoid);
                if (recurso == null) return NotFound();

                var reservas = await _context.reserva
                    .Where(r => r.recursoid == recursoid
                             && r.estadoid != 4
                             && r.fecha_inicio.HasValue
                             && r.fecha_fin.HasValue
                             && r.fecha_inicio.Value >= desdeUtc
                             && r.fecha_inicio.Value <= hastaUtc)
                    .ToListAsync();

                var clienteIds = reservas.Select(r => r.clienteid).Distinct().ToList();
                var clientes = await _context.persona
                    .Where(p => clienteIds.Contains(p.personaid))
                    .ToDictionaryAsync(p => p.personaid);

                var sb = new StringBuilder();
                sb.AppendLine("BEGIN:VCALENDAR");
                sb.AppendLine("VERSION:2.0");
                sb.AppendLine("PRODID:-//CocoBeach//Calendario//ES");
                sb.AppendLine("CALSCALE:GREGORIAN");
                sb.AppendLine("METHOD:PUBLISH");
                sb.AppendLine($"X-WR-CALNAME:CocoBeach - {recurso.nombre}");
                sb.AppendLine("X-WR-TIMEZONE:America/El_Salvador");

                foreach (var r in reservas)
                {
                    var cliente = clientes.ContainsKey(r.clienteid) ? clientes[r.clienteid] : null;
                    var nombreCliente = cliente != null ? $"{cliente.nombre} {cliente.apellido}".Trim() : "Huésped";

                    var dtStart = r.fecha_inicio!.Value.ToString("yyyyMMdd");
                    var dtEnd = r.fecha_fin!.Value.AddDays(1).ToString("yyyyMMdd");
                    var uid = $"reserva-{r.reservaid}@cocobeach";
                    var created = (r.fecha_creacion ?? DateTime.UtcNow).ToString("yyyyMMdd'T'HHmmss'Z'");

                    sb.AppendLine("BEGIN:VEVENT");
                    sb.AppendLine($"UID:{uid}");
                    sb.AppendLine($"DTSTAMP:{created}");
                    sb.AppendLine($"DTSTART;VALUE=DATE:{dtStart}");
                    sb.AppendLine($"DTEND;VALUE=DATE:{dtEnd}");
                    sb.AppendLine($"SUMMARY:{EscapeICS(nombreCliente)} - {EscapeICS(recurso.nombre ?? "")}");
                    sb.AppendLine($"DESCRIPTION:Reserva #{r.reservaid}. Estado: {r.estadoid}. Precio: ${r.preciofinal:N2}");
                    sb.AppendLine($"LOCATION:{EscapeICS(recurso.nombre ?? "CocoBeach")}");
                    sb.AppendLine("END:VEVENT");
                }

                sb.AppendLine("END:VCALENDAR");

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                return File(bytes, "text/calendar; charset=utf-8", $"{recurso.nombre?.Replace(" ", "_")}_{ts}.ics");
            }
        }

        private static string EscapeICS(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace("\n", "\\n")
                .Replace("\r", "");
        }


        [HttpGet]
        [Route("Admin/ExportarICSCompleto")]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> ExportarICSCompleto(string tipo = "hotel", DateTime? desde = null, DateTime? hasta = null)
        {

            var desdeUtc = desde.HasValue ? DateTime.SpecifyKind(desde.Value, DateTimeKind.Utc) : DateTime.MinValue;
            var hastaUtc = hasta.HasValue ? DateTime.SpecifyKind(hasta.Value.AddDays(1), DateTimeKind.Utc) : DateTime.MaxValue;

            IQueryable<reserva> query = _context.reserva
                .Where(r => r.estadoid != 4
                            && r.fecha_inicio.HasValue
                            && r.fecha_fin.HasValue
                            && r.fecha_inicio.Value >= desdeUtc
                            && r.fecha_inicio.Value <= hastaUtc);

            if (tipo == "rancho")
                query = query.Where(r => r.recursoid == 15);
            else
                query = query.Where(r => r.recursoid != 15);

            var reservas = await query.ToListAsync();

            var clienteIds = reservas.Select(r => r.clienteid).Distinct().ToList();
            var clientes = await _context.persona
                .Where(p => clienteIds.Contains(p.personaid))
                .ToDictionaryAsync(p => p.personaid);

            var recursoIds = reservas.Select(r => r.recursoid).Distinct().ToList();
            var recursos = await _context.recurso
                .Where(r => recursoIds.Contains(r.recursoid))
                .ToDictionaryAsync(r => r.recursoid);

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//CocoBeach//Calendario//ES");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");
            sb.AppendLine($"X-WR-CALNAME:CocoBeach - {(tipo == "rancho" ? "Rancho" : "Hotel")}");
            sb.AppendLine("X-WR-TIMEZONE:America/El_Salvador");

            foreach (var r in reservas)
            {
                var cliente = clientes.ContainsKey(r.clienteid) ? clientes[r.clienteid] : null;
                var nombreCliente = cliente != null ? $"{cliente.nombre} {cliente.apellido}".Trim() : "Huésped";
                var recurso = recursos.ContainsKey(r.recursoid) ? recursos[r.recursoid] : null;
                var nombreRecurso = recurso?.nombre ?? "Habitación";

                var dtStart = r.fecha_inicio!.Value.ToString("yyyyMMdd");
                var dtEnd = r.fecha_fin!.Value.AddDays(1).ToString("yyyyMMdd");
                var uid = $"reserva-{r.reservaid}@cocobeach";
                var created = (r.fecha_creacion ?? DateTime.UtcNow).ToString("yyyyMMdd'T'HHmmss'Z'");

                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:{uid}");
                sb.AppendLine($"DTSTAMP:{created}");
                sb.AppendLine($"DTSTART;VALUE=DATE:{dtStart}");
                sb.AppendLine($"DTEND;VALUE=DATE:{dtEnd}");
                sb.AppendLine($"SUMMARY:{EscapeICS(nombreCliente)} - {EscapeICS(nombreRecurso)}");
                sb.AppendLine($"DESCRIPTION:Reserva #{r.reservaid}. Habitación: {EscapeICS(nombreRecurso)}. Precio: ${r.preciofinal:N2}");
                sb.AppendLine($"LOCATION:{EscapeICS(nombreRecurso)}");
                sb.AppendLine("END:VEVENT");
            }

            sb.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var nombreArchivo = tipo == "rancho" ? $"CocoBeach-Rancho_{ts}.ics" : $"CocoBeach-Hotel_{ts}.ics";
            return File(bytes, "text/calendar; charset=utf-8", nombreArchivo);
        }

        // ==============================================
        // AJAX — Cambiar estado de una reserva
        // ==============================================

        [HttpPost]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> CambiarEstadoReserva([FromBody] CambiarEstadoDto dto)
        {
            var reserva = await _context.reserva.FindAsync(dto.reservaid);
            if (reserva == null)
                return NotFound(new { error = "Reserva no encontrada." });

            // ✅ Ahora se permiten estados 1, 2, 3 (no cancelar desde aquí, usar CancelarReserva)
            var estadosPermitidos = new[] { 1, 2, 3 };
            if (!estadosPermitidos.Contains(dto.estadoid))
                return BadRequest(new { error = "Estado no válido." });

            reserva.estadoid = dto.estadoid;
            await _context.SaveChangesAsync();

            return Json(new { success = true, reservaid = reserva.reservaid, estadoid = reserva.estadoid });
        }

        // DTO para CambiarEstadoReserva
        public class CambiarEstadoDto
        {
            public int reservaid { get; set; }
            public int estadoid { get; set; }
        }

        // ==============================================
        // ✅ NUEVO — AJAX: Cancelar reserva con % reembolso
        // ==============================================

        [HttpPost]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> CancelarReserva([FromBody] CancelarReservaDto dto)
        {
            var reserva = await _context.reserva.FindAsync(dto.reservaid);
            if (reserva == null)
                return NotFound(new { error = "Reserva no encontrada." });

            if (reserva.estadoid == 4)
                return BadRequest(new { error = "Esta reserva ya está cancelada." });

            // Validar porcentaje
            if (dto.porcentajeReembolso < 0 || dto.porcentajeReembolso > 100)
                return BadRequest(new { error = "El porcentaje de reembolso debe estar entre 0 y 100." });

            double montoOriginal = reserva.preciofinal ?? 0;
            double montoReembolso = Math.Round(montoOriginal * dto.porcentajeReembolso / 100.0, 2);

            double nuevoPrecioFinal = montoOriginal - montoReembolso;
            reserva.preciofinal = nuevoPrecioFinal;

            // Cambiar estado a Cancelado (ID=4)
            reserva.estadoid = 4;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                reservaid = reserva.reservaid,
                montoOriginal,
                porcentajeReembolso = dto.porcentajeReembolso,
                montoReembolso,
                nuevoPrecioFinal,
                mensaje = $"Reserva #{reserva.reservaid} cancelada. Reembolso: ${montoReembolso:N2} ({dto.porcentajeReembolso}%)"
            });
        }

        // DTO para CancelarReserva
        public class CancelarReservaDto
        {
            public int reservaid { get; set; }
            public double porcentajeReembolso { get; set; } = 100;
        }

        // ==============================================
        // AJAX — Editar reserva desde el calendario
        // ==============================================

        [HttpPost]
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Gerente de Rancho", "Encargado")]
        public async Task<IActionResult> EditarReservaCalendario([FromBody] ReservaEditDto dto)
        {
            var reserva = await _context.reserva.FindAsync(dto.reservaid);
            if (reserva == null)
                return NotFound(new { error = "Reserva no encontrada." });

            if (reserva.estadoid == 4)
                return BadRequest(new { error = "No se puede editar una reserva cancelada." });

            if (dto.recursoid <= 0 || dto.clienteid <= 0)
                return BadRequest(new { error = "Habitación y cliente son requeridos." });

            // ✅ Verificar que el recurso destino está habilitado
            var recurso = await _context.recurso.FindAsync(dto.recursoid);
            if (recurso?.libre == false)
                return BadRequest(new { error = "La habitación seleccionada no está habilitada." });

            var inicioUtc = DateTime.SpecifyKind(dto.fecha_inicio, DateTimeKind.Utc);
            var finUtc = DateTime.SpecifyKind(dto.fecha_fin, DateTimeKind.Utc);

            if (inicioUtc >= finUtc)
                return BadRequest(new { error = "La fecha de inicio debe ser anterior a la fecha de fin." });

            // Validar traslape excluyendo la reserva que se está editando y las canceladas
            var traslape = await _context.reserva
                .AnyAsync(r => r.reservaid != dto.reservaid
                            && r.recursoid == dto.recursoid
                            && r.estadoid != 3
                            && r.estadoid != 4   // ← excluir canceladas
                            && r.fecha_inicio.HasValue && r.fecha_fin.HasValue
                            && r.fecha_inicio.Value < finUtc
                            && r.fecha_fin.Value > inicioUtc);

            if (traslape)
                return Conflict(new { error = "Ya existe otra reserva en ese rango de fechas para esta habitación." });

            reserva.clienteid = dto.clienteid;
            reserva.recursoid = dto.recursoid;
            reserva.estadoid = dto.estadoid > 0 ? dto.estadoid : reserva.estadoid;
            reserva.fecha_inicio = inicioUtc;
            reserva.fecha_fin = finUtc;
            reserva.preciofinal = dto.preciofinal;
            reserva.comentario = string.IsNullOrWhiteSpace(dto.comentario) ? null : dto.comentario.Trim();

            await _context.SaveChangesAsync();

            return Json(new { success = true, reservaid = reserva.reservaid });
        }

        // DTO para EditarReservaCalendario
        public class ReservaEditDto
        {
            public int reservaid { get; set; }
            public int clienteid { get; set; }
            public int recursoid { get; set; }
            public int estadoid { get; set; }
            public DateTime fecha_inicio { get; set; }
            public DateTime fecha_fin { get; set; }
            public double? preciofinal { get; set; }
            public string? comentario { get; set; }
        }


        // ==============================================
        // DASHBOARD - INDICADORES Y DISPONIBILIDAD
        // ==============================================

        // GET: Admin/Dashboard
        [AuthorizeRole("Administrador", "Dueño", "Gerente de Hotel", "Encargado")]
        public async Task<IActionResult> Dashboard(int? mes, int? anio)
        {
            var hoyUtc = DateTime.UtcNow.Date;
            int mesFiltro = mes ?? hoyUtc.Month;
            int anioFiltro = anio ?? hoyUtc.Year;

            // ✅ Rango completo del mes seleccionado: del 1 al último día
            var inicioMesUtc = new DateTime(anioFiltro, mesFiltro, 1, 0, 0, 0, DateTimeKind.Utc);
            var finMesUtc = new DateTime(anioFiltro, mesFiltro,
                                   DateTime.DaysInMonth(anioFiltro, mesFiltro),
                                   23, 59, 59, DateTimeKind.Utc);

            // Rango del día actual (UTC) — se mantiene igual para el estado de habitaciones
            var inicioHoyUtc = hoyUtc;
            var finHoyUtc = inicioHoyUtc.AddDays(1).AddTicks(-1);

            ViewBag.FechaInicioDashboard = inicioMesUtc;
            ViewBag.FechaFinDashboard = finMesUtc;


            // Obtener IDs de estados
            var estados = await _context.estado.ToListAsync();
            int idDisponible = estados.FirstOrDefault(e => e.nombre == "Disponible")?.estadoid ?? 0;
            int idReservada = estados.FirstOrDefault(e => e.nombre == "Reservado")?.estadoid ?? 0;
            int idEnProceso = estados.FirstOrDefault(e => e.nombre == "En proceso de reserva")?.estadoid ?? 0;


            ViewBag.IdReservada = idReservada;
            ViewBag.IdEnProceso = idEnProceso;
            ViewBag.MesFiltro = mesFiltro;
            ViewBag.AnioFiltro = anioFiltro;

            // Lista de meses para el filtro
            var culturaEspanol = new System.Globalization.CultureInfo("es-SV");
            var meses = Enumerable.Range(1, 12).Select(m => new
            {
                Value = m,
                Text = culturaEspanol.DateTimeFormat.GetMonthName(m)
            });
            ViewBag.MesesList = new SelectList(meses, "Value", "Text", mesFiltro);

            // ===== KPIs del MES =====
            var reservasMesQuery = _context.reserva
                .Where(r => r.fecha_fin.HasValue &&
                            r.fecha_fin.Value >= inicioMesUtc &&
                            r.fecha_fin.Value <= finMesUtc &&
                            r.estadoid != 4);

            ViewBag.TotalReservasMes = await reservasMesQuery.CountAsync();
            ViewBag.TotalGananciasMes = await reservasMesQuery.SumAsync(r => r.preciofinal ?? 0);

            // ===== Ranking de habitaciones =====
            var rankingQuery = from res in _context.reserva
                               join rec in _context.recurso on res.recursoid equals rec.recursoid
                               where res.fecha_fin.HasValue &&
                                     res.fecha_fin.Value >= inicioMesUtc &&
                                     res.fecha_fin.Value <= finMesUtc &&
                                     res.estadoid != 4
                               group res by new { rec.nombre } into grupo
                               select new
                               {
                                   Nombre = grupo.Key.nombre,
                                   Reservas = grupo.Count(),
                                   Ganancias = grupo.Sum(x => x.preciofinal ?? 0)
                               };

            ViewBag.RankingHabitaciones = await rankingQuery
                .OrderByDescending(x => x.Ganancias)
                .ToListAsync();

            // ===== Estado actual de cada habitación (hoy) =====
            // Determinamos el estado según la reserva activa de hoy (si existe)
            var estadoHabitaciones = await (from rec in _context.recurso
                                            join res in _context.reserva.Where(r =>
                                                r.fecha_inicio.HasValue && r.fecha_fin.HasValue &&
                                                r.fecha_inicio.Value <= finHoyUtc &&
                                                r.fecha_fin.Value >= inicioHoyUtc)
                                            on rec.recursoid equals res.recursoid into joinReserva
                                            from subRes in joinReserva.DefaultIfEmpty()
                                            group subRes by new { rec.recursoid, rec.nombre } into grupo
                                            select new
                                            {
                                                Nombre = grupo.Key.nombre,
                                                EstadoIdActual = grupo.Any(x => x != null)
                                                                 ? grupo.Where(x => x != null).Min(x => x.estadoid)
                                                                 : idDisponible
                                            })
                                            .OrderBy(x => x.Nombre)
                                            .ToListAsync();

            ViewBag.ListaHabitaciones = estadoHabitaciones;

            return View();
        }


        // ==============================================
        // AUDITORÍA - LOOKUPS PARA RESOLUCIÓN DE IDs
        // ==============================================

        private async Task<(
            Dictionary<int, string> personas,
            Dictionary<int, string> usuarios,
            Dictionary<int, string> recursos,
            Dictionary<int, string> estados,
            Dictionary<int, string> roles
        )> CargarLookupsAuditoriaAsync()
        {
            var personas = await _context.persona
                .ToDictionaryAsync(p => p.personaid, p => $"{p.nombre} {p.apellido}");

            // Usuarios resueltos al nombre de su persona vinculada
            var usuarios = await _context.usuario
                .ToDictionaryAsync(
                    u => u.usuarioid,
                    u => personas.TryGetValue(u.personaid, out var n) ? n : $"ID:{u.usuarioid}"
                );

            var recursos = await _context.recurso
                .ToDictionaryAsync(r => r.recursoid, r => r.nombre ?? $"ID:{r.recursoid}");

            var estados = await _context.estado
                .ToDictionaryAsync(e => e.estadoid, e => e.nombre ?? $"ID:{e.estadoid}");

            var roles = await _context.rol
                .ToDictionaryAsync(r => r.rolid, r => r.nombre ?? $"ID:{r.rolid}");

            return (personas, usuarios, recursos, estados, roles);
        }

        /// <summary>
        /// Dado un JSON de auditoría, sustituye los IDs de FK por su nombre legible.
        /// </summary>
        private static Dictionary<string, string> ResolverIdsEnJson(
            string? json,
            Dictionary<int, string> personas,
            Dictionary<int, string> usuarios,
            Dictionary<int, string> recursos,
            Dictionary<int, string> estados,
            Dictionary<int, string> roles)
        {
            var resultado = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return resultado;

            // Mapa: nombre del campo JSON → diccionario de lookup correspondiente
            var lookups = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["clienteid"] = personas,
                ["empleadoid"] = usuarios,
                ["usuarioid"] = usuarios,
                ["personaid"] = personas,
                ["recursoid"] = recursos,
                ["estadoid"] = estados,
                ["rolid"] = roles,
            };

            try
            {
                var obj = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);

                if (obj == null) return resultado;

                foreach (var kvp in obj)
                {
                    string rawValor = kvp.Value.ToString() ?? "null";

                    if (lookups.TryGetValue(kvp.Key, out var lookup) &&
                        int.TryParse(rawValor, out int id) &&
                        lookup.TryGetValue(id, out var nombre))
                    {
                        resultado[kvp.Key] = $"{nombre} (ID:{id})";
                    }
                    else
                    {
                        resultado[kvp.Key] = rawValor;
                    }
                }
            }
            catch
            {
                resultado["_raw"] = json.Length > 80 ? json[..77] + "…" : json;
            }

            return resultado;
        }

        /// <summary>
        /// Formatea el diccionario resuelto en una cadena legible, con truncado.
        /// </summary>
        private static string FormatearDiccionario(Dictionary<string, string> resuelto, int maxLen = 200)
        {
            if (resuelto.Count == 0) return "—";

            var partes = resuelto.Select(kvp =>
            {
                var v = kvp.Value.Length > 100 ? kvp.Value[..97] + "..." : kvp.Value;
                return $"{kvp.Key}: {v}";
            });

            var resultado = string.Join(" | ", partes);
            return resultado.Length > maxLen ? resultado[..(maxLen - 3)] + "..." : resultado;
        }


        // ==============================================
        // AUDITORÍA - LISTADO Y FILTRADO
        // ==============================================

        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> AuditoriaIndex(
            string? tabla,
            string? accion,
            int? usuarioId,
            DateTime? fechaInicio,
            DateTime? fechaFin,
            string? search)
        {
            var query = _context.auditoria.AsQueryable();

            if (!string.IsNullOrEmpty(tabla))
                query = query.Where(a => a.tabla_afectada == tabla);
            if (!string.IsNullOrEmpty(accion))
                query = query.Where(a => a.accion == accion);
            if (usuarioId.HasValue && usuarioId.Value > 0)
                query = query.Where(a => a.usuarioid == usuarioId.Value);

            // Corrección para fechas UTC
            if (fechaInicio.HasValue)
            {
                var inicioUtc = DateTime.SpecifyKind(fechaInicio.Value.Date, DateTimeKind.Utc);
                query = query.Where(a => a.fecha_accion >= inicioUtc);
            }
            if (fechaFin.HasValue)
            {
                var finUtc = DateTime.SpecifyKind(fechaFin.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(a => a.fecha_accion <= finUtc);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.tabla_afectada.Contains(search) ||
                    a.accion.Contains(search) ||
                    (a.valor_anterior != null && a.valor_anterior.Contains(search)) ||
                    (a.valor_nuevo != null && a.valor_nuevo.Contains(search))
                );
            }

            var auditorias = await query
                .OrderByDescending(a => a.fecha_accion)
                .ToListAsync();

            // JOIN para mostrar el nombre del usuario en la columna "Usuario"
            var userIds = auditorias.Select(a => a.usuarioid).Distinct().ToList();
            var usuariosInfo = await (
                from u in _context.usuario
                join p in _context.persona on u.personaid equals p.personaid
                where userIds.Contains(u.usuarioid)
                select new { u.usuarioid, NombreCompleto = p.nombre + " " + p.apellido }
            ).ToDictionaryAsync(k => k.usuarioid, v => v.NombreCompleto);

            ViewBag.NombresUsuarios = usuariosInfo;
            ViewBag.Tablas = await _context.auditoria.Select(a => a.tabla_afectada).Distinct().OrderBy(t => t).ToListAsync();
            ViewBag.Acciones = await _context.auditoria.Select(a => a.accion).Distinct().OrderBy(a => a).ToListAsync();
            ViewBag.Usuarios = await (
                from u in _context.usuario
                join p in _context.persona on u.personaid equals p.personaid
                orderby p.nombre
                select new { u.usuarioid, NombreCompleto = p.nombre + " " + p.apellido }
            ).ToListAsync();

            ViewBag.TablaSeleccionada = tabla;
            ViewBag.AccionSeleccionada = accion;
            ViewBag.UsuarioIdSeleccionado = usuarioId;
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.SearchText = search;

            // Lookups para resolver IDs dentro de los valores JSON (valor_anterior / valor_nuevo)
            var (lkPersonas, lkUsuarios, lkRecursos, lkEstados, lkRoles) =
                await CargarLookupsAuditoriaAsync();

            ViewBag.LkPersonas = lkPersonas;
            ViewBag.LkUsuarios = lkUsuarios;
            ViewBag.LkRecursos = lkRecursos;
            ViewBag.LkEstados = lkEstados;
            ViewBag.LkRoles = lkRoles;

            return View(auditorias);
        }


        // ==============================================
        // AUDITORÍA - EXPORTAR PDF
        // ==============================================

        [AuthorizeRole("Administrador")]
        public async Task<IActionResult> ExportarAuditoriaPDF(
            string? tabla,
            string? accion,
            int? usuarioId,
            DateTime? fechaInicio,
            DateTime? fechaFin,
            string? search)
        {
            var query = _context.auditoria.AsQueryable();

            if (!string.IsNullOrEmpty(tabla))
                query = query.Where(a => a.tabla_afectada == tabla);
            if (!string.IsNullOrEmpty(accion))
                query = query.Where(a => a.accion == accion);
            if (usuarioId.HasValue && usuarioId.Value > 0)
                query = query.Where(a => a.usuarioid == usuarioId.Value);

            if (fechaInicio.HasValue)
            {
                var inicioUtc = DateTime.SpecifyKind(fechaInicio.Value.Date, DateTimeKind.Utc);
                query = query.Where(a => a.fecha_accion >= inicioUtc);
            }
            if (fechaFin.HasValue)
            {
                var finUtc = DateTime.SpecifyKind(fechaFin.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(a => a.fecha_accion <= finUtc);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.tabla_afectada.Contains(search) ||
                    a.accion.Contains(search) ||
                    (a.valor_anterior != null && a.valor_anterior.Contains(search)) ||
                    (a.valor_nuevo != null && a.valor_nuevo.Contains(search))
                );
            }

            var auditorias = await query
                .OrderByDescending(a => a.fecha_accion)
                .ToListAsync();

            var userIds = auditorias.Select(a => a.usuarioid).Distinct().ToList();
            var usuariosInfo = await (
                from u in _context.usuario
                join p in _context.persona on u.personaid equals p.personaid
                where userIds.Contains(u.usuarioid)
                select new { u.usuarioid, NombreCompleto = p.nombre + " " + p.apellido }
            ).ToDictionaryAsync(k => k.usuarioid, v => v.NombreCompleto);

            // Cargar lookups para resolver IDs en el PDF
            var (lkPersonas, lkUsuarios, lkRecursos, lkEstados, lkRoles) =
                await CargarLookupsAuditoriaAsync();

            var pdfBytes = CrearPDFAuditoria(
                auditorias, usuariosInfo,
                lkPersonas, lkUsuarios, lkRecursos, lkEstados, lkRoles,
                tabla, accion, usuarioId, fechaInicio, fechaFin, search);

            return File(pdfBytes, "application/pdf",
                $"Reporte_Auditoria_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }

        private byte[] CrearPDFAuditoria(
            List<auditoria> auditorias,
            Dictionary<int, string> nombresUsuarios,
            Dictionary<int, string> lkPersonas,
            Dictionary<int, string> lkUsuarios,
            Dictionary<int, string> lkRecursos,
            Dictionary<int, string> lkEstados,
            Dictionary<int, string> lkRoles,
            string? tablaFiltro,
            string? accionFiltro,
            int? usuarioIdFiltro,
            DateTime? fechaInicio,
            DateTime? fechaFin,
            string? search)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                    page.Header()
                        .ShowOnce()
                        .PaddingBottom(10)
                        .Column(col =>
                        {
                            col.Spacing(5);
                            col.Item().Text("Coco Beach - Reporte de Auditoría")
                                .FontSize(16).Bold().FontColor("F5A623");
                            col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                                .FontSize(10).FontColor("666666");

                            var filtrosTexto = new List<string>();
                            if (!string.IsNullOrEmpty(tablaFiltro)) filtrosTexto.Add($"Tabla: {tablaFiltro}");
                            if (!string.IsNullOrEmpty(accionFiltro)) filtrosTexto.Add($"Acción: {accionFiltro}");
                            if (usuarioIdFiltro.HasValue) filtrosTexto.Add($"Usuario ID: {usuarioIdFiltro}");
                            if (fechaInicio.HasValue) filtrosTexto.Add($"Desde: {fechaInicio.Value:dd/MM/yyyy}");
                            if (fechaFin.HasValue) filtrosTexto.Add($"Hasta: {fechaFin.Value:dd/MM/yyyy}");
                            if (!string.IsNullOrEmpty(search)) filtrosTexto.Add($"Búsqueda: {search}");

                            if (filtrosTexto.Any())
                            {
                                col.Item().Text($"Filtros: {string.Join(" | ", filtrosTexto)}")
                                    .FontSize(9).Italic().FontColor("555555");
                            }
                        });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(0.8f); // ID
                            columns.RelativeColumn(1f);   // Tabla
                            columns.RelativeColumn(1f);   // Registro ID
                            columns.RelativeColumn(1f);   // Acción
                            columns.RelativeColumn(3.5f); // Valor Anterior
                            columns.RelativeColumn(3.5f); // Valor Nuevo
                            columns.RelativeColumn(2f);   // Usuario
                            columns.RelativeColumn(2f);   // Fecha
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("29B6D2").Padding(4).Text("ID").Bold().FontColor("FFFFFF");
                            header.Cell().Background("29B6D2").Padding(4).Text("Tabla").Bold().FontColor("FFFFFF");
                            header.Cell().Background("29B6D2").Padding(4).Text("Registro ID").Bold().FontColor("FFFFFF");
                            header.Cell().Background("29B6D2").Padding(4).Text("Acción").Bold().FontColor("FFFFFF");
                            header.Cell().Background("29B6D2").Padding(4).Text("Valor Anterior").Bold().FontColor("FFFFFF");
                            header.Cell().Background("29B6D2").Padding(4).Text("Valor Nuevo").Bold().FontColor("FFFFFF");
                            header.Cell().Background("29B6D2").Padding(4).Text("Usuario").Bold().FontColor("FFFFFF");
                            header.Cell().Background("29B6D2").Padding(4).Text("Fecha Acción").Bold().FontColor("FFFFFF");
                        });

                        foreach (var a in auditorias)
                        {
                            string nombreUsuario = nombresUsuarios.TryGetValue(a.usuarioid, out var n)
                                ? n : $"ID:{a.usuarioid}";

                            var resAnterior = ResolverIdsEnJson(
                                a.valor_anterior, lkPersonas, lkUsuarios, lkRecursos, lkEstados, lkRoles);
                            var resNuevo = ResolverIdsEnJson(
                                a.valor_nuevo, lkPersonas, lkUsuarios, lkRecursos, lkEstados, lkRoles);

                            // Sin truncado — texto completo para el PDF
                            string FormatearPDF(Dictionary<string, string> d)
                            {
                                if (d.Count == 0) return "—";
                                return string.Join("\n", d.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                            }

                            table.Cell().Padding(3).Text(a.auditoriaid.ToString());
                            table.Cell().Padding(3).Text(a.tabla_afectada ?? "");
                            table.Cell().Padding(3).Text(a.registroid.ToString());
                            table.Cell().Padding(3).Text(a.accion ?? "");
                            table.Cell().Padding(3).Text(FormatearPDF(resAnterior));
                            table.Cell().Padding(3).Text(FormatearPDF(resNuevo));
                            table.Cell().Padding(3).Text(nombreUsuario);
                            table.Cell().Padding(3).Text(a.fecha_accion?.ToString("dd/MM/yyyy HH:mm:ss") ?? "");
                        }
                    });

                    page.Footer().PaddingTop(10).AlignCenter().Text(text =>
                    {
                        text.Span("Coco Beach - Reporte de auditoría generado automáticamente. ");
                        text.Span("Página ");
                        text.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();
        }

        // ==============================================
        // RESPALDO DE BASE DE DATOS CON pg_dump
        // ==============================================

        [AuthorizeRole("Administrador")]
        [HttpGet]
        public async Task<IActionResult> DescargarRespaldo()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"respaldo_cocobeach_{timestamp}.sql";
                var sb = new System.Text.StringBuilder();

                // ── Encabezado ───────────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine($"-- Respaldo Coco Beach generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine("-- =====================================================");
                sb.AppendLine();
                sb.AppendLine("DROP SCHEMA public CASCADE;");
                sb.AppendLine("CREATE SCHEMA public;");
                sb.AppendLine();

                // ── Deshabilitar FK para evitar errores de orden ──────────────
                sb.AppendLine("-- Deshabilitar validación de FK durante la restauración");
                sb.AppendLine("SET session_replication_role = 'replica';");
                sb.AppendLine();

                // ── Creación de tablas ────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- CREACIÓN DE TABLAS");
                sb.AppendLine("-- =====================================================");
                sb.AppendLine();

                sb.AppendLine("CREATE TABLE rol (");
                sb.AppendLine("    rolid   SERIAL PRIMARY KEY,");
                sb.AppendLine("    nombre  VARCHAR(50) NOT NULL");
                sb.AppendLine(");");
                sb.AppendLine();

                sb.AppendLine("CREATE TABLE persona (");
                sb.AppendLine("    personaid   SERIAL PRIMARY KEY,");
                sb.AppendLine("    nombre      VARCHAR(50) NOT NULL,");
                sb.AppendLine("    apellido    VARCHAR(50) NOT NULL,");
                sb.AppendLine("    correo      VARCHAR(50) UNIQUE NOT NULL,");
                sb.AppendLine("    rolid       INTEGER REFERENCES rol(rolid),");
                sb.AppendLine("    estado      BOOLEAN,");
                sb.AppendLine("    telefono    VARCHAR(50)");
                sb.AppendLine(");");
                sb.AppendLine();

                sb.AppendLine("CREATE TABLE usuario (");
                sb.AppendLine("    usuarioid   SERIAL PRIMARY KEY,");
                sb.AppendLine("    password    VARCHAR(255) NOT NULL,");
                sb.AppendLine("    personaid   INTEGER UNIQUE NOT NULL REFERENCES persona(personaid)");
                sb.AppendLine(");");
                sb.AppendLine();

                sb.AppendLine("CREATE TABLE estado (");
                sb.AppendLine("    estadoid  SERIAL PRIMARY KEY,");
                sb.AppendLine("    nombre    VARCHAR(50) NOT NULL UNIQUE");
                sb.AppendLine(");");
                sb.AppendLine();

                sb.AppendLine("CREATE TABLE recurso (");
                sb.AppendLine("    recursoid    SERIAL PRIMARY KEY,");
                sb.AppendLine("    nombre       VARCHAR(50) NOT NULL,");
                sb.AppendLine("    libre        BOOLEAN DEFAULT true,");
                sb.AppendLine("    descripcion  VARCHAR(50),");
                sb.AppendLine("    capacidad    INTEGER,");
                sb.AppendLine("    precio       DOUBLE PRECISION");
                sb.AppendLine(");");
                sb.AppendLine();

                sb.AppendLine("CREATE TABLE reserva (");
                sb.AppendLine("    reservaid       SERIAL PRIMARY KEY,");
                sb.AppendLine("    clienteid       INTEGER NOT NULL REFERENCES persona(personaid),");
                sb.AppendLine("    empleadoid      INTEGER NOT NULL REFERENCES usuario(usuarioid),");
                sb.AppendLine("    recursoid       INTEGER NOT NULL REFERENCES recurso(recursoid),");
                sb.AppendLine("    estadoid        INTEGER NOT NULL REFERENCES estado(estadoid),");
                sb.AppendLine("    fecha_inicio    TIMESTAMP NOT NULL,");
                sb.AppendLine("    fecha_fin       TIMESTAMP NOT NULL,");
                sb.AppendLine("    fecha_creacion  TIMESTAMP DEFAULT CURRENT_TIMESTAMP,");
                sb.AppendLine("    preciofinal     DOUBLE PRECISION,");
                sb.AppendLine("    comentario      VARCHAR(255),");
                sb.AppendLine("    CONSTRAINT check_fechas CHECK (fecha_fin > fecha_inicio)");
                sb.AppendLine(");");
                sb.AppendLine();

                sb.AppendLine("CREATE TABLE auditoria (");
                sb.AppendLine("    auditoriaid      SERIAL PRIMARY KEY,");
                sb.AppendLine("    tabla_afectada   VARCHAR(100) NOT NULL,");
                sb.AppendLine("    registroid       INTEGER NOT NULL,");
                sb.AppendLine("    accion           VARCHAR(20) NOT NULL,");
                sb.AppendLine("    valor_anterior   JSONB,");
                sb.AppendLine("    valor_nuevo      JSONB,");
                sb.AppendLine("    usuarioid        INTEGER NOT NULL REFERENCES usuario(usuarioid),");
                sb.AppendLine("    fecha_accion     TIMESTAMP DEFAULT CURRENT_TIMESTAMP");
                sb.AppendLine(");");
                sb.AppendLine();

                // ── Datos: rol ───────────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- DATOS: rol");
                sb.AppendLine("-- =====================================================");
                var roles = await _context.rol.ToListAsync();
                foreach (var r in roles)
                    sb.AppendLine($"INSERT INTO rol (rolid, nombre) VALUES ({r.rolid}, {Sql(r.nombre)});");
                sb.AppendLine();

                // ── Datos: persona ───────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- DATOS: persona");
                sb.AppendLine("-- =====================================================");
                var personas = await _context.persona.ToListAsync();
                foreach (var p in personas)
                    sb.AppendLine($"INSERT INTO persona (personaid, nombre, apellido, correo, telefono, rolid, estado) " +
                        $"VALUES ({p.personaid}, {Sql(p.nombre)}, {Sql(p.apellido)}, {Sql(p.correo)}, " +
                        $"{Sql(p.telefono)}, {SqlInt(p.rolid)}, {p.estado.ToString().ToLower()});");
                sb.AppendLine();

                // ── Datos: usuario ───────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- DATOS: usuario");
                sb.AppendLine("-- =====================================================");
                var usuarios = await _context.usuario.ToListAsync();
                foreach (var u in usuarios)
                    sb.AppendLine($"INSERT INTO usuario (usuarioid, password, personaid) " +
                        $"VALUES ({u.usuarioid}, {Sql(u.password)}, {u.personaid});");
                sb.AppendLine();

                // ── Datos: estado ────────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- DATOS: estado");
                sb.AppendLine("-- =====================================================");
                var estados = await _context.estado.ToListAsync();
                foreach (var e in estados)
                    sb.AppendLine($"INSERT INTO estado (estadoid, nombre) VALUES ({e.estadoid}, {Sql(e.nombre)});");
                sb.AppendLine();

                // ── Datos: recurso ───────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- DATOS: recurso");
                sb.AppendLine("-- =====================================================");
                var recursos = await _context.recurso.ToListAsync();
                foreach (var r in recursos)
                    sb.AppendLine($"INSERT INTO recurso (recursoid, nombre, libre, descripcion, capacidad, precio) " +
                        $"VALUES ({r.recursoid}, {Sql(r.nombre)}, {SqlBool(r.libre)}, " +
                        $"{Sql(r.descripcion)}, {SqlInt(r.capacidad)}, {SqlDouble(r.precio)});");
                sb.AppendLine();

                // ── Datos: reserva ───────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- DATOS: reserva");
                sb.AppendLine("-- =====================================================");
                var reservas = await _context.reserva.ToListAsync();
                foreach (var r in reservas)
                    sb.AppendLine($"INSERT INTO reserva (reservaid, clienteid, empleadoid, recursoid, estadoid, fecha_inicio, fecha_fin, fecha_creacion, preciofinal, comentario) " +
                        $"VALUES ({r.reservaid}, {r.clienteid}, {SqlInt(r.empleadoid)}, {r.recursoid}, {r.estadoid}, " +
                        $"{SqlDate(r.fecha_inicio)}, {SqlDate(r.fecha_fin)}, {SqlDate(r.fecha_creacion)}, {SqlDouble(r.preciofinal)}, {Sql(r.comentario)});");
                sb.AppendLine();

                // ── Datos: auditoria ─────────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- DATOS: auditoria");
                sb.AppendLine("-- =====================================================");
                var auditorias = await _context.auditoria.ToListAsync();
                foreach (var a in auditorias)
                    sb.AppendLine($"INSERT INTO auditoria (auditoriaid, tabla_afectada, registroid, accion, valor_anterior, valor_nuevo, usuarioid, fecha_accion) " +
                        $"VALUES ({a.auditoriaid}, {Sql(a.tabla_afectada)}, {a.registroid}, {Sql(a.accion)}, " +
                        $"{Sql(a.valor_anterior)}, {Sql(a.valor_nuevo)}, {a.usuarioid}, {SqlDate(a.fecha_accion)});");
                sb.AppendLine();

                // ── Reactivar FK ─────────────────────────────────────────────
                sb.AppendLine("-- Reactivar validación de FK");
                sb.AppendLine("SET session_replication_role = 'origin';");
                sb.AppendLine();

                // ── Ajuste de secuencias ──────────────────────────────────────
                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- AJUSTE DE SECUENCIAS");
                sb.AppendLine("-- =====================================================");
                if (roles.Any())
                    sb.AppendLine($"SELECT setval('rol_rolid_seq', {roles.Max(r => r.rolid)}, true);");
                if (personas.Any())
                    sb.AppendLine($"SELECT setval('persona_personaid_seq', {personas.Max(p => p.personaid)}, true);");
                if (usuarios.Any())
                    sb.AppendLine($"SELECT setval('usuario_usuarioid_seq', {usuarios.Max(u => u.usuarioid)}, true);");
                if (estados.Any())
                    sb.AppendLine($"SELECT setval('estado_estadoid_seq', {estados.Max(e => e.estadoid)}, true);");
                if (recursos.Any())
                    sb.AppendLine($"SELECT setval('recurso_recursoid_seq', {recursos.Max(r => r.recursoid)}, true);");
                if (reservas.Any())
                    sb.AppendLine($"SELECT setval('reserva_reservaid_seq', {reservas.Max(r => r.reservaid)}, true);");
                if (auditorias.Any())
                    sb.AppendLine($"SELECT setval('auditoria_auditoriaid_seq', {auditorias.Max(a => a.auditoriaid)}, true);");
                sb.AppendLine();

                sb.AppendLine("-- =====================================================");
                sb.AppendLine("-- FIN DEL RESPALDO");
                sb.AppendLine("-- =====================================================");

                var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al generar el respaldo: {ex.Message}");
            }
        }

        // ── Helpers para escapar valores SQL ────────────────────────────────────
        private static string Sql(string? val) =>
            val == null ? "NULL" : "'" + val.Replace("'", "''") + "'";

        private static string SqlInt(int? val) =>
            val == null ? "NULL" : val.ToString()!;

        private static string SqlDouble(double? val) =>
            val == null ? "NULL" : val.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string SqlBool(bool? val) =>
            val == null ? "NULL" : val.Value ? "true" : "false";

        private static string SqlDate(DateTime? val) =>
            val == null ? "NULL" : $"'{val.Value:yyyy-MM-dd HH:mm:ss}'";
    }
}