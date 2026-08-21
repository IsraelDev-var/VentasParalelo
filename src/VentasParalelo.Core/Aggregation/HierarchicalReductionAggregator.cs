using System.Diagnostics;
using VentasParalelo.Core.Metrics;
using VentasParalelo.Core.Models;
using VentasParalelo.Core.Partitioning;

namespace VentasParalelo.Core.Aggregation;

/// <summary>
/// Cada particion contigua se agrega en su propio <see cref="AggregationResult"/> local, igual
/// que <see cref="LocalReductionAggregator"/>, pero la fusion final no es un solo paso serializado
/// bajo un lock compartido: los resultados locales se combinan de a pares en un arbol binario,
/// haciendo cada nivel del arbol en paralelo con <see cref="Parallel.Invoke(Action[])"/>. La
/// profundidad de la fusion pasa de O(P) locks seriales a O(log P) niveles paralelos.
/// </summary>
public sealed class HierarchicalReductionAggregator : IAggregationStrategy
{
    public string Nombre => "Particiones locales + reduccion jerarquica en arbol";

    public AggregationResult Aggregate(SaleRecord[] registros, int maxDegreeOfParallelism)
    {
        var particiones = ContiguousRangePartitioner.Partition(registros.Length, maxDegreeOfParallelism);
        var locales = new AggregationResult[particiones.Count];
        var diag = new AggregationDiagnostics();

        Parallel.For(
            0, particiones.Count,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            i =>
            {
                var rango = particiones[i];
                var local = new AggregationResult();

                var mapeo = Stopwatch.StartNew();
                for (var j = rango.Start.Value; j < rango.End.Value; j++)
                    SequentialAggregator.Acumular(local, in registros[j]);

                local.FilasProcesadas = rango.End.Value - rango.Start.Value;
                mapeo.Stop();
                diag.SumarMapeo(mapeo.Elapsed);
                locales[i] = local;
            });

        var resultado = ReducirEnArbol(locales, 0, locales.Length, diag);
        resultado.Diagnosticos = diag;
        return resultado;
    }

    private static AggregationResult ReducirEnArbol(
        AggregationResult[] locales, int inicio, int fin, AggregationDiagnostics diag)
    {
        var cantidad = fin - inicio;
        if (cantidad == 1)
            return locales[inicio];

        var medio = inicio + cantidad / 2;

        AggregationResult izquierda = null!;
        AggregationResult derecha = null!;
        Parallel.Invoke(
            () => izquierda = ReducirEnArbol(locales, inicio, medio, diag),
            () => derecha = ReducirEnArbol(locales, medio, fin, diag));

        var reduccion = Stopwatch.StartNew();
        izquierda.MergeFrom(derecha);
        reduccion.Stop();
        diag.SumarReduccion(reduccion.Elapsed);

        return izquierda;
    }
}
