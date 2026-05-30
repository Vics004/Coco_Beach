// DESPUÉS
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

        [Range(0.01, double.MaxValue, ErrorMessage = "El precio final debe ser mayor a cero.")]
        public double? preciofinal { get; set; }

        [MaxLength(200, ErrorMessage = "El comentario no puede exceder los 200 caracteres.")]
        public string? comentario { get; set; }
    }
}