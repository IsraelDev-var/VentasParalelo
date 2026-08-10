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
        new LocalReductionAggregator()
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
}
