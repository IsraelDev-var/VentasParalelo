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
    "escalar" => Escalar(args),
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

    EjecutarComparacion(registros, hilos);
    return 0;
}

void EjecutarComparacion(SaleRecord[] registros, int[] hilos)
{
    IAggregationStrategy[] estrategias =
    [
        new SequentialAggregator(),
        new LockDictionaryAggregator(),
        new ConcurrentDictionaryAggregator(),
        new LocalReductionAggregator(),
        new RoundRobinReductionAggregator(),
        new DynamicChunkReductionAggregator(),
        new HierarchicalReductionAggregator(),
        new CoarseGrainedTaskAggregator(),
        new PlinqGroupByAggregator()
    ];

    Console.WriteLine($"{"Estrategia",-52}{"Hilos",6}{"Tiempo (s)",12}{"Filas/seg",14}{"Speedup",10}{"Eficiencia",11}");
    Console.WriteLine(new string('-', 105));

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
                $"{metricas.Estrategia,-52}{metricas.Hilos,6}{metricas.Duracion.TotalSeconds,12:F3}{metricas.FilasPorSegundo,14:N0}{speedup,10:F2}{eficiencia,11:P0}{marca}");

            if (resultado.Diagnosticos is { } diag)
            {
                Console.WriteLine(
                    $"{"",52}    mapeo={diag.TiempoMapeo.TotalSeconds,7:F3}s  reduccion={diag.TiempoReduccion.TotalSeconds,7:F3}s  contencion={diag.TiempoContencion.TotalSeconds,7:F3}s");
            }
        }
    }
}

int Escalar(string[] args)
{
    var tipo = "fuerte";
    var hilos = new[] { 1, 2, 4, Environment.ProcessorCount };
    var volumenes = new long[] { 1_000_000, 5_000_000, 20_000_000 };
    var filasBase = 250_000L;
    var estrategiaNombre = "local";

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--tipo" when i + 1 < args.Length:
                tipo = args[++i];
                break;
            case "--hilos" when i + 1 < args.Length:
                hilos = args[++i].Split(',').Select(int.Parse).ToArray();
                break;
            case "--volumenes" when i + 1 < args.Length:
                volumenes = args[++i].Split(',').Select(long.Parse).ToArray();
                break;
            case "--filas-base" when i + 1 < args.Length:
                filasBase = long.Parse(args[++i]);
                break;
            case "--estrategia" when i + 1 < args.Length:
                estrategiaNombre = args[++i];
                break;
        }
    }

    IAggregationStrategy estrategia = estrategiaNombre switch
    {
        "local" => new LocalReductionAggregator(),
        "arbol" => new HierarchicalReductionAggregator(),
        "lock" => new LockDictionaryAggregator(),
        "concurrent" => new ConcurrentDictionaryAggregator(),
        "roundrobin" => new RoundRobinReductionAggregator(),
        "chunking" => new DynamicChunkReductionAggregator(),
        "grueso" => new CoarseGrainedTaskAggregator(),
        "plinq" => new PlinqGroupByAggregator(),
        _ => throw new ArgumentException($"Estrategia desconocida: '{estrategiaNombre}'.")
    };

    Console.WriteLine($"Estrategia: {estrategia.Nombre}");
    Console.WriteLine();

    if (tipo == "fuerte")
        EscalabilidadFuerte(estrategia, volumenes, hilos);
    else if (tipo == "debil")
        EscalabilidadDebil(estrategia, filasBase, hilos);
    else
        throw new ArgumentException($"--tipo debe ser 'fuerte' o 'debil', se recibio '{tipo}'.");

    return 0;
}

void EscalabilidadFuerte(IAggregationStrategy estrategia, long[] volumenes, int[] hilos)
{
    Console.WriteLine("Escalabilidad fuerte: mismo dataset, mas hilos (speedup y eficiencia respecto a 1 hilo).");
    Console.WriteLine();

    foreach (var volumen in volumenes)
    {
        Console.WriteLine($"== {volumen:N0} filas ==");
        var registros = SalesDataGenerator.GenerateInMemory(volumen);

        double? baseline = null;
        foreach (var h in hilos)
        {
            var (_, metricas) = BenchmarkRunner.Ejecutar(estrategia, registros, h);
            baseline ??= metricas.Duracion.TotalSeconds;

            var speedup = baseline.Value / metricas.Duracion.TotalSeconds;
            var eficiencia = speedup / h;
            Console.WriteLine(
                $"  hilos={h,-3}  tiempo={metricas.Duracion.TotalSeconds,8:F3}s  filas/seg={metricas.FilasPorSegundo,14:N0}  speedup={speedup,6:F2}  eficiencia={eficiencia,7:P0}");
        }

        Console.WriteLine();
    }
}

void EscalabilidadDebil(IAggregationStrategy estrategia, long filasBase, int[] hilos)
{
    Console.WriteLine($"Escalabilidad debil: filas = {filasBase:N0} x hilos (ideal: tiempo constante).");
    Console.WriteLine();

    double? tiempoUnHilo = null;
    foreach (var h in hilos)
    {
        var filas = filasBase * h;
        var registros = SalesDataGenerator.GenerateInMemory(filas);
        var (_, metricas) = BenchmarkRunner.Ejecutar(estrategia, registros, h);
        tiempoUnHilo ??= metricas.Duracion.TotalSeconds;

        var eficienciaDebil = tiempoUnHilo.Value / metricas.Duracion.TotalSeconds;
        Console.WriteLine(
            $"  hilos={h,-3}  filas={filas,12:N0}  tiempo={metricas.Duracion.TotalSeconds,8:F3}s  filas/seg={metricas.FilasPorSegundo,14:N0}  eficiencia-debil={eficienciaDebil,7:P0}");
    }
}

void ImprimirAyuda()
{
    Console.WriteLine("""
        Uso:
          generar  --filas <N> --salida <ruta.csv>
          comparar --archivo <ruta.csv> [--hilos 1,2,4,8]
          escalar  --tipo fuerte|debil [--estrategia local|arbol|lock|concurrent|roundrobin|chunking|grueso|plinq]
                   [--hilos 1,2,4,8] [--volumenes 1000000,5000000,20000000] [--filas-base 250000]

        Ejemplos:
          dotnet run --project src/VentasParalelo.Cli -- generar --filas 1000000 --salida data/ventas_1m.csv
          dotnet run --project src/VentasParalelo.Cli -- comparar --archivo data/ventas_1m.csv --hilos 1,2,4,8
          dotnet run --project src/VentasParalelo.Cli -- escalar --tipo fuerte --volumenes 1000000,5000000
          dotnet run --project src/VentasParalelo.Cli -- escalar --tipo debil --filas-base 250000 --hilos 1,2,4,8
        """);
}
