using System.Globalization;
using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.IO;

/// <summary>
/// Lector de CSV de ventas. Los campos sucursal/producto son valores fijos sin comas,
/// por lo que se usa un split simple en vez de un parser CSV completo (mas rapido para millones de filas).
/// </summary>
public static class SalesCsvReader
{
    /// <summary>
    /// Carga el archivo completo en memoria como un arreglo, listo para ser particionado
    /// entre hilos por las distintas estrategias de agregacion.
    /// </summary>
    public static SaleRecord[] ReadAll(string path)
    {
        var records = new List<SaleRecord>();
        using var reader = new StreamReader(path);

        reader.ReadLine(); // encabezado
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            records.Add(ParseLine(line));
        }

        return [.. records];
    }

    public static SaleRecord ParseLine(ReadOnlySpan<char> line)
    {
        Span<Range> fieldRanges = stackalloc Range[5];
        var fieldCount = line.Split(fieldRanges, ',');

        if (fieldCount != 5)
            throw new FormatException($"Se esperaban 5 campos, se encontraron {fieldCount}: {line}");

        var fecha = DateOnly.ParseExact(line[fieldRanges[0]], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var sucursal = line[fieldRanges[1]].ToString();
        var producto = line[fieldRanges[2]].ToString();
        var cantidad = int.Parse(line[fieldRanges[3]], CultureInfo.InvariantCulture);
        var monto = decimal.Parse(line[fieldRanges[4]], CultureInfo.InvariantCulture);

        return new SaleRecord(fecha, sucursal, producto, cantidad, monto);
    }
}
