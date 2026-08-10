using System.Diagnostics;
using VentasParalelo.Core.Aggregation;
using VentasParalelo.Core.DataGeneration;
using VentasParalelo.Core.IO;
using VentasParalelo.Core.Metrics;
using VentasParalelo.Core.Models;

if (args.Length == 0)
{
    ImprimirAyuda();
    return 1;
}

return args[0] switch
{
    "generar" => Generar(args),
    "comparar" => Comparar(args),
    _ => Fallback()
};

int Fallback()
{
    ImprimirAyuda();
    return 1;
}

int Generar(string[] args)
{
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
}

int Comparar(string[] args)
{
    var archivo = "data/ventas.csv";
    var hilos = new[] { 1, 2, 4, Environment.ProcessorCount };

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--archivo" when i + 1 < args.Length:
                archivo = args[++i];
                break;
            case "--hilos" when i + 1 < args.Length:
                hilos = args[++i].Split(',').Select(int.Parse).ToArray();
                break;
        }
    }

    if (!File.Exists(archivo))
    {
        Console.WriteLine($"No existe '{archivo}'. Genera un dataset primero con 'generar'.");
        return 1;
    }

    Console.WriteLine($"Cargando '{archivo}'...");
    var cargaSw = Stopwatch.StartNew();
    var registros = SalesCsvReader.ReadAll(archivo);
    cargaSw.Stop();
    Console.WriteLine($"{registros.Length:N0} filas cargadas en {cargaSw.Elapsed.TotalSeconds:F2}s.");
    Console.WriteLine();

    IAggregationStrategy[] estrategias =
    [
        new SequentialAggregator(),
        new LockDictionaryAggregator(),
        new ConcurrentDictionaryAggregator(),
        new LocalReductionAggregator()
    ];

    Console.WriteLine($"{"Estrategia",-48}{"Hilos",6}{"Tiempo (s)",12}{"Filas/seg",14}{"Speedup",10}{"Eficiencia",11}");
    Console.WriteLine(new string('-', 101));

    AggregationResult? referencia = null;
    double? baselineSegundos = null;

    foreach (var estrategia in estrategias)
    {
        var hilosAEjecutar = estrategia is SequentialAggregator ? new[] { 1 } : hilos;

        foreach (var h in hilosAEjecutar)
        {
            var (resultado, metricas) = BenchmarkRunner.Ejecutar(estrategia, registros, h);
            referencia ??= resultado;
            baselineSegundos ??= metricas.Duracion.TotalSeconds;

            var speedup = baselineSegundos.Value / metricas.Duracion.TotalSeconds;
            var eficiencia = speedup / metricas.Hilos;

            var marca = referencia.TotalesCoinciden(resultado) ? string.Empty : "  (!) totales distintos a la referencia";
            Console.WriteLine(
                $"{metricas.Estrategia,-48}{metricas.Hilos,6}{metricas.Duracion.TotalSeconds,12:F3}{metricas.FilasPorSegundo,14:N0}{speedup,10:F2}{eficiencia,11:P0}{marca}");
        }
    }

    return 0;
}

void ImprimirAyuda()
{
    Console.WriteLine("""
        Uso:
          generar  --filas <N> --salida <ruta.csv>
          comparar --archivo <ruta.csv> [--hilos 1,2,4,8]

        Ejemplos:
          dotnet run --project src/VentasParalelo.Cli -- generar --filas 1000000 --salida data/ventas_1m.csv
          dotnet run --project src/VentasParalelo.Cli -- comparar --archivo data/ventas_1m.csv --hilos 1,2,4,8
        """);
}
