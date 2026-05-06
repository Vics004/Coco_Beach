using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

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