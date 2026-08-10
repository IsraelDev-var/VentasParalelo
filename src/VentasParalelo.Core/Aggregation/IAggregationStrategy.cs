using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Estrategia de agregacion de un arreglo de ventas: monto total por sucursal
/// y unidades por producto. Cada implementacion resuelve el acceso a los
/// acumuladores compartidos de una forma distinta.
/// </summary>
public interface IAggregationStrategy
{
    string Nombre { get; }

    AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism);
}
