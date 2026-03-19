using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class recurso
    {
        [Key]
        public int recursoid { get; set; }
        public string? nombre { get; set; }
        public bool? libre { get; set; }
        public string? descripcion { get; set; }
        public int? capacidad { get; set; }
        public double? precio { get; set; }
    }
}
