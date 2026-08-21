namespace VentasParalelo.Core.Metrics;

/// <summary>
/// Desglose opcional del tiempo total de una estrategia de agregacion: cuanto se gasto
/// procesando filas sin sincronizacion ("mapeo"), cuanto fusionando resultados parciales
/// ("reduccion") y cuanto esperando para adquirir un lock compartido ("contencion").
/// No todas las estrategias pueden medir las tres cosas: por ejemplo,
/// <c>ConcurrentDictionary</c> usa locking interno por bucket que no es observable desde
/// afuera, y PLINQ no expone su propio mapeo/reduccion interno. Esos casos dejan el campo
/// correspondiente en <see cref="TimeSpan.Zero"/> y se documentan como "no medible" en el reporte.
/// Los acumuladores son thread-safe: se suman de forma concurrente desde varios hilos
/// mientras corre la agregacion.
/// </summary>
public sealed class AggregationDiagnostics
{
    private long _tiempoMapeoTicks;
    private long _tiempoReduccionTicks;
    private long _tiempoContencionTicks;

    public TimeSpan TiempoMapeo => TimeSpan.FromTicks(Interlocked.Read(ref _tiempoMapeoTicks));
    public TimeSpan TiempoReduccion => TimeSpan.FromTicks(Interlocked.Read(ref _tiempoReduccionTicks));
    public TimeSpan TiempoContencion => TimeSpan.FromTicks(Interlocked.Read(ref _tiempoContencionTicks));

    public void SumarMapeo(TimeSpan transcurrido) => Interlocked.Add(ref _tiempoMapeoTicks, transcurrido.Ticks);
    public void SumarReduccion(TimeSpan transcurrido) => Interlocked.Add(ref _tiempoReduccionTicks, transcurrido.Ticks);
    public void SumarContencion(TimeSpan transcurrido) => Interlocked.Add(ref _tiempoContencionTicks, transcurrido.Ticks);
}
