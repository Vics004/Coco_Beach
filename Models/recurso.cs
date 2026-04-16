using System.ComponentModel.DataAnnotations;

namespace Coco_Beach.Models
{
    public class recurso
    {
        [Key]
        public int recursoid { get; set; }

        [Required(ErrorMessage = "El nombre de la habitación es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
        public string? nombre { get; set; }

        [Required(ErrorMessage = "El estado de la habitación es obligatorio.")]
        public bool? libre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
        public string? descripcion { get; set; }

        [Required(ErrorMessage = "La capacidad es obligatoria.")]
        [Range(1, 20, ErrorMessage = "La capacidad debe ser entre 1 y 20 personas.")]
        public int? capacidad { get; set; }

        [Required(ErrorMessage = "El precio por noche es obligatorio.")]
        [Range(0.01, 999999.99, ErrorMessage = "El precio debe ser mayor a 0 y menor a 1,000,000.")]
        public double? precio { get; set; }
    }
}
