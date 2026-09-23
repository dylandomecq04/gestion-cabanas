using System.ComponentModel.DataAnnotations;
using PhoneNumbers;

namespace GestionCabanas.Models
{
    /// <summary>
    /// Valida que el teléfono sea un número real y completo (ej: rechaza "15113311", al que
    /// le falta el código de área), no solo que tenga la cantidad de dígitos correcta.
    /// Argentina es el país por defecto; los números de otros países deben incluir el
    /// +código de país para validarse contra las reglas de ese país.
    /// </summary>
    public class TelefonoValidoAttribute : ValidationAttribute
    {
        public TelefonoValidoAttribute()
        {
            ErrorMessage = "Ingresá un teléfono válido, con código de área (ej: 011 15-1234-5678), o el +código de país si es de otro país";
        }

        public override bool IsValid(object? value)
        {
            if (value is not string telefono || string.IsNullOrWhiteSpace(telefono))
            {
                return true;
            }

            try
            {
                var util = PhoneNumberUtil.GetInstance();
                var numero = util.Parse(telefono, "AR");
                return util.IsValidNumber(numero);
            }
            catch (NumberParseException)
            {
                return false;
            }
        }
    }
}
