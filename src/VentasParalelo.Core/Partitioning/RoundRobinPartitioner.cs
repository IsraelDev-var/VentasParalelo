namespace VentasParalelo.Core.Partitioning;

/// <summary>
/// Reparte [0, length) en <paramref name="partitionCount"/> particiones round-robin: la
/// particion <c>p</c> recibe los indices <c>p, p + P, p + 2P, ...</c> en vez de un bloque
/// contiguo. El balance de filas por particion es igual de parejo que
/// <see cref="ContiguousRangePartitioner"/>, pero cada hilo salta por el arreglo con paso
/// <c>P</c> en vez de recorrerlo secuencialmente, lo que castiga la localidad de cache
/// (util para comparar el efecto de la estrategia de particionado, no solo del balance de carga).
/// </summary>
public static class RoundRobinPartitioner
{
    public static IReadOnlyList<StridedRange> Partition(int length, int partitionCount)
    {
        if (partitionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(partitionCount));

        partitionCount = Math.Min(partitionCount, Math.Max(length, 1));

        var particiones = new List<StridedRange>(partitionCount);
        var baseSize = length / partitionCount;
        var remainder = length % partitionCount;

        for (var p = 0; p < partitionCount; p++)
        {
            var count = baseSize + (p < remainder ? 1 : 0);
            particiones.Add(new StridedRange(p, partitionCount, count));
        }

        return particiones;
    }
}
