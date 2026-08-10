using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Agregacion de un solo hilo. Sirve como baseline de correctitud y de referencia
/// para calcular speedup y eficiencia de las estrategias paralelas.
/// </summary>
public sealed class SequentialAggregator : IAggregationStrategy
{
    public string Nombre => "Secuencial (baseline)";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var result = new AggregationResult();

        foreach (var r in registros)
        {
            Acumular(result, in r);
        }

        result.FilasProcesadas = registros.Length;
        return result;
    }

    internal static void Acumular(AggregationResult result, in SaleRecord r)
    {
        result.MontoPorSucursal[r.Sucursal] =
            result.MontoPorSucursal.GetValueOrDefault(r.Sucursal) + r.Monto;

        result.UnidadesPorProducto[r.Producto] =
            result.UnidadesPorProducto.GetValueOrDefault(r.Producto) + r.Cantidad;
    }
}
