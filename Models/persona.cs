using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class persona
    {
        [Key]
        public int personaid { get; set; }
        public string? nombre { get; set; }
        public string? apellido { get; set; }
        public string? correo { get; set; }
        public int? rolid { get; set; }
        public string? estado { get; set; }
        public string? telefono { get; set; }
    }
}
