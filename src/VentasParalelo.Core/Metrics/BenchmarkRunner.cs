using System.Diagnostics;
using VentasParalelo.Core.Aggregation;
using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.Metrics;

public static class BenchmarkRunner
{
    public static (AggregationResult Resultado, BenchmarkResult Metricas) Ejecutar(
        IAggregationStrategy estrategia, SaleRecord[] registros, int hilos)
    {
        var sw = Stopwatch.StartNew();
        var resultado = estrategia.Aggregate(registros, hilos);
        sw.Stop();

        var metricas = new BenchmarkResult(estrategia.Nombre, registros.Length, hilos, sw.Elapsed);
        return (resultado, metricas);
    }
}
