using VentasParalelo.Core.Aggregation;
using VentasParalelo.Core.DataGeneration;
using VentasParalelo.Core.IO;
using Xunit;

namespace VentasParalelo.Core.Tests;

public class AggregationStrategyTests
{
    private static readonly IAggregationStrategy[] EstrategiasParalelas =
    [
        new LockDictionaryAggregator(),
        new ConcurrentDictionaryAggregator(),
        new LocalReductionAggregator(),
        new RoundRobinReductionAggregator(),
        new DynamicChunkReductionAggregator(),
        new HierarchicalReductionAggregator(),
        new CoarseGrainedTaskAggregator(),
        new PlinqGroupByAggregator()
    ];

    public static IEnumerable<object[]> EstrategiasYHilos()
    {
        foreach (var estrategia in EstrategiasParalelas)
        foreach (var hilos in new[] { 1, 2, 4, 8 })
            yield return [estrategia, hilos];
    }

    [Theory]
    [MemberData(nameof(EstrategiasYHilos))]
    public void Aggregate_ProduceLosMismosTotalesQueElBaseline(IAggregationStrategy estrategia, int hilos)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ventas_agg_test_{Guid.NewGuid():N}.csv");
        try
        {
            SalesDataGenerator.GenerateCsv(path, 5_000);
            var registros = SalesCsvReader.ReadAll(path);

            var esperado = new SequentialAggregator().Aggregate(registros, 1);
            var obtenido = estrategia.Aggregate(registros, hilos);

            Assert.True(
                esperado.TotalesCoinciden(obtenido),
                $"{estrategia.Nombre} con {hilos} hilos no coincide con el baseline secuencial.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static IEnumerable<object[]> EstrategiasConDiagnosticos()
    {
        yield return [new LockDictionaryAggregator()];
        yield return [new LocalReductionAggregator()];
        yield return [new RoundRobinReductionAggregator()];
        yield return [new DynamicChunkReductionAggregator()];
        yield return [new HierarchicalReductionAggregator()];
        yield return [new CoarseGrainedTaskAggregator()];
    }

    [Theory]
    [MemberData(nameof(EstrategiasConDiagnosticos))]
    public void Aggregate_ConMasDeUnHilo_PoblaDiagnosticos(IAggregationStrategy estrategia)
    {
        var registros = SalesDataGenerator.GenerateInMemory(20_000);

        var resultado = estrategia.Aggregate(registros, 4);

        Assert.NotNull(resultado.Diagnosticos);
    }

    [Fact]
    public void PlinqGroupByAggregator_NoPoblaDiagnosticos()
    {
        var registros = SalesDataGenerator.GenerateInMemory(5_000);

        var resultado = new PlinqGroupByAggregator().Aggregate(registros, 4);

        Assert.Null(resultado.Diagnosticos);
    }
}
