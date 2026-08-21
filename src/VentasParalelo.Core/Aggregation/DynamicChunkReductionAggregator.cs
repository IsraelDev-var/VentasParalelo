using System.Collections.Concurrent;
using System.Diagnostics;
using VentasParalelo.Core.Metrics;
using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// En vez de repartir el trabajo por adelantado en <c>P</c> particiones fijas (una por hilo,
/// como <see cref="LocalReductionAggregator"/>), corta el arreglo en muchos chunks pequenos de
/// tamano fijo y los reparte de forma dinamica: cada hilo toma el siguiente chunk disponible de
/// una cola compartida en cuanto termina el anterior (<see cref="Partitioner.Create(int,int,int)"/>).
/// Esto balancea mejor la carga cuando el costo por fila no es uniforme, a costa de mas
/// overhead de coordinacion que una particion estatica.
/// </summary>
public sealed class DynamicChunkReductionAggregator : IAggregationStrategy
{
    private readonly int _tamanoChunk;

    public DynamicChunkReductionAggregator(int tamanoChunk = 4096)
    {
        if (tamanoChunk < 1)
            throw new ArgumentOutOfRangeException(nameof(tamanoChunk));

        _tamanoChunk = tamanoChunk;
    }

    public string Nombre => "Chunking dinamico + acumuladores locales";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var resultado = new AggregationResult();
        var mergeLock = new object();
        var diag = new AggregationDiagnostics();

        var chunkSize = Math.Max(1, Math.Min(_tamanoChunk, registros.Length));
        var particionador = Partitioner.Create(0, registros.Length, chunkSize);

        Parallel.ForEach(
            particionador,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            localInit: () => new AggregationResult(),
            body: (rango, _, local) =>
            {
                var (inicio, fin) = rango;
                var mapeo = Stopwatch.StartNew();
                for (var i = inicio; i < fin; i++)
                    SequentialAggregator.Acumular(local, in registros[i]);

                local.FilasProcesadas += fin - inicio;
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
