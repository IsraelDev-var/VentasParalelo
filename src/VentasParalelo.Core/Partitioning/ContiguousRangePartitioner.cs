namespace VentasParalelo.Core.Partitioning;

/// <summary>
/// Divide un rango [0, length) en <paramref name="partitionCount"/> particiones contiguas
/// de tamano lo mas parejo posible (las primeras `length % partitionCount` particiones
/// reciben una fila extra).
/// </summary>
public static class ContiguousRangePartitioner
{
    public static IReadOnlyList<Range> Partition(int length, int partitionCount)
    {
        if (partitionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(partitionCount));

        partitionCount = Math.Min(partitionCount, Math.Max(length, 1));

        var particiones = new List<Range>(partitionCount);
        var baseSize = length / partitionCount;
        var remainder = length % partitionCount;
        var start = 0;

        for (var i = 0; i < partitionCount; i++)
        {
            var size = baseSize + (i < remainder ? 1 : 0);
            var end = start + size;
            particiones.Add(new Range(start, end));
            start = end;
        }

        return particiones;
    }
}
