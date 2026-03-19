using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class check_in
    {
        [Key]
        public int check_lnid { get; set; }
        public int reservaid { get; set; }
        public int empleadoid { get; set; }
        public DateTime? fecha_ingreso { get; set; }
        public DateTime? fecha_salida { get; set; }
    }
}
