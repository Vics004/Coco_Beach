using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class estado
    {
        [Key]
        public int estadoid { get; set; }
        public string? nombre { get; set; }
    }
}
