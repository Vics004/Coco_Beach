using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coco_Beach.Models
{
    public class auditoria
    {
        [Key]
        public int auditoriaid { get; set; }

        [Required]
        [MaxLength(100)]
        public string tabla_afectada { get; set; } = string.Empty;

        [Required]
        public int registroid { get; set; }

        [Required]
        [MaxLength(20)]
        public string accion { get; set; } = string.Empty;  // 'INSERT', 'UPDATE'

        [Column(TypeName = "jsonb")]
        public string? valor_anterior { get; set; }  // JSON en texto

        [Column(TypeName = "jsonb")]
        public string? valor_nuevo { get; set; }     // JSON en texto

        [Required]
        public int usuarioid { get; set; }

        public DateTime? fecha_accion { get; set; }
    }
}
