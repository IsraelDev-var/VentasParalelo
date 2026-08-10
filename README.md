# VentasParalelo

Motor de agregación analítica sobre un dataset masivo de ventas (transacciones de punto de
venta), comparando estrategias de programación paralela en .NET. Simula el procesamiento
nocturno (batch) de datos de venta de una cadena de tiendas y la reportería de un data
warehouse.

## Idea general

Un CSV de millones de transacciones (`fecha,sucursal,producto,cantidad,monto`) se divide en
particiones de registros. Cada hilo ejecuta el mismo parseo y la misma agregación sobre su
porción (monto total por sucursal, unidades por producto, top-N productos); al final los
resultados parciales se fusionan (patrón map-reduce / reduction).

El objetivo del proyecto es **comparar en vivo, con números**, distintas formas de resolver el
mismo problema paralelo:

- Ejecución simultánea con `Parallel.ForEach` / PLINQ sobre N particiones.
- Acceso a datos compartidos: `lock` sobre `Dictionary` vs. `ConcurrentDictionary` vs.
  acumuladores locales por partición con reducción final.
- Estrategias de particionado: rangos contiguos vs. chunking dinámico vs. round-robin.
- PLINQ con `GroupBy` vs. reducción manual con `localInit`/`localFinally`.
- Reducción en un solo paso vs. reducción jerárquica en árbol.
- Escalabilidad fuerte (mismo dataset, más hilos) y débil (más datos y más hilos
  proporcionalmente), en volumen (1M → 5M → 20M filas) y en hilos (1 a N).
- Métricas: filas/segundo, speedup, eficiencia, tiempo perdido en contención por estrategia
  de sincronización, tiempo de mapeo vs. tiempo de reducción.

## Estructura del repo

```
src/
  VentasParalelo.Core/   Modelos de dominio, generador de datos, lector CSV, estrategias de agregación
  VentasParalelo.Cli/    Punto de entrada: genera datasets y ejecuta los benchmarks
tests/
  VentasParalelo.Core.Tests/
```

## Uso

Generar un dataset sintético de ventas:

```bash
dotnet run --project src/VentasParalelo.Cli -- generar --filas 1000000 --salida data/ventas_1m.csv
```

Comparar las estrategias de agregación sobre ese dataset (tiempo, filas/seg, speedup y
eficiencia respecto al baseline secuencial):

```bash
dotnet run --project src/VentasParalelo.Cli -- comparar --archivo data/ventas_1m.csv --hilos 1,2,4,8
```

Estrategias incluidas hasta ahora:

- **Secuencial (baseline)**: un solo hilo, referencia de correctitud y de speedup/eficiencia.
- **`lock` sobre `Dictionary`**: `Parallel.ForEach` sobre particiones contiguas, pero cada fila
  toma el mismo lock global antes de actualizar los acumuladores compartidos.
- **`ConcurrentDictionary`**: mismo particionado, pero los acumuladores usan locking interno
  más fino (por bucket) en vez de un único lock global.
- **Acumuladores locales + reducción final**: cada partición acumula en un `AggregationResult`
  propio sin sincronizarse durante el procesamiento (`localInit`/`localFinally`); el lock solo
  se toma una vez por partición, al fusionar el resultado local en el compartido.

## Roadmap (entrega: 21 de agosto)

**Semana 1 — fundamentos y primera comparación**
1. Estructura del proyecto, modelo de dominio y generador de datos sintéticos.
2. Agregador secuencial (baseline) y agregador paralelo comparando `lock` + `Dictionary`
   vs. `ConcurrentDictionary`.
3. Acumuladores locales por partición con reducción final (map-reduce) + métricas de
   speedup y eficiencia frente al baseline.

**Semana 2 — estrategias de particionado y reducción**
4. Particionado por rangos contiguos vs. chunking dinámico vs. round-robin.
5. PLINQ con `GroupBy` vs. reducción manual con `localInit`/`localFinally`.
6. Reducción en un solo paso vs. reducción jerárquica en árbol.

**Semana 3 — escalabilidad y reporte final**
7. Barrido de volumen (1M / 5M / 20M filas) y de hilos (1..N), escalabilidad fuerte y débil.
8. Medición de tiempo de contención por estrategia de sincronización y desglose
   mapeo vs. reducción.
9. Consolidación de resultados, gráficos y reporte final.
