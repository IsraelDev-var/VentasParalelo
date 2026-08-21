using VentasParalelo.Core.Metrics;

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

    /// <summary>
    /// Desglose de tiempo de mapeo/reduccion/contencion, si la estrategia que produjo este
    /// resultado lo mide. Null si la estrategia no lo soporta (p. ej. PLINQ).
    /// </summary>
    public AggregationDiagnostics? Diagnosticos { get; set; }

    public IEnumerable<(string Producto, long Unidades)> TopProductos(int n) =>
        UnidadesPorProducto
            .OrderByDescending(kv => kv.Value)
            .Take(n)
            .Select(kv => (kv.Key, kv.Value));

    /// <summary>
    /// Compara los totales con otro resultado. Se usa para verificar que todas las
    /// estrategias de agregacion, sin importar como particionan o sincronizan, produzcan
    /// exactamente el mismo resultado sobre el mismo dataset.
    /// </summary>
    public bool TotalesCoinciden(AggregationResult otro)
    {
        if (FilasProcesadas != otro.FilasProcesadas) return false;

        return DiccionariosIguales(MontoPorSucursal, otro.MontoPorSucursal)
            && DiccionariosIguales(UnidadesPorProducto, otro.UnidadesPorProducto);
    }

    /// <summary>
    /// Fusiona un acumulador local (de una particion) dentro de este resultado compartido.
    /// Se usa una vez por particion (en localFinally), no una vez por fila.
    /// </summary>
    public void MergeFrom(AggregationResult otro)
    {
        foreach (var (clave, valor) in otro.MontoPorSucursal)
            MontoPorSucursal[clave] = MontoPorSucursal.GetValueOrDefault(clave) + valor;

        foreach (var (clave, valor) in otro.UnidadesPorProducto)
            UnidadesPorProducto[clave] = UnidadesPorProducto.GetValueOrDefault(clave) + valor;

        FilasProcesadas += otro.FilasProcesadas;
    }

    private static bool DiccionariosIguales<TValue>(
        Dictionary<string, TValue> a, Dictionary<string, TValue> b) where TValue : IEquatable<TValue>
    {
        if (a.Count != b.Count) return false;

        foreach (var (clave, valor) in a)
        {
            if (!b.TryGetValue(clave, out var otroValor) || !valor.Equals(otroValor))
                return false;
        }

        return true;
    }
}
