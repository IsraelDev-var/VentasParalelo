namespace VentasParalelo.Core.Models;

/// <summary>
/// Resultado de agregar un conjunto de <see cref="SaleRecord"/>: monto total por sucursal
/// y unidades vendidas por producto. El "top-N" se deriva de UnidadesPorProducto en el reporte final.
/// </summary>
public sealed class AggregationResult
{
    public Dictionary<string, decimal> MontoPorSucursal { get; } = new();
    public Dictionary<string, long> UnidadesPorProducto { get; } = new();
    public long FilasProcesadas { get; set; }

    public IEnumerable<(string Producto, long Unidades)> TopProductos(int n) =>
        UnidadesPorProducto
            .OrderByDescending(kv => kv.Value)
            .Take(n)
            .Select(kv => (kv.Key, kv.Value));
}
