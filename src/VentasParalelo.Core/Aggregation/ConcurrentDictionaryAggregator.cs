using System.Collections.Concurrent;
using VentasParalelo.Core.Models;
using VentasParalelo.Core.Partitioning;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Misma particion por rangos contiguos que <see cref="LockDictionaryAggregator"/>, pero
/// los acumuladores compartidos son <see cref="ConcurrentDictionary{TKey,TValue}"/>, que
/// usa bloqueos internos por "bucket" (striped locking) en vez de un unico lock global.
/// Menos contencion que <see cref="LockDictionaryAggregator"/>, pero sigue pagando el costo
/// de sincronizar en cada fila (AddOrUpdate por elemento).
/// </summary>
public sealed class ConcurrentDictionaryAggregator : IAggregationStrategy
{
    public string Nombre => "Parallel.ForEach + ConcurrentDictionary";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var montoPorSucursal = new ConcurrentDictionary<string, decimal>();
        var unidadesPorProducto = new ConcurrentDictionary<string, long>();
        var particiones = ContiguousRangePartitioner.Partition(registros.Length, maxDegreeOfParallelism);

        Parallel.ForEach(
            particiones,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            rango =>
            {
                for (var i = rango.Start.Value; i < rango.End.Value; i++)
                {
                    var r = registros[i];
                    montoPorSucursal.AddOrUpdate(r.Sucursal, r.Monto, (_, actual) => actual + r.Monto);
                    unidadesPorProducto.AddOrUpdate(r.Producto, r.Cantidad, (_, actual) => actual + r.Cantidad);
                }
            });

        var result = new AggregationResult { FilasProcesadas = registros.Length };
        foreach (var kv in montoPorSucursal) result.MontoPorSucursal[kv.Key] = kv.Value;
        foreach (var kv in unidadesPorProducto) result.UnidadesPorProducto[kv.Key] = kv.Value;
        return result;
    }
}
