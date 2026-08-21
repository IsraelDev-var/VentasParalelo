using System.Globalization;
using System.Text;
using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.DataGeneration;

/// <summary>
/// Genera un CSV sintético de transacciones de punto de venta (fecha,sucursal,producto,cantidad,monto),
/// simulando la carga nocturna de una cadena de tiendas.
/// </summary>
public static class SalesDataGenerator
{
    private static readonly string[] Sucursales =
    [
        "Santo Domingo Centro", "Santiago", "La Vega", "San Cristobal",
        "Punta Cana", "Puerto Plata", "San Pedro de Macoris", "Bavaro"
    ];

    private static readonly string[] Productos =
    [
        "Panel Solar 450W", "Panel Solar 550W", "Inversor 5kW", "Inversor 10kW",
        "Bateria Litio 5kWh", "Bateria Litio 10kWh", "Controlador de Carga",
        "Cable Solar 10AWG", "Estructura de Montaje", "Microinversor",
        "Medidor Bidireccional", "Kit de Conexion a Tierra"
    ];

    public static void GenerateCsv(string path, long rowCount, int seed = 12345)
    {
        var random = new Random(seed);
        var startDate = new DateOnly(2025, 1, 1);

        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        writer.WriteLine("fecha,sucursal,producto,cantidad,monto");

        for (long i = 0; i < rowCount; i++)
        {
            var r = GenerarRegistro(random, startDate);

            writer.Write(r.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.Sucursal);
            writer.Write(',');
            writer.Write(r.Producto);
            writer.Write(',');
            writer.Write(r.Cantidad.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.WriteLine(r.Monto.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Genera el dataset directamente en memoria (sin pasar por CSV), pensado para el barrido
    /// de escalabilidad: evita el costo de escribir/leer disco al comparar muchos volumenes.
    /// Usa la misma logica de generacion de filas que <see cref="GenerateCsv"/>.
    /// </summary>
    public static SaleRecord[] GenerateInMemory(long rowCount, int seed = 12345)
    {
        var random = new Random(seed);
        var startDate = new DateOnly(2025, 1, 1);
        var registros = new SaleRecord[rowCount];

        for (long i = 0; i < rowCount; i++)
            registros[i] = GenerarRegistro(random, startDate);

        return registros;
    }

    private static SaleRecord GenerarRegistro(Random random, DateOnly startDate)
    {
        const int dateRangeDays = 365;

        var fecha = startDate.AddDays(random.Next(dateRangeDays));
        var sucursal = Sucursales[random.Next(Sucursales.Length)];
        var producto = Productos[random.Next(Productos.Length)];
        var cantidad = random.Next(1, 21);
        var precioUnitario = 500 + random.NextDouble() * 45000;
        var monto = Math.Round((decimal)(cantidad * precioUnitario), 2);

        return new SaleRecord(fecha, sucursal, producto, cantidad, monto);
    }
}
