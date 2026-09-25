using System.Text.RegularExpressions;
using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCabanas.Services
{
    public record DefinicionMensajeWhatsApp(string Clave, string Titulo, string Descripcion, string TextoOriginal, (string Variable, string Explicacion)[] Variables);

    /// <summary>
    /// Guarda y arma los mensajes de WhatsApp. Cada texto usa variables entre llaves (por ejemplo
    /// <c>{nombre}</c>) que se reemplazan al armar el mensaje. Si una variable vale <c>null</c>, se
    /// quita la línea completa donde aparece.
    /// </summary>
    public class MensajesWhatsAppService
    {
        public const string SolicitudDelHuesped = "solicitud-huesped";
        public const string SolicitudRecibida = "solicitud-recibida";
        public const string Cancelacion = "cancelacion";
        public const string Confirmacion = "confirmacion";

        private static readonly (string, string)[] VariablesBasicas =
        {
            ("{nombre}", "nombre del huésped"),
            ("{cabana}", "nombre de la cabaña"),
            ("{desde}", "fecha de ingreso (dd/mm/aaaa)"),
            ("{hasta}", "fecha de salida (dd/mm/aaaa)")
        };

        public static readonly IReadOnlyList<DefinicionMensajeWhatsApp> Definiciones = new[]
        {
            new DefinicionMensajeWhatsApp(
                SolicitudDelHuesped,
                "Solicitud enviada por el huésped",
                "Lo que le llega a Cabañas Sidharta cuando el huésped toca «Escribinos por WhatsApp» después de enviar su solicitud en el sitio.",
                "Hola, ¿cómo están? 😊\n\nSoy {nombre} y quería coordinar mi reserva en *Cabañas Sidharta*.\n\n🏡 *Cabaña:* {cabana}\n👥 *Huéspedes:* {personas}\n📅 *Ingreso:* {desde}\n📅 *Salida:* {hasta}\n💰 *Valor total de la reserva:* {valor_total}\n\nLa cabaña asignada sería *{cabana}*.\n{salida_anticipada}\n\nQuedo atento/a para confirmar la disponibilidad y avanzar con la reserva.\n\n¡Muchas gracias! 😊",
                VariablesBasicas.Concat(new[]
                {
                    ("{personas}", "cantidad de personas, con la palabra (ej: 4 personas)"),
                    ("{valor_total}", "valor total de la reserva (ej: $150.000, o «a consultar»)"),
                    ("{salida_anticipada}", "«Salgo el domingo a la noche.» Si no eligió salida anticipada, se quita la línea donde lo pongas")
                }).ToArray()),
            new DefinicionMensajeWhatsApp(
                SolicitudRecibida,
                "Respuesta a una solicitud nueva",
                "Lo que enviamos nosotros al huésped desde Reservas → Solicitudes para coordinar el pago.",
                "Hola, cómo estás? Somos de Cabañas Sidharta.\n\nRecibí tu solicitud de reserva para el período del {desde} a partir de las 12hs hasta {hasta} a las 10hs, a nombre de {nombre} en Cabaña {cabana}. Te escribo para coordinar y confirmar la misma.\n\nEl valor total de la estadía es {valor_total}. {pago}\n\nPara poder mantener la reserva, contás con un plazo de 24 horas para realizar {plazo_pago}. A continuación, te envío los datos de la cuenta.\n\nUna vez efectuado el pago, por favor enviame el comprobante para poder continuar con la confirmación de la reserva.\n\nMuchas gracias!",
                VariablesBasicas.Concat(new[]
                {
                    ("{valor_total}", "valor total de la estadía (ej: $150.000, o «a confirmar»)"),
                    ("{pago}", "frase de cómo se paga: seña del 50% o, si es una sola noche, el total"),
                    ("{plazo_pago}", "«el pago de la seña» o, si es una sola noche, «el pago»")
                }).ToArray()),
            new DefinicionMensajeWhatsApp(
                Cancelacion,
                "Solicitud cancelada",
                "Lo que enviamos al huésped cuando cancelamos su solicitud.",
                "Hola, ¿cómo estás?\n\n❌ *TU SOLICITUD DE RESERVA FUE CANCELADA*\n\n*Titular:* {nombre}\n*Cabaña:* {cabana}\n*Fecha de ingreso:* {desde}\n*Fecha de salida:* {hasta}\n\nLamentablemente, quedó cancelada por falta de pago o disponibilidad.\n\nSi todavía te interesa reservar, quedamos a disposición para coordinar una nueva fecha.\n\n¡Muchas gracias!",
                VariablesBasicas),
            new DefinicionMensajeWhatsApp(
                Confirmacion,
                "Reserva confirmada",
                "Lo que enviamos al huésped cuando su solicitud se aprobó y pasó a ser una reserva.",
                TextoConfirmacionOriginal,
                VariablesBasicas.Concat(new[]
                {
                    ("{personas}", "cantidad de huéspedes, con menores si hay (ej: 4 personas (1 menor))"),
                    ("{saldo_pendiente}", "monto que falta pagar. Si no queda saldo, se quita la línea donde lo pongas")
                }).ToArray())
        };

        private const string TextoConfirmacionOriginal =
            "Hola, ¿cómo estás?\n\n" +
            "🏡 *TU RESERVA EN CABAÑAS SIDHARTA ESTÁ CONFIRMADA*\n\n" +
            "*Titular:* {nombre}\n" +
            "*Cabaña:* {cabana}\n" +
            "*Fecha de ingreso:* {desde}\n" +
            "*Fecha de salida:* {hasta}\n" +
            "*Cantidad de huéspedes:* {personas}\n\n" +
            "🕐 *Horarios*\n" +
            "Ingreso: 12:00 hs\n" +
            "Salida: 10:00 hs\n\n" +
            "💰 *Saldo pendiente:* {saldo_pendiente}.\n\n" +
            "📋 *Datos de los huéspedes*\n" +
            "Cuando puedas, por favor pasanos nombre, apellido y DNI de todas las personas que se van a hospedar, incluidos los menores.\n\n" +
            "🧳 *Qué deben traer*\n" +
            "Recordá traer:\n" +
            "- Sábanas\n" +
            "- Toallones\n" +
            "- Toalla de mano\n" +
            "- Repasador\n\n" +
            "Las cabañas cuentan con acolchados y almohadas.\n\n" +
            "🚤 *CÓMO LLEGAR*\n" +
            "Estamos en la isla y se llega únicamente en lancha.\n\n" +
            "*Lancha remis*\n" +
            "La lancha sale desde:\n" +
            "Guardería Poseidón\n" +
            "📍 Colón 10, San Fernando\n" +
            "El viaje tiene una duración aproximada de 15 minutos.\n\n" +
            "📞 *Contactos de lancheros:*\n" +
            "- Leo: +54 9 11 3133-1665\n" +
            "- Estela: +54 9 11 3430-4574\n" +
            "- Juan: +54 9 11 5575-1756\n" +
            "- Beto: +54 9 11 5618-7841\n\n" +
            "📍 Para llegar a la guardería, colocá en el GPS: \"Colón 10, San Fernando\".\n\n" +
            "🚗 *Estacionamiento*\n" +
            "Podés dejar el auto en cualquier lugar habilitado, excepto en la puerta de la guardería.\n\n" +
            "🏡 *INFORMACIÓN DEL COMPLEJO*\n" +
            "- El complejo cuenta con 9 cabañas.\n" +
            "- La pileta y el muelle son espacios compartidos.\n" +
            "- Cada cabaña cuenta con: aire acondicionado frío/calor, agua caliente, TV, vajilla, heladera, anafe, horno eléctrico, luz de emergencia en la cocina, parrilla privada y Wi-Fi.\n" +
            "- Hay una proveeduría a 100 metros.\n" +
            "- El agua no es potable.\n" +
            "- No se proporciona ropa de cama ni toallas. Los huéspedes deben traerlas.\n" +
            "- Se proporcionan acolchados y almohadas.\n\n" +
            "🏊 *REGLAMENTO DE LA PILETA*\n" +
            "Horario: 10:00 a 20:00 hs\n" +
            "Está prohibido:\n" +
            "🚫 Consumir alcohol.\n" +
            "🚫 Escuchar música.\n" +
            "🚫 Zambullirse.\n\n" +
            "👨‍👩‍👧 Los menores de 12 años no pueden permanecer en la pileta sin la supervisión de un adulto.\n\n" +
            "📜 *REGLAMENTO Y TÉRMINOS Y CONDICIONES*\n" +
            "Por favor, leé atentamente los siguientes términos y condiciones:\n" +
            "- El horario de ingreso es a las 12:00 hs y el horario de salida es a las 10:00 hs, salvo que se haya acordado otro horario previamente.\n" +
            "- Una vez entregada la cabaña, no se puede permanecer en las instalaciones después del horario de salida.\n" +
            "- El horario de la pileta es de 10:00 a 20:00 hs.\n" +
            "- A partir de las 22:00 hs no se pueden realizar ruidos molestos.\n" +
            "- Los menores de 12 años no pueden permanecer en la pileta sin la supervisión de un adulto.\n" +
            "- Está prohibido fumar dentro de las cabañas.\n" +
            "- No se aceptan visitas.\n" +
            "- La reserva no es reembolsable.\n" +
            "- No se permite cambiar la fecha de la reserva.\n" +
            "- Al ingresar se deberá completar el pago del 100% de la reserva en efectivo.\n" +
            "- No se puede acortar la estadía una vez realizado el ingreso.\n" +
            "- La reserva no puede cancelarse por lluvias ni por agua alta (sudestada).\n" +
            "- En caso de no presentarse el día del ingreso hasta las 22:00 hs, se pierde la reserva y la cabaña queda disponible para ser alquilada.\n" +
            "- Está prohibido alimentar a los perros y permitirles el acceso a las cabañas o galerías.\n" +
            "- Los perros que se encuentran en la zona no pertenecen al complejo. No nos hacemos responsables por ninguna situación que pueda ocurrir con ellos.\n" +
            "- No se devuelve el dinero de la estadía por cortes de luz ni por sudestada.\n" +
            "- Cada huésped debe llevar su propio botiquín y todo aquello que considere necesario durante su estadía.\n\n" +
            "⚠️ *IMPORTANTE*\n" +
            "La reserva implica la aceptación de todos estos términos y condiciones.\n\n" +
            "Muchas gracias por elegir Cabañas Sidharta.\n" +
            "¡Los esperamos! 🌿🏡";

        private static readonly Regex Variable = new(@"\{[a-z_]+\}", RegexOptions.Compiled);

        private readonly ApplicationDbContext _db;

        public MensajesWhatsAppService(ApplicationDbContext db)
        {
            _db = db;
        }

        public static DefinicionMensajeWhatsApp? Buscar(string clave) =>
            Definiciones.FirstOrDefault(d => d.Clave == clave);

        /// <summary>Texto vigente de cada mensaje (el guardado, o el original si nunca se editó).</summary>
        public async Task<Dictionary<string, string>> ObtenerTextosAsync()
        {
            var guardados = await _db.MensajesWhatsApp.AsNoTracking().ToDictionaryAsync(m => m.Clave, m => m.Texto);
            return Definiciones.ToDictionary(d => d.Clave, d => guardados.TryGetValue(d.Clave, out var t) ? t : d.TextoOriginal);
        }

        public async Task<string> ObtenerTextoAsync(string clave) => (await ObtenerTextosAsync())[clave];

        public async Task GuardarAsync(string clave, string texto)
        {
            var fila = await _db.MensajesWhatsApp.FirstOrDefaultAsync(m => m.Clave == clave);
            if (fila is null)
            {
                _db.MensajesWhatsApp.Add(new MensajeWhatsApp { Clave = clave, Texto = texto });
            }
            else
            {
                fila.Texto = texto;
            }
            await _db.SaveChangesAsync();
        }

        public async Task RestaurarAsync(string clave)
        {
            var fila = await _db.MensajesWhatsApp.FirstOrDefaultAsync(m => m.Clave == clave);
            if (fila is not null)
            {
                _db.MensajesWhatsApp.Remove(fila);
                await _db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Reemplaza las variables del texto. Las que no se conocen se dejan tal cual, y una variable
        /// con valor null quita la línea entera en la que aparece.
        /// </summary>
        public static string Renderizar(string texto, IReadOnlyDictionary<string, string?> valores)
        {
            var lineas = new List<string>();
            var quitoAlguna = false;
            foreach (var linea in texto.Replace("\r\n", "\n").Split('\n'))
            {
                var descartar = false;
                var resultado = Variable.Replace(linea, m =>
                {
                    var nombre = m.Value.Trim('{', '}');
                    if (!valores.TryGetValue(nombre, out var valor)) return m.Value;
                    if (valor is null) descartar = true;
                    return valor ?? "";
                });
                if (descartar) { quitoAlguna = true; continue; }
                lineas.Add(resultado);
            }

            var final = string.Join("\n", lineas);
            return quitoAlguna ? Regex.Replace(final, @"\n{3,}", "\n\n") : final;
        }

        // ---- Armado de cada mensaje ----

        private static string Dinero(decimal monto) => "$" + monto.ToString("N0");

        private static Dictionary<string, string?> Basicos(Reserva r, string? cabanas = null) => new()
        {
            ["nombre"] = r.NombreHuesped,
            ["cabana"] = cabanas ?? r.Cabana?.Nombre,
            ["desde"] = r.FechaDesde.ToString("dd/MM/yyyy"),
            ["hasta"] = r.FechaHasta.ToString("dd/MM/yyyy")
        };

        public static string ArmarSolicitudDelHuesped(string plantilla, string nombre, string cabanas, DateTime desde, DateTime hasta,
            int personas, decimal? total, bool salidaAnticipada) =>
            Renderizar(plantilla, new Dictionary<string, string?>
            {
                ["nombre"] = nombre,
                ["cabana"] = cabanas,
                ["desde"] = desde.ToString("dd/MM/yyyy"),
                ["hasta"] = hasta.ToString("dd/MM/yyyy"),
                ["personas"] = $"{personas} {(personas == 1 ? "persona" : "personas")}",
                ["valor_total"] = total.HasValue ? Dinero(total.Value) : "a consultar",
                ["salida_anticipada"] = salidaAnticipada ? "Salgo el domingo a la noche." : null
            });

        public static string ArmarSolicitudRecibida(string plantilla, Reserva r)
        {
            var esUnaNoche = (r.FechaHasta - r.FechaDesde).Days == 1;
            var valorTexto = r.Valor.HasValue ? Dinero(r.Valor.Value) : "a confirmar";
            string pago, plazo;
            if (esUnaNoche)
            {
                pago = $"Para confirmar la reserva, al ser de una sola noche, se abona el total de la estadía ({valorTexto}).";
                plazo = "el pago";
            }
            else
            {
                var sena = r.Valor.HasValue ? Dinero(Math.Round(r.Valor.Value * 0.5m)) : "a confirmar";
                pago = $"Para confirmar la reserva, se abona una seña del 50% ({sena}) y el resto al llegar.";
                plazo = "el pago de la seña";
            }

            var valores = Basicos(r);
            valores["valor_total"] = valorTexto;
            valores["pago"] = pago;
            valores["plazo_pago"] = plazo;
            return Renderizar(plantilla, valores);
        }

        public static string ArmarCancelacion(string plantilla, Reserva r) => Renderizar(plantilla, Basicos(r));

        public static string ArmarConfirmacion(string plantilla, Reserva r)
        {
            var valores = Basicos(r);
            valores["personas"] = $"{r.CantidadPersonas} {(r.CantidadPersonas == 1 ? "persona" : "personas")}"
                + (r.CantidadMenores > 0 ? $" ({r.CantidadMenores} menor{(r.CantidadMenores == 1 ? "" : "es")})" : "");
            valores["saldo_pendiente"] = r.Valor.HasValue
                ? (r.Valor.Value == 0 ? null : Dinero(r.Valor.Value))
                : "a confirmar";
            return Renderizar(plantilla, valores);
        }
    }
}
