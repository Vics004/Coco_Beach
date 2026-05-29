using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Coco_Beach.Models;

namespace Coco_Beach.Servicios
{
    public class AuthorizeRoleAttribute : ActionFilterAttribute
    {
        private readonly string[] _allowedRoles;

        public AuthorizeRoleAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var usuarioId = context.HttpContext.Session.GetInt32("usuarioId");
            var rolUsuario = context.HttpContext.Session.GetString("tipoUsuario");

            if (usuarioId == null)
            {
                context.Result = new RedirectToActionResult("Autenticar", "Login", null);
                return;
            }
            // 👇 Resolver el DbContext igual que en el controller
            var db = context.HttpContext.RequestServices.GetService<Coco_BeachDbContext>();
            if (db != null)
            {
                var personaId = context.HttpContext.Session.GetInt32("personaId");
                if (personaId != null)
                {
                    var persona = db.persona.Find(personaId);
                    if (persona != null && persona.estado == false)
                    {
                        context.HttpContext.Session.Clear();
                        context.Result = new RedirectToActionResult("Autenticar", "Login", null);
                        return;
                    }
                }
            }

            if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(rolUsuario))
            {
                // No tiene permiso: redirigir a página de acceso denegado o al dashboard
                context.Result = new RedirectToActionResult("Index", "Login", new { area = "" });
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}