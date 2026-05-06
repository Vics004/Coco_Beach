using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Coco_Beach.Servicios
{
    public class LayoutInjectorAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Controller is Controller controller)
            {
                var rol = context.HttpContext.Session.GetString("tipoUsuario");
                string layout = "_Layout_Administrador"; // por defecto

                switch (rol)
                {
                    case "Administrador":
                        layout = "_Layout_Administrador";
                        break;
                    case "Dueño":
                        layout = "_Layout_Dueño";
                        break;
                    case "Encargado":
                        layout = "_Layout_Encargado";
                        break;
                    case "Gerente de Hotel":
                        layout = "_Layout_GerenteHotel";
                        break;
                    case "Gerente de Rancho":
                        layout = "_Layout_GerenteRancho";
                        break;
                    default:
                        layout = "_Layout_Administrador"; // fallback seguro
                        break;
                }

                controller.ViewBag.Layout = layout;
            }

            base.OnActionExecuted(context);
        }
    }
}