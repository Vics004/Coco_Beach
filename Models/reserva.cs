using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class reserva
    {
        [Key]
        public int reservaid { get; set; }
        public int clienteid { get; set; }
        public int empleadoid { get; set; }
        public int recursoid { get; set; }
        public int estadoid { get; set; }
        public DateTime? fecha_inicio { get; set; }
        public DateTime? fecha_fin { get; set; }
        public DateTime? fecha_creacion { get; set; }
        public double? preciofinal { get; set; }
    }
}
