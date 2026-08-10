using VentasParalelo.Core.Partitioning;
using Xunit;

namespace VentasParalelo.Core.Tests;

public class ContiguousRangePartitionerTests
{
    [Theory]
    [InlineData(100, 4)]
    [InlineData(101, 4)]
    [InlineData(7, 3)]
    [InlineData(3, 8)]
    public void Partition_CoversEveryIndexExactlyOnce(int length, int partitionCount)
    {
        var particiones = ContiguousRangePartitioner.Partition(length, partitionCount);
        var visto = new bool[length];

        foreach (var rango in particiones)
        {
            for (var i = rango.Start.Value; i < rango.End.Value; i++)
            {
                Assert.False(visto[i], $"El indice {i} fue cubierto por mas de una particion.");
                visto[i] = true;
            }
        }

        Assert.All(visto, v => Assert.True(v));
    }

    [Fact]
    public void Partition_WithFewerElementsThanPartitions_DoesNotCreateEmptyOverflow()
    {
        var particiones = ContiguousRangePartitioner.Partition(3, 8);

        Assert.True(particiones.Count <= 3);
        Assert.Equal(3, particiones.Sum(r => r.End.Value - r.Start.Value));
    }
}
