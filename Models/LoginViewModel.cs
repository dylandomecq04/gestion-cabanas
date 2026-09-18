using System.ComponentModel.DataAnnotations;

namespace GestionCabanas.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Ingresá el usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresá la contraseña")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Mantener la sesión iniciada")]
        public bool Recordarme { get; set; } = true;

        public string? ReturnUrl { get; set; }
    }
}
