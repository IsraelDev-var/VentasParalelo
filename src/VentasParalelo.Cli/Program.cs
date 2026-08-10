using System.Diagnostics;
using VentasParalelo.Core.DataGeneration;

if (args.Length == 0 || args[0] != "generar")
{
    ImprimirAyuda();
    return 1;
}

var filas = 1_000_000L;
var salida = "data/ventas.csv";

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--filas" when i + 1 < args.Length:
            filas = long.Parse(args[++i]);
            break;
        case "--salida" when i + 1 < args.Length:
            salida = args[++i];
            break;
    }
}

var directorio = Path.GetDirectoryName(salida);
if (!string.IsNullOrEmpty(directorio))
    Directory.CreateDirectory(directorio);

Console.WriteLine($"Generando {filas:N0} filas en '{salida}'...");
var sw = Stopwatch.StartNew();
SalesDataGenerator.GenerateCsv(salida, filas);
sw.Stop();
Console.WriteLine($"Listo en {sw.Elapsed.TotalSeconds:F2}s.");

return 0;

void ImprimirAyuda()
{
    Console.WriteLine("""
        Uso:
          generar --filas <N> --salida <ruta.csv>

        Ejemplo:
          dotnet run --project src/VentasParalelo.Cli -- generar --filas 1000000 --salida data/ventas_1m.csv
        """);
}
