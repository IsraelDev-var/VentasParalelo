using VentasParalelo.Core.Models;
using VentasParalelo.Core.Partitioning;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Particiona el arreglo en rangos contiguos (uno por tarea de <see cref="Parallel.ForEach{TSource}(System.Collections.Generic.IEnumerable{TSource}, System.Action{TSource})"/>)
/// pero todas las tareas comparten un unico <see cref="AggregationResult"/> protegido por
/// un solo lock global. Es el caso "malo" a proposito: cada fila procesada compite por el
/// mismo candado, por lo que el paralelismo se degrada a serializacion con overhead extra.
/// </summary>
public sealed class LockDictionaryAggregator : IAggregationStrategy
{
    public string Nombre => "Parallel.ForEach + lock sobre Dictionary";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var result = new AggregationResult();
        var sync = new object();
        var particiones = ContiguousRangePartitioner.Partition(registros.Length, maxDegreeOfParallelism);

        Parallel.ForEach(
            particiones,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            rango =>
            {
                for (var i = rango.Start.Value; i < rango.End.Value; i++)
                {
                    var r = registros[i];
                    lock (sync)
                    {
                        SequentialAggregator.Acumular(result, in r);
                    }
                }
            });

        result.FilasProcesadas = registros.Length;
        return result;
    }
}
