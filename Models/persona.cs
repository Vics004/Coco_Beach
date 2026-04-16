using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coco_Beach.Models
{
    public class persona
    {
        [Key]
        public int personaid { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras")]
        [MinLength(2, ErrorMessage = "Mínimo 2 caracteres")]
        public string? nombre { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El apellido solo puede contener letras")]
        [MinLength(2, ErrorMessage = "Mínimo 2 caracteres")]
        public string? apellido { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string? correo { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un rol")]
        public int? rolid { get; set; }
        public bool estado { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        public string? telefono { get; set; }


        public rol? rol { get; set; }

        // Propiedades calculadas (no se guardan en BD)
        [NotMapped]
        public string CodigoPais => telefono?.Split('|')[0] ?? "";

        [NotMapped]
        public string NumeroTelefono => telefono?.Split('|')[1] ?? "";
    }
}
