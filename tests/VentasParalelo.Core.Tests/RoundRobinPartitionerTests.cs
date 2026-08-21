using VentasParalelo.Core.Partitioning;
using Xunit;

namespace VentasParalelo.Core.Tests;

public class RoundRobinPartitionerTests
{
    [Theory]
    [InlineData(100, 4)]
    [InlineData(101, 4)]
    [InlineData(7, 3)]
    [InlineData(3, 8)]
    public void Partition_CoversEveryIndexExactlyOnce(int length, int partitionCount)
    {
        var particiones = RoundRobinPartitioner.Partition(length, partitionCount);
        var visto = new bool[length];

        foreach (var particion in particiones)
        {
            foreach (var i in particion.Indices())
            {
                Assert.False(visto[i], $"El indice {i} fue cubierto por mas de una particion.");
                visto[i] = true;
            }
        }

        Assert.All(visto, v => Assert.True(v));
    }

    [Fact]
    public void Partition_AssignsIndicesWithStrideEqualToPartitionCount()
    {
        var particiones = RoundRobinPartitioner.Partition(10, 3);

        Assert.Equal([0, 3, 6, 9], particiones[0].Indices());
        Assert.Equal([1, 4, 7], particiones[1].Indices());
        Assert.Equal([2, 5, 8], particiones[2].Indices());
    }

    [Fact]
    public void Partition_WithFewerElementsThanPartitions_DoesNotCreateEmptyOverflow()
    {
        var particiones = RoundRobinPartitioner.Partition(3, 8);

        Assert.True(particiones.Count <= 3);
        Assert.Equal(3, particiones.Sum(p => p.Count));
    }
}
