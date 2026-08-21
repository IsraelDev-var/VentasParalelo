using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Misma agregacion que las demas estrategias, pero expresada de forma declarativa con PLINQ
/// (<c>AsParallel().GroupBy(...)</c>) en vez de particionar y reducir a mano con
/// <c>localInit</c>/<c>localFinally</c>. PLINQ decide internamente como particionar y fusionar;
/// a cambio, no expone ganchos para medir su propio tiempo de mapeo/reduccion (por eso no
/// establece <see cref="AggregationResult.Diagnosticos"/>), a diferencia de
/// <see cref="LocalReductionAggregator"/>.
/// </summary>
public sealed class PlinqGroupByAggregator : IAggregationStrategy
{
    public string Nombre => "PLINQ GroupBy";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var grado = Math.Clamp(maxDegreeOfParallelism, 1, 64);

        var montoPorSucursal = registros
            .AsParallel()
            .WithDegreeOfParallelism(grado)
            .GroupBy(r => r.Sucursal)
            .Select(g => (Clave: g.Key, Total: g.Sum(r => r.Monto)))
            .ToArray();

        var unidadesPorProducto = registros
            .AsParallel()
            .WithDegreeOfParallelism(grado)
            .GroupBy(r => r.Producto)
            .Select(g => (Clave: g.Key, Total: g.Sum(r => (long)r.Cantidad)))
            .ToArray();

        var result = new AggregationResult { FilasProcesadas = registros.Length };
        foreach (var (clave, total) in montoPorSucursal) result.MontoPorSucursal[clave] = total;
        foreach (var (clave, total) in unidadesPorProducto) result.UnidadesPorProducto[clave] = total;
        return result;
    }
}
