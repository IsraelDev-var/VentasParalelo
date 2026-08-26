using System.Diagnostics;
using VentasParalelo.Core.Aggregation;
using VentasParalelo.Core.Models;

namespace VentasParalelo.Core.Metrics;

public static class BenchmarkRunner
{
    public const int CalentamientosPorDefecto = 1;
    public const int RepeticionesPorDefecto = 3;

    /// <summary>
    /// Cronometra una estrategia descartando primero una o mas ejecuciones de calentamiento y
    /// quedandose despues con el MEJOR tiempo de varias repeticiones.
    /// </summary>
    /// <remarks>
    /// El calentamiento no es un lujo: la primera ejecucion de un proceso paga la compilacion
    /// JIT, el crecimiento inicial de los diccionarios y las caches frias, y midio ~50% mas
    /// lento que las siguientes sobre el mismo dato y el mismo codigo. Sin descartarla, la
    /// primera estrategia de la tabla (el baseline secuencial) quedaba penalizada e inflaba el
    /// speedup de todas las demas, hasta producir eficiencias imposibles por encima del 100%
    /// con un solo hilo.
    ///
    /// Entre repeticiones se toma el minimo y no el promedio porque el ruido de una maquina de
    /// uso general solo puede hacer una medicion mas lenta, nunca mas rapida: el minimo es la
    /// muestra menos contaminada.
    /// </remarks>
    public static (AggregationResult Resultado, BenchmarkResult Metricas) Ejecutar(
        IAggregationStrategy estrategia,
        SaleRecord[] registros,
        int hilos,
        int calentamientos = CalentamientosPorDefecto,
        int repeticiones = RepeticionesPorDefecto)
    {
        if (repeticiones < 1)
            throw new ArgumentOutOfRangeException(nameof(repeticiones));

        for (var i = 0; i < calentamientos; i++)
            estrategia.Aggregate(registros, hilos);

        var mejorDuracion = TimeSpan.MaxValue;
        AggregationResult? mejorResultado = null;

        for (var i = 0; i < repeticiones; i++)
        {
            var sw = Stopwatch.StartNew();
            var resultado = estrategia.Aggregate(registros, hilos);
            sw.Stop();

            if (sw.Elapsed >= mejorDuracion)
                continue;

            mejorDuracion = sw.Elapsed;
            mejorResultado = resultado;
        }

        var metricas = new BenchmarkResult(estrategia.Nombre, registros.Length, hilos, mejorDuracion);
        return (mejorResultado!, metricas);
    }
}
