namespace VentasParalelo.Core.Metrics;

public sealed record BenchmarkResult(string Estrategia, long Filas, int Hilos, TimeSpan Duracion)
{
    public double FilasPorSegundo => Filas / Duracion.TotalSeconds;
}
