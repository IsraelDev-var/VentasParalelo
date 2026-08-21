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

Comparar las estrategias de agregación sobre ese dataset (tiempo, filas/seg, speedup,
eficiencia respecto al baseline secuencial y, cuando la estrategia lo soporta, desglose de
tiempo de mapeo/reducción/contención):

```bash
dotnet run --project src/VentasParalelo.Cli -- comparar --archivo data/ventas_1m.csv --hilos 1,2,4,8
```

Barrido de escalabilidad fuerte (mismo dataset, más hilos) o débil (filas proporcionales a
los hilos), sobre datos generados en memoria (sin pasar por CSV):

```bash
dotnet run --project src/VentasParalelo.Cli -- escalar --tipo fuerte --volumenes 1000000,5000000,20000000 --hilos 1,2,4,8
dotnet run --project src/VentasParalelo.Cli -- escalar --tipo debil --filas-base 250000 --hilos 1,2,4,8
```

Estrategias incluidas:

- **Secuencial (baseline)**: un solo hilo, referencia de correctitud y de speedup/eficiencia.
- **`lock` sobre `Dictionary`**: `Parallel.ForEach` sobre particiones contiguas, pero cada fila
  toma el mismo lock global antes de actualizar los acumuladores compartidos. Mide su propio
  tiempo de contención (espera para adquirir el lock).
- **`ConcurrentDictionary`**: mismo particionado, pero los acumuladores usan locking interno
  más fino (por bucket) en vez de un único lock global. Su contención es interna y no se puede
  medir desde afuera.
- **Acumuladores locales + reducción final**: cada partición contigua acumula en un
  `AggregationResult` propio sin sincronizarse durante el procesamiento
  (`localInit`/`localFinally`); el lock solo se toma una vez por partición, al fusionar el
  resultado local en el compartido. Mide tiempo de mapeo y de reducción.
- **Particionado round-robin**: mismo algoritmo de reducción local que la anterior, pero
  repartiendo filas `i, i+P, i+2P, ...` por partición en vez de bloques contiguos — aísla el
  efecto de la localidad de caché frente al particionado por rangos.
- **Chunking dinámico**: en vez de fijar de antemano qué partición procesa cada hilo, corta el
  arreglo en chunks pequeños tomados dinámicamente de una cola compartida
  (`Partitioner.Create`), balanceando mejor cuando el costo por fila no es uniforme.
- **Reducción jerárquica en árbol**: cada partición contigua se agrega localmente igual que la
  de "acumuladores locales", pero el merge final no es un solo paso serializado bajo un lock:
  los resultados parciales se combinan de a pares en un árbol binario, en paralelo
  (`Parallel.Invoke`), pasando de O(P) locks seriales a O(log P) niveles paralelos.
- **Grano grueso (cómputo independiente + fusión secuencial)**: `P` particiones grandes (una
  por hilo), cada una escribe su resultado local en su propio slot de un arreglo sin ningún
  lock ni estado compartido durante el cómputo — cero dependencia entre hilos. La fusión final
  ocurre en un solo hilo, después de que todas las particiones terminaron, sin sincronización.
  En las mediciones de este proyecto (ver [REPORTE.md](REPORTE.md)) fue la estrategia más
  rápida con 8 hilos.
- **PLINQ `GroupBy`**: misma agregación expresada de forma declarativa
  (`AsParallel().GroupBy(...)`) en vez de particionar y reducir a mano; no expone su propio
  particionado/reducción, así que no mide diagnósticos.

Ver [REPORTE.md](REPORTE.md) para los resultados medidos en detalle (comparación de
estrategias, escalabilidad fuerte/débil de 1M a 20M filas, y desglose de contención).

## Roadmap (entrega: 21 de agosto)

**Semana 1 — fundamentos y primera comparación** ✅
1. Estructura del proyecto, modelo de dominio y generador de datos sintéticos.
2. Agregador secuencial (baseline) y agregador paralelo comparando `lock` + `Dictionary`
   vs. `ConcurrentDictionary`.
3. Acumuladores locales por partición con reducción final (map-reduce) + métricas de
   speedup y eficiencia frente al baseline.

**Semana 2 — estrategias de particionado y reducción** ✅
4. Particionado por rangos contiguos vs. chunking dinámico vs. round-robin.
5. PLINQ con `GroupBy` vs. reducción manual con `localInit`/`localFinally`.
6. Reducción en un solo paso vs. reducción jerárquica en árbol.

**Semana 3 — escalabilidad y reporte final** ✅
7. Barrido de volumen (1M / 5M / 20M filas) y de hilos (1..N), escalabilidad fuerte y débil.
8. Medición de tiempo de contención por estrategia de sincronización y desglose
   mapeo vs. reducción.
9. Consolidación de resultados, gráficos y reporte final — ver [REPORTE.md](REPORTE.md).
