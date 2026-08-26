# Reporte final — VentasParalelo

Todas las mediciones se corrieron en modo `Release` sobre la misma maquina
(`Environment.ProcessorCount` = 8 hilos logicos), con los comandos `comparar` y `escalar` del
CLI, contra datasets generados con `SalesDataGenerator` (semilla fija, reproducibles).

## 0. Correccion de la metodologia de medicion

> **Los numeros de la primera version de este reporte estaban inflados y fueron reemplazados.**

La version anterior reportaba eficiencias imposibles: hasta **187 % con 2 hilos** y **172 % con
1 solo hilo**. Una eficiencia superior al 100 % con un unico hilo no tiene interpretacion fisica
posible —una estrategia paralela con 1 hilo hace el mismo trabajo que la secuencial mas el
overhead de particionar y fusionar, asi que nunca puede ser mas rapida—, y esa cifra fue la
pista de que el problema estaba en la medicion, no en el codigo medido.

**La causa.** Las formulas siempre fueron correctas (`speedup = T1/Tp`, `eficiencia = speedup/p`).
El problema era el valor de `T1`: se tomaba de **una unica ejecucion**, que ademas resultaba ser
**la primera del proceso**. .NET compila por niveles (*tiered compilation*): el codigo caliente
se promueve a su version optimizada recien despues de varias ejecuciones. Como las nueve
estrategias comparten el mismo metodo de acumulacion, la primera de la tabla —el baseline
secuencial— corria con codigo aun sin optimizar y quedaba penalizada, inflando el speedup de
todas las demas.

Midiendo el agregador secuencial diez veces seguidas sobre el mismo dato y el mismo codigo:

```
#1   0.1622s   <-- la unica que medía la version anterior
#2   0.1082s
#3   0.1133s
...
#10  0.1092s
```

La primera ejecucion es **50 % mas lenta** que las siguientes, que entre si son estables.

**Las correcciones aplicadas:**

1. **Calentamiento global** (`Program.cs`): se ejecutan todas las estrategias una vez antes de
   medir cualquiera, de modo que el orden dentro de la tabla deje de influir en el resultado.
2. **Repeticiones** (`BenchmarkRunner`): cada medicion descarta una ejecucion de calentamiento y
   reporta el **mejor** de tres tiempos. Se usa el minimo y no el promedio porque el ruido de una
   maquina de uso general solo puede hacer una medicion mas lenta, nunca mas rapida.
3. **Baseline correcto en `escalar`**: antes se tomaba como referencia el primer valor de
   `--hilos`. Si ese valor no era 1 —por ejemplo `--hilos 2,4,8`— el speedup quedaba referido a
   2 hilos mientras la eficiencia seguia dividiendo entre el numero absoluto, y la tabla mostraba
   `hilos=2 speedup=1.00 eficiencia=50 %` bajo un encabezado que decia "respecto a 1 hilo". Ahora
   el baseline se mide siempre con 1 hilo, este o no en la lista pedida.

**Efecto de la correccion.** Con 1 hilo, las cinco estrategias sin contencion ahora miden entre
**0.93 y 1.02 de speedup** (antes: 1.32 a 1.72), que es exactamente lo que predice la teoria. Las
conclusiones cualitativas del proyecto no cambiaron —el ranking entre estrategias se mantiene,
porque todas se dividian entre el mismo baseline erroneo—, pero **los valores absolutos si**: la
eficiencia real con 8 hilos ronda el 40 %, no el 85 % que se reportaba antes.

### Conclusion retirada: la supuesta mejora por Server GC

La version anterior de este reporte afirmaba que activar **Server GC** mejoraba la eficiencia
entre 6 y 32 puntos porcentuales, y lo presentaba como evidencia de que el recolector de basura
era un punto de sincronizacion oculto entre hilos. **Esa conclusion era falsa**: salia de comparar
mediciones tomadas con la metodologia defectuosa descrita arriba.

Repitiendo la comparacion con el metodo corregido, sobre 5 millones de filas y 8 hilos, ejecutando
ambas configuraciones una a continuacion de la otra (`DOTNET_gcServer=0` y `=1`):

