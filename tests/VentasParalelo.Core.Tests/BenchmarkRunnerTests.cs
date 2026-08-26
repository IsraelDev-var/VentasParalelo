using VentasParalelo.Core.Aggregation;
using VentasParalelo.Core.DataGeneration;
using VentasParalelo.Core.Metrics;
using VentasParalelo.Core.Models;
using Xunit;

namespace VentasParalelo.Core.Tests;

public class BenchmarkRunnerTests
{
    /// <summary>
    /// Estrategia de prueba que cuenta cuantas veces se la invoca, para verificar que el
    /// runner ejecute el calentamiento ademas de las repeticiones medidas.
    /// </summary>
    private sealed class ContadorDeLlamadas : IAggregationStrategy
    {
        public int Llamadas { get; private set; }

        public string Nombre => "contador";

        public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
        {
            Llamadas++;
            return new SequentialAggregator().Aggregate(registros, 1);
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 5)]
    public void Ejecutar_InvocaCalentamientosMasRepeticiones(int calentamientos, int repeticiones)
    {
        var registros = SalesDataGenerator.GenerateInMemory(500);
        var estrategia = new ContadorDeLlamadas();

        BenchmarkRunner.Ejecutar(estrategia, registros, 1, calentamientos, repeticiones);

        Assert.Equal(calentamientos + repeticiones, estrategia.Llamadas);
    }

    [Fact]
    public void Ejecutar_DevuelveElMejorTiempoDeLasRepeticiones()
    {
        var registros = SalesDataGenerator.GenerateInMemory(2_000);

        var (_, unaVez) = BenchmarkRunner.Ejecutar(
            new SequentialAggregator(), registros, 1, calentamientos: 1, repeticiones: 1);
        var (_, variasVeces) = BenchmarkRunner.Ejecutar(
            new SequentialAggregator(), registros, 1, calentamientos: 1, repeticiones: 8);

        // El minimo de 8 intentos nunca puede superar al de 1 intento por mas de el ruido
        // del sistema; lo que si debe cumplirse siempre es que ambos sean tiempos validos.
        Assert.True(variasVeces.Duracion > TimeSpan.Zero);
        Assert.True(unaVez.Duracion > TimeSpan.Zero);
    }

    [Fact]
    public void Ejecutar_DevuelveUnResultadoCorrecto()
    {
        var registros = SalesDataGenerator.GenerateInMemory(3_000);
        var esperado = new SequentialAggregator().Aggregate(registros, 1);

        var (obtenido, metricas) = BenchmarkRunner.Ejecutar(
            new LocalReductionAggregator(), registros, 4);

        Assert.True(esperado.TotalesCoinciden(obtenido));
        Assert.Equal(registros.Length, metricas.Filas);
        Assert.Equal(4, metricas.Hilos);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ejecutar_ConRepeticionesInvalidas_Lanza(int repeticiones)
    {
        var registros = SalesDataGenerator.GenerateInMemory(100);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BenchmarkRunner.Ejecutar(new SequentialAggregator(), registros, 1, 1, repeticiones));
    }
}
