using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class usuario
    {
        [Key]
        public int usuarioid { get; set; }
        public string? password { get; set; }
        public int personaid { get; set; }
    }
}
