using System.Diagnostics;
using VentasParalelo.Core.Metrics;
using VentasParalelo.Core.Models;
using VentasParalelo.Core.Partitioning;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Mismo patron de acumuladores locales + reduccion final que <see cref="LocalReductionAggregator"/>,
/// pero particionando con <see cref="RoundRobinPartitioner"/> en vez de rangos contiguos. Sirve
/// para aislar el efecto de la estrategia de particionado (localidad de cache) del algoritmo de
/// reduccion, que es identico en ambas clases.
/// </summary>
public sealed class RoundRobinReductionAggregator : IAggregationStrategy
{
    public string Nombre => "Particionado round-robin + acumuladores locales";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var resultado = new AggregationResult();
        var mergeLock = new object();
        var diag = new AggregationDiagnostics();
        var particiones = RoundRobinPartitioner.Partition(registros.Length, maxDegreeOfParallelism);

        Parallel.ForEach(
            particiones,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            localInit: () => new AggregationResult(),
            body: (particion, _, local) =>
            {
                var mapeo = Stopwatch.StartNew();
                foreach (var i in particion.Indices())
                    SequentialAggregator.Acumular(local, in registros[i]);

                local.FilasProcesadas += particion.Count;
                mapeo.Stop();
                diag.SumarMapeo(mapeo.Elapsed);
                return local;
            },
            localFinally: local =>
            {
                var reduccion = Stopwatch.StartNew();
                lock (mergeLock)
                {
                    resultado.MergeFrom(local);
                }
                reduccion.Stop();
                diag.SumarReduccion(reduccion.Elapsed);
            });

        resultado.Diagnosticos = diag;
        return resultado;
    }
}