| Estrategia | Workstation GC | Server GC |
|---|---:|---:|
| Acumuladores locales | 39 % | 35 % |
| Round-robin | 29 % | 29 % |
| Chunking dinamico | 42 % | 41 % |
| Reduccion jerarquica en arbol | 43 % | 38 % |
| Grano grueso | 39 % | 40 % |

Las diferencias son mas chicas que la variacion entre corridas de una misma configuracion: en
este banco de pruebas **Server GC no produce una mejora medible**. La explicacion es coherente con
el resto de los datos: los acumuladores de este proyecto son diminutos (8 sucursales y 12
productos), de modo que la presion sobre el heap es minima y el recolector casi no interviene.

La opcion se dejo activada en `VentasParalelo.Cli.csproj` porque es la recomendada para cargas
paralelas en general, pero **no se le atribuye ninguna mejora medida en este proyecto**.

## 1. Estrategias comparadas (5,000,000 filas, hilos 1/2/4/8)

Corrida representativa de tres ejecuciones consistentes entre si (la eficiencia con 8 hilos vario
a lo sumo 3 puntos entre corridas).

| Estrategia | Hilos | Tiempo (s) | Filas/seg | Speedup | Eficiencia |
|---|---:|---:|---:|---:|---:|
| Secuencial (baseline) | 1 | 0.779 | 6,414,689 | 1.00 | 100 % |
| lock sobre Dictionary | 1 | 1.403 | 3,564,500 | 0.56 | 56 % |
| lock sobre Dictionary | 2 | 1.475 | 3,390,184 | 0.53 | 26 % |
| lock sobre Dictionary | 4 | 1.476 | 3,386,594 | 0.53 | 13 % |
| lock sobre Dictionary | 8 | 1.517 | 3,296,608 | 0.51 | **6 %** |
| ConcurrentDictionary | 1 | 1.505 | 3,322,173 | 0.52 | 52 % |
| ConcurrentDictionary | 2 | 1.291 | 3,872,199 | 0.60 | 30 % |
| ConcurrentDictionary | 4 | 1.174 | 4,259,324 | 0.66 | 17 % |
| ConcurrentDictionary | 8 | 0.942 | 5,306,212 | 0.83 | **10 %** |
| Acumuladores locales | 1 | 0.780 | 6,413,080 | 1.00 | 100 % |
| Acumuladores locales | 2 | 0.385 | 12,992,527 | 2.03 | 101 % |
| Acumuladores locales | 4 | 0.254 | 19,666,944 | 3.07 | 77 % |
| Acumuladores locales | 8 | 0.240 | 20,800,443 | 3.24 | **41 %** |
| Round-robin | 1 | 0.778 | 6,427,593 | 1.00 | 100 % |
| Round-robin | 2 | 0.412 | 12,142,557 | 1.89 | 95 % |
| Round-robin | 4 | 0.313 | 15,969,624 | 2.49 | 62 % |
| Round-robin | 8 | 0.308 | 16,250,085 | 2.53 | **32 %** |
| Chunking dinamico | 1 | 0.765 | 6,535,795 | 1.02 | 102 % |
| Chunking dinamico | 2 | 0.385 | 12,982,107 | 2.02 | 101 % |
| Chunking dinamico | 4 | 0.259 | 19,311,819 | 3.01 | 75 % |
| Chunking dinamico | 8 | 0.230 | 21,718,696 | 3.39 | **42 %** |
| Reduccion jerarquica en arbol | 1 | 0.792 | 6,315,838 | 0.98 | 98 % |
| Reduccion jerarquica en arbol | 2 | 0.395 | 12,644,853 | 1.97 | 99 % |
| Reduccion jerarquica en arbol | 4 | 0.262 | 19,101,248 | 2.98 | 74 % |
| Reduccion jerarquica en arbol | 8 | 0.240 | 20,844,633 | 3.25 | **41 %** |
| Grano grueso | 1 | 0.761 | 6,573,185 | 1.02 | 102 % |
| Grano grueso | 2 | 0.378 | 13,233,139 | 2.06 | 103 % |
| Grano grueso | 4 | 0.278 | 18,017,460 | 2.81 | 70 % |
| Grano grueso | 8 | 0.253 | 19,738,684 | 3.08 | **38 %** |
| PLINQ GroupBy | 1 | 1.744 | 2,866,627 | 0.45 | 45 % |
| PLINQ GroupBy | 2 | 1.740 | 2,873,884 | 0.45 | 22 % |
| PLINQ GroupBy | 4 | 1.491 | 3,352,641 | 0.52 | 13 % |
| PLINQ GroupBy | 8 | 1.299 | 3,849,484 | 0.60 | **8 %** |

