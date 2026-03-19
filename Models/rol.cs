using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class rol
    {
        [Key]
        public int rolid { get; set; }
        public string? nombre { get; set; }
    }
}
