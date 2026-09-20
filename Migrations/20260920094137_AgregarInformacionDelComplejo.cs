using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCabanas.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInformacionDelComplejo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Actividades",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComoLlegarAuto",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComoLlegarTren",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DondeComprar",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbarcaderoDireccion",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbarcaderoNombre",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbarcaderoTelefono",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanchasRemis",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Normas",
                table: "InformacionSitio",
                type: "TEXT",
                maxLength: 6000,
                nullable: true);

            CargarContenidoInicial(migrationBuilder);
        }

        // Carga el contenido del documento que se les manda a los huéspedes en las secciones nuevas.
        // Lo que ya estaba cargado no se pisa, salvo los textos de relleno del alta inicial ("Completá
        // acá...") y las comodidades de ese mismo alta, que ya no corresponden a un complejo en una isla.
        private static void CargarContenidoInicial(MigrationBuilder migrationBuilder)
        {
            static string Texto(params string[] lineas) => "'" + string.Join("\n", lineas).Replace("'", "''") + "'";

            var comoLlegar = Texto(
                "Las cabañas están en una isla del Delta y se llega únicamente en lancha. La lancha remis se toma en la Guardería Poseidón, en San Fernando; más abajo están los pasos para llegar hasta ahí en auto o en tren.",
                "Cuando llames a la lancha remis, decile que vas a la Cabaña Sidharta, de Sabrina: sabe dónde tiene que bajarte. Por favor, llamá a una sola lancha remis.");

            var auto = Texto(
                "Desde el ramal Tigre, bajá en la salida de la 197.",
                "Tomá a la derecha por la 197. Primero vas a pasar por debajo de la vía (estación Carupá).",
                "Continuá por la avenida Colón. Vas a cruzar otra vía (la del Tren de la Costa); seguí derecho.",
                "Después de la vía empieza un empedrado y, a mano izquierda, se ve un canal. Seguí por esa calle hasta donde termina.",
                "La calle termina en una rotonda chiquita sobre el río Luján. A la izquierda está la Prefectura y a la derecha la Guardería Poseidón; después de la guardería hay un muelle público, donde paran las lanchas remis.",
                "Cuando llegues al muelle, llamá a la lancha remis para que te busque.",
                "Podés dejar el auto en cualquier lugar, menos en la puerta de la guardería.");

            var tren = Texto(
                "Tomá el ramal Tigre desde Retiro o desde cualquiera de sus estaciones.",
                "Bajá en la estación Carupá (una antes de Tigre).",
                "Tomá el colectivo 710 (verde) en la calle Constitución, hasta la Guardería Poseidón.",
                "El colectivo termina en una rotonda chiquita sobre el río Luján. A la izquierda está la Prefectura y a la derecha la Guardería Poseidón; después de la guardería hay un muelle público, donde paran las lanchas remis.",
                "Cuando llegues al muelle, llamá a la lancha remis para que te busque.");

            var remis = Texto(
                "Leo | +54 9 11 3133-1665",
                "Beto | +54 9 11 5618-7841",
                "Ariel | +54 9 11 6292-8874 | Hasta 4 personas");

            var comodidades = Texto(
                "Pileta compartida con playa húmeda",
                "Vestuario",
                "Solárium",
                "Muelle compartido con área de pesca",
                "Wifi Starlink, internet satelital",
                "Parrilla privada en cada cabaña");

            var dondeComprar = Texto(
                "Proveeduría a 100 metros: productos de almacén, verdulería y carnicería (de 9 a 13 hs y de 15 a 22 hs).",
                "Rotisería a 50 metros.",
                "Bares a 400 metros y a 2 km.",
                "En la isla no hay agua potable: en la proveeduría se venden bidones de 10 litros.",
                "En la isla solo se puede pagar en efectivo.");

            var actividades = Texto(
                "Once kilómetros de senderos para hacer trekking o caminatas, visitando los distintos puntos turísticos.",
                "Restaurantes sobre los ríos: Tres Bocas, Arroyo Santa Rosa, Río Sarmiento y Río San Antonio.",
                "Caminando, a 1,3 km aproximadamente, empiezan “El Hornero”, “Remanso”, “Brujas” y “La Martita”.",
                "Pesca.");

            var normas = Texto(
                "Se llega únicamente en lancha.",
                "Ingreso a partir de las 12 hs y salida hasta las 10 hs. Una vez entregada la cabaña no se puede permanecer en las instalaciones.",
                "Fines de semana: ingreso a partir de las 12 hs y salida el domingo hasta las 20 hs.",
                "Para confirmar la reserva se abona la mitad del total, por transferencia, depósito o tarjeta de crédito o débito con MercadoPago (12% de interés). Los datos para pagar te los pasamos por WhatsApp.",
                "Al ingresar se completa el pago del 100% de la reserva en efectivo, y no se puede acortar la estadía.",
                "No ponemos toallas ni ropa de cama: traé las tuyas. Sí ponemos acolchados y almohadas.",
                "El agua no es potable.",
                "El horario de la pileta es de 10 a 20 hs.",
                "Los menores de 12 años no pueden estar en la pileta sin la supervisión de un adulto.",
                "Está prohibido escuchar música en la zona de la pileta.",
                "A partir de las 22 hs no se pueden hacer ruidos molestos.",
                "Está prohibido fumar dentro de las cabañas.",
                "No se aceptan visitas (salvo en fechas especiales).",
                "Se aceptan mascotas pequeñas.",
                "Está prohibido alimentar a los perros y dejarlos entrar a la cabaña o a la galería. Los perros que andan por el lugar no son nuestros y no nos hacemos cargo de lo que pueda pasar.",
                "La reserva no se devuelve ni puede cancelarse por lluvias o agua alta (sudestada), y no se puede cambiar la fecha.",
                "Si no te presentás el día del ingreso hasta las 22 hs, se pierde la reserva y la cabaña queda disponible para alquilar.",
                "No se devuelve el dinero de la estadía por cortes de luz.",
                "Al irte, avisá, dejá todo apagado (la luz y los aires acondicionados) y dejá la llave puesta en la puerta, del lado de afuera de la cabaña.",
                "La reserva implica haber aceptado estos términos y condiciones.");

            migrationBuilder.Sql($@"
UPDATE ""InformacionSitio"" SET
    ""Direccion"" = CASE WHEN ""Direccion"" LIKE 'Completá acá la dirección real%' THEN NULL ELSE ""Direccion"" END,
    ""ComoLlegar"" = CASE WHEN ""ComoLlegar"" LIKE 'Contanos acá cómo llegar%' THEN {comoLlegar} ELSE ""ComoLlegar"" END,
    ""Comodidades"" = CASE WHEN REPLACE(""Comodidades"", char(13), '') = 'Pileta compartida' || char(10) || 'Parrilla individual' || char(10) || 'WiFi' || char(10) || 'Estacionamiento' || char(10) || 'Muelle propio' || char(10) || 'Río a metros' THEN {comodidades} ELSE ""Comodidades"" END,
    ""InformacionAdicional"" = CASE WHEN ""InformacionAdicional"" LIKE 'Agregá acá cualquier información adicional%' THEN NULL ELSE ""InformacionAdicional"" END,
    ""ComoLlegarAuto"" = {auto},
    ""ComoLlegarTren"" = {tren},
    ""EmbarcaderoNombre"" = 'Guardería Poseidón',
    ""EmbarcaderoDireccion"" = 'Colón 10, San Fernando',
    ""EmbarcaderoTelefono"" = '011 4746-5656',
    ""LanchasRemis"" = {remis},
    ""DondeComprar"" = {dondeComprar},
    ""Actividades"" = {actividades},
    ""Normas"" = {normas};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Actividades",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "ComoLlegarAuto",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "ComoLlegarTren",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "DondeComprar",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "EmbarcaderoDireccion",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "EmbarcaderoNombre",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "EmbarcaderoTelefono",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "LanchasRemis",
                table: "InformacionSitio");

            migrationBuilder.DropColumn(
                name: "Normas",
                table: "InformacionSitio");
        }
    }
}
