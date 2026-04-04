using Coco_Beach.Models;
using Coco_Beach.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using System.Text;
using System.Text.Json;

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
        [AutenticationAttribute.Autenticacion]
        public async Task<IActionResult> UsuarioIndex()
        {
            var personasConUsuario = await _context.persona
                .Include(p => p.rol)
                .Where(p => _context.usuario.Any(u => u.personaid == p.personaid))
                .ToListAsync();

            return View(personasConUsuario);
        }

        [AutenticationAttribute.Autenticacion]
        // GET: Admin/Create
        public IActionResult UsuarioCreate()
        {
            ViewBag.RolSelect = new SelectList(_context.rol, "rolid", "nombre");
            return View();
        }

        [AutenticationAttribute.Autenticacion]
        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UsuarioCreate(
            [Bind("nombre,apellido,correo,rolid,estado,telefono")] persona persona,
            string password)
        {
            if (ModelState.IsValid)
            {
                _context.persona.Add(persona);
                await _context.SaveChangesAsync();

                var usuario = new usuario
                {
                    personaid = persona.personaid,
                    password = password  // ← TODO: Hashear 
                };

                _context.usuario.Add(usuario);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(UsuarioIndex));
            }

            ViewBag.RolSelect = new SelectList(_context.rol, "rolid", "nombre", persona.rolid);
            return View(persona);
        }

        [AutenticationAttribute.Autenticacion]
        // GET: Admin/Edit/5
        public async Task<IActionResult> UsuarioEdit(int? id)
        {
            if (id == null) return NotFound();

            var persona = await _context.persona.FindAsync(id);
            if (persona == null || !_context.usuario.Any(u => u.personaid == id))
                return NotFound();

            ViewBag.RolSelect = new SelectList(_context.rol.ToList(), "rolid", "nombre", persona.rolid);
            return View(persona);
        }


        [AutenticationAttribute.Autenticacion]
        // POST: Admin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UsuarioEdit(int id,
            [Bind("personaid,nombre,apellido,correo,rolid,estado,telefono")] persona persona)
        {
            if (id != persona.personaid) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(persona);


                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PersonaExists(id)) return NotFound();
                    throw;
                }

                return RedirectToAction(nameof(UsuarioIndex));
            }

            ViewBag.RolSelect = new SelectList(_context.rol.ToList(), "rolid", "nombre", persona.rolid);
            return View(persona);
        }

        [AutenticationAttribute.Autenticacion]
        // GET: Admin/Delete/5
        public async Task<IActionResult> UsuarioDelete(int? id)
        {
            if (id == null) return NotFound();

            var persona = await _context.persona
                .Include(p => p.rol)
                .FirstOrDefaultAsync(p => p.personaid == id);

            if (persona == null) return NotFound();

            return View(persona);
        }

        [AutenticationAttribute.Autenticacion]
        // POST: Admin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var usuario = await _context.usuario.FirstOrDefaultAsync(u => u.personaid == id);
            if (usuario != null)
            {
                _context.usuario.Remove(usuario);
            }

            var persona = await _context.persona.FindAsync(id);
            if (persona != null)
            {
                _context.persona.Remove(persona);
            }

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
        [AutenticationAttribute.Autenticacion]
        public async Task<IActionResult> RecursoIndex()
        {
            var recursos = await _context.recurso.ToListAsync();
            return View(recursos);
        }

        // GET: Admin/RecursoCreate
        [AutenticationAttribute.Autenticacion]
        public IActionResult RecursoCreate()
        {
            return View();
        }

        // POST: Admin/RecursoCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        [AutenticationAttribute.Autenticacion]
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
        [AutenticationAttribute.Autenticacion]
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

        // POST: Admin/RecursoDelete/5
        [HttpPost, ActionName("RecursoDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecursoDeleteConfirmed(int id)
        {
            var recurso = await _context.recurso.FindAsync(id);
            if (recurso != null)
            {
                _context.recurso.Remove(recurso);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Habitación eliminada correctamente.";
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
        [AutenticationAttribute.Autenticacion]
        public async Task<IActionResult> Finanzas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            // Establecer fechas por defecto (últimos 30 días)
            if (!fechaInicio.HasValue)
                fechaInicio = DateTime.Now.AddDays(-30);

            if (!fechaFin.HasValue)
                fechaFin = DateTime.Now;

            // Obtener datos de finanzas usando consultas LINQ
            var datosFinanzas = await ObtenerDatosFinanzas(fechaInicio.Value, fechaFin.Value);

            return View(datosFinanzas);
        }

        // POST: Admin/Finanzas (para filtrar)
        [HttpPost]
        [AutenticationAttribute.Autenticacion]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finanzas(DateTime fechaInicio, DateTime fechaFin)
        {
            // Validar que las fechas sean correctas
            if (fechaInicio > fechaFin)
            {
                TempData["ErrorMessage"] = "La fecha de inicio no puede ser mayor a la fecha de fin.";
                fechaFin = fechaInicio;
            }

            var datosFinanzas = await ObtenerDatosFinanzas(fechaInicio, fechaFin);
            return View(datosFinanzas);
        }

        // Método privado para obtener datos de finanzas
        private async Task<dynamic> ObtenerDatosFinanzas(DateTime fechaInicio, DateTime fechaFin)
        {
            // CONVERTIR FECHAS A UTC PARA POSTGRESQL
            // Especificar que las fechas son UTC
            var fechaInicioUtc = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            var fechaFinUtc = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59, DateTimeKind.Utc);

            // Obtener todas las habitaciones con sus reservas en el rango de fechas
            var todasLasHabitaciones = await _context.recurso.ToListAsync();

            // Obtener reservas en el rango de fechas (excluyendo estado "Disponible" que es el 3)
            var reservasEnRango = await _context.reserva
                .Where(r => r.fecha_inicio.HasValue &&
                            r.fecha_inicio.Value >= fechaInicioUtc &&
                            r.fecha_inicio.Value <= fechaFinUtc &&
                            r.estadoid != 3) // Excluir reservas con estado "Disponible"
                .ToListAsync();

            // Agrupar reservas por recursoid y calcular estadísticas
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

            // Construir el resultado combinando habitaciones con sus reservas
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
            .Where(r => r.TotalReservas > 0) // Solo mostrar habitaciones con reservas
            .OrderByDescending(r => r.GananciasTotales)
            .ToList();

            // Calcular totales generales
            var totalGanancias = resultado.Sum(r => r.GananciasTotales);
            var totalReservas = resultado.Sum(r => r.TotalReservas);
            var totalHabitaciones = resultado.Count();

            // Preparar datos para la vista (convertir fechas de vuelta a Local para mostrar)
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
        // CALENDARIO — HOTEL (todos los recursos excepto Rancho)
        // ==============================================

        [AutenticationAttribute.Autenticacion]
        public async Task<IActionResult> CalendarioHotel()
        {
            var recursos = await _context.recurso
                .Where(r => r.recursoid != 15)
                .OrderBy(r => r.nombre)
                .ToListAsync();

            return View(recursos);
        }

        // ==============================================
        // CALENDARIO — RANCHO (solo recurso ID 15)
        // ==============================================

        [AutenticationAttribute.Autenticacion]
        public async Task<IActionResult> CalendarioRancho()
        {
            var recursos = await _context.recurso
                .Where(r => r.recursoid == 15)
                .OrderBy(r => r.nombre)
                .ToListAsync();

            return View("CalendarioHotel", recursos); // Reutiliza la misma vista
        }

        // ==============================================
        // AJAX — Obtener reservas en rango de fechas
        // ==============================================

        [HttpGet]
        public async Task<IActionResult> GetReservas(DateTime fechaInicio, DateTime fechaFin, string tipo = "hotel")
        {
            var fechaInicioUtc = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            var fechaFinUtc = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59, DateTimeKind.Utc);

            IQueryable<reserva> query = _context.reserva
                .Where(r => r.fecha_inicio.HasValue && r.fecha_fin.HasValue)
                .Where(r => r.fecha_inicio.Value <= fechaFinUtc && r.fecha_fin.Value >= fechaInicioUtc);

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

            var resultado = reservas.Select(r => new
            {
                r.reservaid,
                r.recursoid,
                r.estadoid,
                r.preciofinal,
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
            });

            return Json(resultado);
        }

        // ==============================================
        // AJAX — Buscar clientes por nombre
        // ==============================================

        [HttpGet]
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
        public async Task<IActionResult> CrearClienteRapido([FromBody] PersonaCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.nombre))
                return BadRequest(new { error = "El nombre es requerido." });

            // Buscar rol "Cliente" — ajusta el rolid según tu BD
            var rolCliente = await _context.rol.FirstOrDefaultAsync(r => r.nombre.ToLower().Contains("cliente"));

            var nuevaPersona = new persona
            {
                nombre = dto.nombre,
                apellido = dto.apellido,
                correo = dto.correo,
                telefono = dto.telefono,
                rolid = rolCliente?.rolid,
                estado = "Activo"
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
            public string? apellido { get; set; }
            public string? correo { get; set; }
            public string? telefono { get; set; }
        }

        // ==============================================
        // AJAX — Crear reserva desde el calendario
        // ==============================================

        [HttpPost]
        public async Task<IActionResult> CrearReservaCalendario([FromBody] ReservaCreateDto dto)
        {
            // Validar que los campos requeridos estén presentes
            if (dto.recursoid <= 0 || dto.clienteid <= 0)
                return BadRequest(new { error = "Habitación y cliente son requeridos." });

            var inicioUtc = DateTime.SpecifyKind(dto.fecha_inicio, DateTimeKind.Utc);
            var finUtc = DateTime.SpecifyKind(dto.fecha_fin, DateTimeKind.Utc);

            if (inicioUtc >= finUtc)
                return BadRequest(new { error = "La fecha de inicio debe ser anterior a la fecha de fin." });

            // Validar traslape
            var traslape = await _context.reserva
                .AnyAsync(r => r.recursoid == dto.recursoid
                            && r.estadoid != 3 // excluir canceladas/disponible
                            && r.fecha_inicio.HasValue && r.fecha_fin.HasValue
                            && r.fecha_inicio.Value < finUtc
                            && r.fecha_fin.Value > inicioUtc);

            if (traslape)
                return Conflict(new { error = "Ya existe una reserva en ese rango de fechas para esta habitación." });

            // Obtener el empleado logueado desde la sesión
            int empleadoId = HttpContext.Session.GetInt32("personaId") ?? 0;
            if (empleadoId == 0)
                return Unauthorized(new { error = "Sesión no válida. Por favor inicia sesión nuevamente." });

            var nuevaReserva = new reserva
            {
                clienteid = dto.clienteid,
                empleadoid = empleadoId,
                recursoid = dto.recursoid,
                estadoid = dto.estadoid > 0 ? dto.estadoid : 1, // 1 = Reservado por defecto
                fecha_inicio = inicioUtc,
                fecha_fin = finUtc,
                fecha_creacion = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-6), DateTimeKind.Utc),
                preciofinal = dto.preciofinal
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
        }

        // ==============================================
        // EXPORTAR .ICS — Feed de reservas de una habitación
        // ==============================================

        [HttpGet]
        public async Task<IActionResult> ExportarICS(int recursoid)
        {
            var recurso = await _context.recurso.FindAsync(recursoid);
            if (recurso == null) return NotFound();

            var reservas = await _context.reserva
                .Where(r => r.recursoid == recursoid
                         && r.estadoid != 3
                         && r.fecha_inicio.HasValue
                         && r.fecha_fin.HasValue)
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

                // Para reservas de todo el día usamos formato DATE (YYYYMMDD)
                // La fecha de fin en iCal para todo-el-día es EXCLUSIVA (el día siguiente)
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
            return File(bytes, "text/calendar; charset=utf-8", $"cocobeach-{recurso.nombre?.Replace(" ", "_")}.ics");
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

        // ==============================================
        // AJAX — Cambiar estado de una reserva
        // ==============================================

        [HttpPost]
        public async Task<IActionResult> CambiarEstadoReserva([FromBody] CambiarEstadoDto dto)
        {
            var reserva = await _context.reserva.FindAsync(dto.reservaid);
            if (reserva == null)
                return NotFound(new { error = "Reserva no encontrada." });

            // Estados válidos: 1=Reservado, 2=En proceso de reserva, 3=Disponible
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
        // AJAX — Editar reserva desde el calendario
        // ==============================================

        [HttpPost]
        public async Task<IActionResult> EditarReservaCalendario([FromBody] ReservaEditDto dto)
        {
            var reserva = await _context.reserva.FindAsync(dto.reservaid);
            if (reserva == null)
                return NotFound(new { error = "Reserva no encontrada." });

            if (dto.recursoid <= 0 || dto.clienteid <= 0)
                return BadRequest(new { error = "Habitación y cliente son requeridos." });

            var inicioUtc = DateTime.SpecifyKind(dto.fecha_inicio, DateTimeKind.Utc);
            var finUtc = DateTime.SpecifyKind(dto.fecha_fin, DateTimeKind.Utc);

            if (inicioUtc >= finUtc)
                return BadRequest(new { error = "La fecha de inicio debe ser anterior a la fecha de fin." });

            // Validar traslape excluyendo la reserva que se está editando
            var traslape = await _context.reserva
                .AnyAsync(r => r.reservaid != dto.reservaid          // excluir la propia reserva
                            && r.recursoid == dto.recursoid
                            && r.estadoid != 3
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
        }

        [AutenticationAttribute.Autenticacion]
        public async Task<IActionResult> Dashboard(int? mes, int? anio)
        {
            // Lógica del Filtro de Mes 
            var hoy = DateTime.Today;
            int mesFiltro = mes ?? hoy.Month;
            int anioFiltro = anio ?? hoy.Year;

            // Rango del mes para KPIs
            var inicioMes = DateTime.SpecifyKind(new DateTime(anioFiltro, mesFiltro, 1), DateTimeKind.Utc);
            var finMes = inicioMes.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59);

      
            var inicioHoy = DateTime.SpecifyKind(hoy, DateTimeKind.Utc);
            var finHoy = inicioHoy.AddDays(1).AddTicks(-1);

            ViewBag.MesFiltro = mesFiltro;
            ViewBag.AnioFiltro = anioFiltro;

            var meses = Enumerable.Range(1, 12).Select(m => new {
                Value = m,
                Text = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m)
            });
            ViewBag.MesesList = new SelectList(meses, "Value", "Text", mesFiltro);

            // 2. IDs de Estados
            var estados = await _context.estado.ToListAsync();
            int idDisponible = estados.FirstOrDefault(e => e.nombre == "Disponible")?.estadoid ?? 0;
            int idReservada = estados.FirstOrDefault(e => e.nombre == "Reservado")?.estadoid ?? 0;
            int idEnProceso = estados.FirstOrDefault(e => e.nombre == "En proceso de reserva")?.estadoid ?? 0;

            ViewBag.IdReservada = idReservada;
            ViewBag.IdEnProceso = idEnProceso;

            // KPIs del Mes
            var reservasMesQuery = _context.reserva
                .Where(r => r.fecha_inicio >= inicioMes && r.fecha_inicio <= finMes && r.estadoid != idDisponible);

            ViewBag.TotalReservasMes = await reservasMesQuery.CountAsync();
            ViewBag.TotalGananciasMes = await reservasMesQuery.SumAsync(r => r.preciofinal) ?? 0;

            // Filtrado por mes
            ViewBag.RankingHabitaciones = await (from res in _context.reserva
                                                 join rec in _context.recurso on res.recursoid equals rec.recursoid
                                                 where res.fecha_inicio >= inicioMes && res.fecha_inicio <= finMes && res.estadoid != idDisponible
                                                 group res by new { rec.nombre } into grupo
                                                 select new
                                                 {
                                                     Nombre = grupo.Key.nombre,
                                                     Reservas = grupo.Count(),
                                                     Ganancias = grupo.Sum(x => x.preciofinal) ?? 0
                                                 })
                                                 .OrderByDescending(x => x.Ganancias)
                                                 .ToListAsync();


        
            var estadoHabitaciones = await (from rec in _context.recurso
                                            join res in _context.reserva.Where(r => r.fecha_inicio <= finHoy && r.fecha_fin >= inicioHoy)
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


    }
}