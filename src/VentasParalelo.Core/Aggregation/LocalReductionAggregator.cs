using VentasParalelo.Core.Models;
using VentasParalelo.Core.Partitioning;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Patron map-reduce "real": cada particion acumula en su propio <see cref="AggregationResult"/>
/// local (sin ninguna sincronizacion mientras procesa sus filas, via localInit/localFinally de
/// <see cref="Parallel.ForEach{TSource,TLocal}(System.Collections.Generic.IEnumerable{TSource},System.Threading.Tasks.ParallelOptions,System.Func{TLocal},System.Func{TSource,System.Threading.Tasks.ParallelLoopState,TLocal,TLocal},System.Action{TLocal})"/>)
/// y solo al terminar la particion completa se toma un lock, una unica vez, para fusionar
/// ese acumulador local en el resultado compartido. El numero de operaciones bajo lock pasa
/// de ser O(filas) -como en <see cref="LockDictionaryAggregator"/>- a O(particiones).
/// </summary>
public sealed class LocalReductionAggregator : IAggregationStrategy
{
    public string Nombre => "Parallel.ForEach + acumuladores locales + reduccion final";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var resultado = new AggregationResult();
        var mergeLock = new object();
        var particiones = ContiguousRangePartitioner.Partition(registros.Length, maxDegreeOfParallelism);

        Parallel.ForEach(
            particiones,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            localInit: () => new AggregationResult(),
            body: (rango, _, local) =>
            {
                for (var i = rango.Start.Value; i < rango.End.Value; i++)
                    SequentialAggregator.Acumular(local, in registros[i]);

                local.FilasProcesadas += rango.End.Value - rango.Start.Value;
                return local;
            },
            localFinally: local =>
            {
                lock (mergeLock)
                {
                    resultado.MergeFrom(local);
                }
            });

        return resultado;
    }
}
