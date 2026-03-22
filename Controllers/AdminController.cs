using Coco_Beach.Models;
using Coco_Beach.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> Index()
        {
            var personasConUsuario = await _context.persona
                .Include(p => p.rol)
                .Where(p => _context.usuario.Any(u => u.personaid == p.personaid))
                .ToListAsync();

            return View(personasConUsuario);
        }

        [AutenticationAttribute.Autenticacion]
        // GET: Admin/Create
        public IActionResult Create()
        {
            ViewBag.RolSelect = new SelectList(_context.rol, "rolid", "nombre");
            return View();
        }

        [AutenticationAttribute.Autenticacion]
        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
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

                return RedirectToAction(nameof(Index));
            }

            ViewBag.RolSelect = new SelectList(_context.rol, "rolid", "nombre", persona.rolid);
            return View(persona);
        }

        [AutenticationAttribute.Autenticacion]
        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
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
        public async Task<IActionResult> Edit(int id,
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

                return RedirectToAction(nameof(Index));
            }

            ViewBag.RolSelect = new SelectList(_context.rol.ToList(), "rolid", "nombre", persona.rolid);
            return View(persona);
        }

        [AutenticationAttribute.Autenticacion]
        // GET: Admin/Delete/5
        public async Task<IActionResult> Delete(int? id)
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
            return RedirectToAction(nameof(Index));
        }

        [AutenticationAttribute.Autenticacion]
        private bool PersonaExists(int id)
        {
            return _context.persona.Any(e => e.personaid == id);
        }
    }
}
