using System.Diagnostics;
using VentasParalelo.Core.Metrics;
using VentasParalelo.Core.Models;
using VentasParalelo.Core.Partitioning;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Grano grueso puro: parte el arreglo en exactamente <c>P</c> porciones grandes (una por
/// hilo, no muchos chunks chicos como <see cref="DynamicChunkReductionAggregator"/>). Cada
/// hilo escribe su <see cref="AggregationResult"/> local en su propio slot de un arreglo — sin
/// tocar ningun estado compartido ni tomar ningun lock mientras procesa: cero dependencia entre
/// hilos durante el computo. Solo despues de que <see cref="Parallel.For(int,int,ParallelOptions,Action{int})"/>
/// termina se fusionan los resultados, de forma secuencial y en un solo hilo — no hace falta
/// sincronizar el merge porque para ese momento ya no hay escritura concurrente posible.
/// A diferencia de <see cref="HierarchicalReductionAggregator"/> (mismo computo independiente
/// por particion), aqui la fusion final es un solo paso secuencial en vez de un arbol binario
/// paralelo: aisla el efecto de "grano grueso + cero contencion" del efecto de la estrategia
/// de reduccion.
/// </summary>
public sealed class CoarseGrainedTaskAggregator : IAggregationStrategy
{
    public string Nombre => "Grano grueso: computo independiente + fusion secuencial";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var particiones = ContiguousRangePartitioner.Partition(registros.Length, maxDegreeOfParallelism);
        var locales = new AggregationResult[particiones.Count];
        var diag = new AggregationDiagnostics();

        Parallel.For(
            0, particiones.Count,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            i =>
            {
                var rango = particiones[i];
                var local = new AggregationResult();
                var mapeo = Stopwatch.StartNew();

                for (var j = rango.Start.Value; j < rango.End.Value; j++)
                    SequentialAggregator.Acumular(local, in registros[j]);

                local.FilasProcesadas = rango.End.Value - rango.Start.Value;
                mapeo.Stop();
                diag.SumarMapeo(mapeo.Elapsed);
                locales[i] = local;
            });

        var reduccion = Stopwatch.StartNew();
        var resultado = new AggregationResult();
        foreach (var local in locales)
            resultado.MergeFrom(local);
        reduccion.Stop();
        diag.SumarReduccion(reduccion.Elapsed);

        resultado.Diagnosticos = diag;
        return resultado;
    }
}