Todas las estrategias reproducen exactamente los mismos totales que el baseline secuencial
(ninguna fila quedo marcada con `(!)`), verificado ademas por las 61 pruebas unitarias.

### Lecturas clave

- **Sincronizar por fila destruye el paralelismo.** Con 8 hilos, `lock` sobre `Dictionary` rinde
  **6 %** de eficiencia y `ConcurrentDictionary` **10 %** — ambas mas lentas que un solo hilo
  secuencial. El diagnostico de contencion lo confirma en numeros: el tiempo acumulado esperando
  el candado crece de 0.24 s con 1 hilo a **10.64 s con 8 hilos**, mas de lo que dura toda la
  corrida (son ocho hilos esperando en simultaneo).
- **Sincronizar por particion lo aprovecha.** Las cinco estrategias de acumuladores locales
  llegan a **38–42 %** de eficiencia con 8 hilos: entre cuatro y siete veces mejor que la familia
  anterior, con el mismo numero de hilos y el mismo trabajo util.
- **Escalado por numero de hilos.** La curva de las estrategias buenas es limpia y muy
  reproducible: **100 % con 1 hilo, ~100 % con 2, ~75 % con 4 y ~41 % con 8**. El quiebre entre 4
  y 8 hilos es coherente con una maquina de 4 nucleos fisicos con hyperthreading: hasta 4 hilos
  hay hardware real que repartir, a partir de ahi los hilos logicos compiten por las mismas
  unidades de ejecucion.
- **Round-robin es la mas debil del grupo bueno** (32 % contra 38–42 %), de forma consistente en
  las tres corridas. Como el algoritmo de acumulacion es identico al de los acumuladores locales
  y solo cambia el particionado, la diferencia aisla un efecto puro de localidad de cache.
- **No hay ganador claro entre las cuatro mejores.** Acumuladores locales (41 %), chunking
  dinamico (42 %), arbol (41 %) y grano grueso (38 %) quedan dentro del margen de variacion entre
  corridas. Con la precision de este banco de pruebas no se puede afirmar cual es la mejor.
- **PLINQ es el codigo mas corto y el que peor rinde** (8 %). Ademas de no controlar el
  particionado, recorre el arreglo dos veces —una por cada agrupacion—, mientras que las demas
  estrategias calculan ambos acumuladores en una sola pasada.

## 2. Escalabilidad fuerte — `escalar --tipo fuerte`

Estrategia: acumuladores locales + reduccion final.

```
== 1,000,000 filas ==
  hilos=1   tiempo=0.078s  speedup=1.00  eficiencia=100 %
  hilos=2   tiempo=0.039s  speedup=1.98  eficiencia= 99 %
  hilos=4   tiempo=0.020s  speedup=3.81  eficiencia= 95 %
  hilos=8   tiempo=0.011s  speedup=6.86  eficiencia= 86 %

== 20,000,000 filas ==
  hilos=1   tiempo=2.936s  speedup=1.00  eficiencia=100 %
  hilos=2   tiempo=1.330s  speedup=2.21  eficiencia=110 %
  hilos=4   tiempo=0.927s  speedup=3.17  eficiencia= 79 %
  hilos=8   tiempo=0.834s  speedup=3.52  eficiencia= 44 %
```

El contraste entre ambos volumenes es el hallazgo interesante: con 1 millon de filas el escalado
es casi perfecto hasta 8 hilos (86 %), mientras que con 20 millones se estanca en 44 %. La
explicacion es que el dataset chico, repartido entre los hilos, cabe en la cache del procesador;
el grande no, y el cuello de botella pasa a ser el ancho de banda de memoria, que es un recurso
compartido y no se multiplica al agregar hilos.

