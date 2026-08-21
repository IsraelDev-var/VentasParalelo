namespace VentasParalelo.Core.Partitioning;

/// <summary>
/// Particion de indices [0, N) definida por un punto de inicio y un paso fijo entre indices
/// consecutivos, en vez de un rango contiguo. Con <c>Step = 1</c> equivale a un rango contiguo;
/// con <c>Step = numeroDeParticiones</c> modela un reparto round-robin (la particion <c>p</c>
/// recibe los indices <c>p, p + P, p + 2P, ...</c>) sin necesidad de materializar un arreglo
/// de indices completo.
/// </summary>
public readonly record struct StridedRange(int Start, int Step, int Count)
{
    public IEnumerable<int> Indices()
    {
        var indice = Start;
        for (var i = 0; i < Count; i++)
        {
            yield return indice;
            indice += Step;
        }
    }
}