> **Nota sobre `escalar` frente a `comparar`.** Los tiempos de los dos comandos **no son
> comparables entre si**. `escalar` genera los datos en memoria reutilizando las mismas
> referencias de string del catalogo, mientras que `comparar` las parsea del CSV creando
> instancias nuevas; la comparacion de claves en el diccionario es mucho mas barata en el primer
> caso. Sobre 5 millones de filas y 1 hilo, la misma estrategia mide 0.276 s por un camino y
> 0.780 s por el otro. Cada comando es internamente consistente; mezclar sus cifras no.

## 3. Escalabilidad debil — `escalar --tipo debil`

Estrategia: acumuladores locales + reduccion final. Filas base: 250,000 por hilo.

El tiempo se mantiene aproximadamente constante aunque el volumen de datos crece en proporcion
al numero de hilos: es la definicion de buena escalabilidad debil, y la propiedad que importa
para un job batch nocturno que debe absorber datasets cada vez mas grandes agregando hardware.

## 4. Desglose de mapeo, reduccion y contencion

| Estrategia | Contencion medible | Que domina el tiempo |
|---|---|---|
| `lock` sobre `Dictionary` | Si — crece de 0.24 s (1 hilo) a 10.64 s (8 hilos) | Contencion por fila |
| `ConcurrentDictionary` | No (locking interno, opaco) | — |
| Acumuladores locales y variantes | Contencion nula; reduccion medible pero despreciable | Mapeo (>99 % del tiempo) |
| PLINQ GroupBy | No (no expone sus fases internas) | — |

Esto confirma el objetivo central del proyecto: mover la sincronizacion de "una vez por fila" a
"una vez por particion" elimina la contencion como factor relevante y deja el mapeo —el trabajo
util— como el termino dominante.

## 5. Conclusiones

1. **El cuello de botella es la sincronizacion, no el paralelismo.** Sincronizar en cada fila da
   6–10 % de eficiencia; hacerlo una vez por particion da 38–42 %. Mismo hardware, mismos hilos,
   mismo trabajo util.
2. **El techo real lo pone el hardware.** Una vez eliminada la sincronizacion por fila, las cinco
   variantes rinden practicamente igual y todas se estancan alrededor del 40 % con 8 hilos. Ese
   limite no es del algoritmo: son 4 nucleos fisicos y, en datasets grandes, el ancho de banda de
   memoria.
3. **El particionado importa, pero menos que la sincronizacion.** Round-robin pierde unos 10
   puntos frente a las demas por localidad de cache — un efecto real y repetible, pero de segundo
   orden frente a la diferencia entre las dos familias.
4. **La abstraccion se paga.** PLINQ es el codigo mas legible del proyecto y el de peor
   rendimiento, y al no exponer sus fases internas tampoco permite diagnosticar por que.
5. **Una medicion sin repetir no es un resultado.** Este reporte tuvo que corregirse porque su
   primera version tomaba una unica muestra, en frio, como referencia de todo lo demas. Las
   eficiencias imposibles por encima del 100 % fueron la senal de alarma; sin repetir las
   mediciones no habrian aparecido nunca.

## Como reproducir estos numeros

```bash
dotnet run --project src/VentasParalelo.Cli -c Release -- generar --filas 5000000 --salida data/ventas_5m.csv
dotnet run --project src/VentasParalelo.Cli -c Release -- comparar --archivo data/ventas_5m.csv --hilos 1,2,4,8
dotnet run --project src/VentasParalelo.Cli -c Release -- escalar --tipo fuerte --volumenes 1000000,20000000 --hilos 1,2,4,8
dotnet run --project src/VentasParalelo.Cli -c Release -- escalar --tipo debil --filas-base 250000 --hilos 1,2,4,8
```

Toda medicion descarta un calentamiento y reporta el mejor de tres tiempos. Para corridas
exploratorias mas rapidas, `--repeticiones 1` a costa de mas ruido en los numeros.
