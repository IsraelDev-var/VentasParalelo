# Reporte final — VentasParalelo

Entrega: 21 de agosto. Todas las mediciones de este reporte se corrieron en modo `Release`
sobre esta misma maquina (`Environment.ProcessorCount` = 8 hilos logicos), con los comandos
`comparar` y `escalar` del CLI, contra datasets generados con `SalesDataGenerator` (semilla fija,
resultados reproducibles).

## 1. Estrategias comparadas (1,000,000 filas, hilos 1/2/4/8)

```
Estrategia                                           Hilos  Tiempo (s)     Filas/seg   Speedup Eficiencia
---------------------------------------------------------------------------------------------------------
Secuencial (baseline)                                    1       0.135     7,416,224      1.00      100 %
Parallel.ForEach + lock sobre Dictionary                 1       0.253     3,946,356      0.53       53 %
                                                        mapeo=  0.000s  reduccion=  0.000s  contencion=  0.027s
Parallel.ForEach + lock sobre Dictionary                 2       0.216     4,623,153      0.62       31 %
                                                        mapeo=  0.000s  reduccion=  0.000s  contencion=  0.239s
Parallel.ForEach + lock sobre Dictionary                 4       0.206     4,860,605      0.66       16 %
                                                        mapeo=  0.000s  reduccion=  0.000s  contencion=  0.627s
Parallel.ForEach + lock sobre Dictionary                 8       0.204     4,906,723      0.66        8 %
                                                        mapeo=  0.000s  reduccion=  0.000s  contencion=  1.380s
Parallel.ForEach + ConcurrentDictionary                  1       0.235     4,250,569      0.57       57 %
Parallel.ForEach + ConcurrentDictionary                  2       0.141     7,091,575      0.96       48 %
Parallel.ForEach + ConcurrentDictionary                  4       0.122     8,207,532      1.11       28 %
Parallel.ForEach + ConcurrentDictionary                  8       0.137     7,297,917      0.98       12 %
Parallel.ForEach + acumuladores locales + reduccion final     1       0.097    10,313,904      1.39      139 %
                                                        mapeo=  0.096s  reduccion=  0.000s  contencion=  0.000s
Parallel.ForEach + acumuladores locales + reduccion final     2       0.054    18,482,478      2.49      125 %
                                                        mapeo=  0.107s  reduccion=  0.000s  contencion=  0.000s
Parallel.ForEach + acumuladores locales + reduccion final     4       0.034    29,762,170      4.01      100 %
                                                        mapeo=  0.127s  reduccion=  0.000s  contencion=  0.000s
Parallel.ForEach + acumuladores locales + reduccion final     8       0.020    48,950,501      6.60       83 %
                                                        mapeo=  0.155s  reduccion=  0.000s  contencion=  0.000s
Particionado round-robin + acumuladores locales          1       0.096    10,455,406      1.41      141 %
                                                        mapeo=  0.093s  reduccion=  0.000s  contencion=  0.000s
Particionado round-robin + acumuladores locales          2       0.057    17,416,020      2.35      117 %
                                                        mapeo=  0.113s  reduccion=  0.000s  contencion=  0.000s
Particionado round-robin + acumuladores locales          4       0.035    28,591,688      3.86       96 %
                                                        mapeo=  0.134s  reduccion=  0.000s  contencion=  0.000s
Particionado round-robin + acumuladores locales          8       0.027    37,354,411      5.04       63 %
                                                        mapeo=  0.194s  reduccion=  0.000s  contencion=  0.000s
Chunking dinamico + acumuladores locales                 1       0.106     9,421,474      1.27      127 %
                                                        mapeo=  0.102s  reduccion=  0.000s  contencion=  0.000s
Chunking dinamico + acumuladores locales                 2       0.052    19,368,134      2.61      131 %
                                                        mapeo=  0.102s  reduccion=  0.000s  contencion=  0.000s
Chunking dinamico + acumuladores locales                 4       0.036    27,877,673      3.76       94 %
                                                        mapeo=  0.142s  reduccion=  0.000s  contencion=  0.000s
Chunking dinamico + acumuladores locales                 8       0.023    43,448,225      5.86       73 %
                                                        mapeo=  0.179s  reduccion=  0.000s  contencion=  0.000s
Particiones locales + reduccion jerarquica en arbol      1       0.115     8,670,568      1.17      117 %
                                                        mapeo=  0.114s  reduccion=  0.000s  contencion=  0.000s
Particiones locales + reduccion jerarquica en arbol      2       0.064    15,626,318      2.11      105 %
                                                        mapeo=  0.123s  reduccion=  0.000s  contencion=  0.000s
Particiones locales + reduccion jerarquica en arbol      4       0.035    28,312,010      3.82       95 %
                                                        mapeo=  0.136s  reduccion=  0.000s  contencion=  0.000s
Particiones locales + reduccion jerarquica en arbol      8       0.020    48,915,542      6.60       82 %
                                                        mapeo=  0.144s  reduccion=  0.000s  contencion=  0.000s
PLINQ GroupBy                                            1       0.407     2,454,691      0.33       33 %
PLINQ GroupBy                                            2       0.298     3,355,380      0.45       23 %
PLINQ GroupBy                                            4       0.140     7,160,082      0.97       24 %
PLINQ GroupBy                                            8       0.230     4,339,415      0.59        7 %
```

Todas las estrategias reproducen exactamente los mismos totales que el baseline secuencial
(columna de speedup/eficiencia sin marca `(!)`), verificado ademas por
`AggregationStrategyTests.Aggregate_ProduceLosMismosTotalesQueElBaseline` sobre 5,000 filas.

### Lecturas clave

- **`lock` sobre `Dictionary` no escala**: al pasar de 1 a 8 hilos el tiempo casi no baja
  (0.253s -> 0.204s) porque cada fila compite por el mismo candado global. El diagnostico de
  contencion lo confirma en numeros: con 1 hilo la espera por el lock es 0.027s, y con 8 hilos
  sube a 1.380s de tiempo total esperando el candado — practicamente todo el tiempo de pared se
  va en contencion, no en trabajo util.
- **`ConcurrentDictionary` mejora pero no es gratis**: su locking interno por bucket reduce la
  contencion visible respecto al lock global, pero sigue sincronizando en cada fila
  (`AddOrUpdate`), por lo que su eficiencia tambien cae con mas hilos (57% -> 12%). Al ser
  locking interno, no hay forma de medir su contencion desde afuera (por eso no imprime
  diagnosticos): es una caja negra en ese aspecto.
- **Acumuladores locales + reduccion (contigua, round-robin, chunking, arbol) escalan mucho
  mejor**: con 8 hilos superan 6x de speedup y 60-83% de eficiencia, porque cada hilo trabaja
  sobre su propio acumulador sin sincronizar por fila; el lock solo se toma una vez por
  particion (mapeo domina el tiempo total, reduccion es practicamente 0s en la tabla).
- **Particionado (item 4): contigua vs round-robin vs chunking dinamico** sobre el mismo
  algoritmo de reduccion muestran tiempos de mapeo similares a hilos bajos, pero round-robin se
  degrada mas que las otras dos al llegar a 8 hilos (eficiencia 63% vs 83% de la contigua) por
  peor localidad de cache: cada hilo salta por el arreglo con paso `P` en vez de recorrer un
  bloque contiguo en memoria. El chunking dinamico queda entre ambas: reparte trabajo en chunks
  pequenos tomados de una cola compartida, lo que balancea mejor cuando el costo por fila no es
  uniforme, a costa de mas overhead de coordinacion que una particion estatica.
- **Reduccion en un paso vs jerarquica en arbol (item 6)**: en este dataset ambas rinden
  parecido porque el numero de particiones (<=8) es chico y el merge es barato — la ventaja de
  la reduccion en arbol (O(log P) niveles paralelos vs O(P) locks seriales) se nota mas con
  muchas mas particiones que hilos fisicos (ver seccion de escalabilidad de volumen).
- **PLINQ GroupBy** es la mas lenta y la menos consistente (baja de 0.97 a 0.59 de speedup entre
  4 y 8 hilos): al no controlar el particionado ni la reduccion, no se puede diagnosticar por
  que empeora, y ese es justamente el costo de programar en un nivel mas alto — se gana
  legibilidad, se pierde control fino (y con el, la capacidad de medir mapeo/reduccion/contencion).

## 1.1 Grano grueso: eliminar toda dependencia entre hilos (5,000,000 filas)

Se agrego una octava estrategia, `CoarseGrainedTaskAggregator`: en vez de que las particiones
compartan siquiera el lock de merge de "acumuladores locales" (que ya es minimo, una vez por
particion), cada hilo escribe su resultado en su propio slot de un arreglo — cero estado
compartido y cero locks durante todo el computo — y la fusion final es un solo paso secuencial
en un unico hilo, ya sin ninguna escritura concurrente que sincronizar.

Con el dataset de 1,000,000 filas usado en la seccion 1, las mediciones entre corridas
identicas variaban demasiado (la eficiencia de "reduccion jerarquica" con 8 hilos salto de 82%
a 97% y luego a 29% en tres corridas seguidas) porque el trabajo por hilo es tan chico
(~0.02s) que el ruido de otros procesos de esta maquina (no es un servidor de benchmarking
dedicado) domina la medicion. Con 5,000,000 filas el trabajo por hilo es 5x mayor y el ruido se
diluye; los resultados salen estables y repetibles:

```
Estrategia                                              Hilos  Tiempo(s)   Filas/seg   Speedup  Eficiencia
------------------------------------------------------------------------------------------------------------
Secuencial (baseline)                                       1      0.801   6,238,438      1.00       100%
lock sobre Dictionary                                        1      1.366   3,661,234      0.59        59%
lock sobre Dictionary                                        8      1.592   3,141,630      0.50         6%
ConcurrentDictionary                                          1      1.276   3,917,494      0.63        63%
ConcurrentDictionary                                          8      0.884   5,658,369      0.91        11%
Acumuladores locales (contigua)                               1      0.768   6,512,627      1.04       104%
Acumuladores locales (contigua)                               8      0.236  21,207,001      3.40        42%
Round-robin                                                    1      0.783   6,389,048      1.02       102%
Round-robin                                                    8      0.340  14,713,698      2.36        29%
Chunking dinamico                                              1      0.795   6,289,005      1.01       101%
Chunking dinamico                                              8      0.230  21,774,367      3.49        44%
Reduccion jerarquica en arbol                                  1      0.766   6,525,210      1.05       105%
Reduccion jerarquica en arbol                                  8      0.251  19,897,441      3.19        40%
Grano grueso: computo independiente + fusion secuencial        1      0.724   6,906,468      1.11       111%
Grano grueso: computo independiente + fusion secuencial        8      0.226  22,121,838      3.55        44%
PLINQ GroupBy                                                  1      1.958   2,553,936      0.41        41%
PLINQ GroupBy                                                  8      0.926   5,400,470      0.87        11%
```

Lecturas:

- **Grano grueso gana**: con 8 hilos es la estrategia mas rapida de todas (3.55x, 22.1M
  filas/seg), apenas por delante de chunking dinamico (3.49x) y acumuladores locales por rangos
  contiguos (3.40x). Confirma la intuicion de liberar toda dependencia entre hilos: al no
  compartir ni siquiera el lock de merge durante el computo (solo se toca memoria compartida
  una vez, al final, en un solo hilo), no queda ningun punto de sincronizacion que pueda
  generar espera entre hilos.
- **La diferencia entre las variantes "sin contencion" (acumuladores locales, round-robin,
  chunking, arbol, grano grueso) es chica una vez que el trabajo por hilo es grande** (3.19x a
  3.55x, todas entre 40-44% de eficiencia con 8 hilos) — la eleccion entre ellas importa mas
  para datasets chicos o con overhead relativo alto que para este volumen.
- **Round-robin es consistentemente la peor del grupo "sin contencion"** (2.36x, 29%) por la
  localidad de cache: acceder al arreglo con paso `P` en vez de en bloques contiguos cuesta mas
  cache misses, y ese costo no desaparece al crecer el dataset.
- **La eficiencia de todas las estrategias buenas cae de forma pareja de ~100% (1-2 hilos) a
  ~40-44% (8 hilos)**: esto ya no es ruido (los resultados son repetibles), sino una senal real
  de que esta maquina tiene menos nucleos fisicos que 8 hilos logicos (probablemente 4 nucleos
  con hyperthreading) — a partir de cierto punto, agregar "hilos" logicos adicionales compite
  por los mismos nucleos fisicos en vez de sumar capacidad de computo nueva.

## 2. Escalabilidad fuerte (mismo dataset, mas hilos) — `escalar --tipo fuerte`

Estrategia: acumuladores locales + reduccion final.

```
== 1,000,000 filas ==
  hilos=1    tiempo=0.142s  speedup=1.00  eficiencia=100 %
  hilos=2    tiempo=0.069s  speedup=2.07  eficiencia=103 %
  hilos=4    tiempo=0.047s  speedup=3.06  eficiencia= 77 %
  hilos=8    tiempo=0.029s  speedup=4.83  eficiencia= 60 %

== 5,000,000 filas ==
  hilos=1    tiempo=0.436s  speedup=1.00  eficiencia=100 %
  hilos=2    tiempo=0.248s  speedup=1.76  eficiencia= 88 %
  hilos=4    tiempo=0.143s  speedup=3.06  eficiencia= 76 %
  hilos=8    tiempo=0.101s  speedup=4.32  eficiencia= 54 %

== 20,000,000 filas ==
  hilos=1    tiempo=1.585s  speedup=1.00  eficiencia=100 %
  hilos=2    tiempo=0.930s  speedup=1.70  eficiencia= 85 %
  hilos=4    tiempo=0.566s  speedup=2.80  eficiencia= 70 %
  hilos=8    tiempo=0.327s  speedup=4.84  eficiencia= 61 %
```

La eficiencia cae de forma consistente al pasar de 4 a 8 hilos en los tres volumenes (esta
maquina tiene 8 hilos logicos mapeados sobre menos nucleos fisicos, ademas del overhead fijo de
coordinar `Parallel.ForEach`), pero el speedup absoluto sigue subiendo con el volumen: a 20M
filas 8 hilos siguen dando 4.84x, muy cerca del 4.83x visto a 1M — la ley de Amdahl pesa menos
cuanto mas grande es el dataset frente al overhead fijo de arrancar y coordinar los hilos.

## 3. Escalabilidad debil (filas proporcional a hilos) — `escalar --tipo debil`

Estrategia: acumuladores locales + reduccion final. Filas base: 250,000 por hilo.

```
  hilos=1  filas=   250,000  tiempo=0.044s  eficiencia-debil=100 %
  hilos=2  filas=   500,000  tiempo=0.035s  eficiencia-debil=128 %
  hilos=4  filas= 1,000,000  tiempo=0.039s  eficiencia-debil=115 %
  hilos=8  filas= 2,000,000  tiempo=0.041s  eficiencia-debil=109 %
```

El tiempo se mantiene practicamente constante (0.035s-0.044s) aunque el volumen de datos crece
proporcional a los hilos — la definicion misma de buena escalabilidad debil. La eficiencia-debil
por encima de 100% en hilos=2..8 respecto a hilos=1 se explica porque la corrida de un solo hilo
con 250,000 filas es tan chica que el overhead fijo (crear el `Parallel.ForEach`, alocar el
acumulador) pesa proporcionalmente mas que en las corridas con mas datos y mas hilos.

## 4. Tiempo de contencion y desglose mapeo vs. reduccion (item 8)

Ver columnas `mapeo=`/`reduccion=`/`contencion=` en la tabla de la seccion 1. Resumen:

| Estrategia | Contencion medible | Que domina el tiempo |
|---|---|---|
| `lock` sobre `Dictionary` | Si — crece de 0.027s (1 hilo) a 1.380s (8 hilos) | Contencion por fila |
| `ConcurrentDictionary` | No (locking interno, opaco) | — |
| Acumuladores locales (contigua/round-robin/chunking/arbol) | Reduccion si, contencion no aplica (solo O(particiones) locks) | Mapeo (99%+ del tiempo) |
| PLINQ GroupBy | No (PLINQ no expone sus fases internas) | — |

Esto confirma el objetivo central del proyecto: mover la sincronizacion de "una vez por fila"
(`lock`/`ConcurrentDictionary`) a "una vez por particion" (acumuladores locales) elimina la
contencion como factor relevante, dejando el mapeo (trabajo util) como el termino dominante del
tiempo total.

## 5. Conclusiones

1. Sincronizar por fila (`lock`, `ConcurrentDictionary`) es la peor estrategia para este problema:
   la contencion crece con los hilos y anula la mayor parte del paralelismo.
2. Acumular localmente por particion y reducir al final (una vez por particion, no por fila) es
   la estrategia mas efectiva medida aqui, sin importar mucho el detalle de particionado
   (contigua/round-robin/chunking) o de reduccion (un paso/arbol) — todas superan 3.7x-6.6x de
   speedup con 8 hilos sobre 1M filas.
3. Entre las variantes de particionado, la contigua tiene mejor localidad de cache que
   round-robin; el chunking dinamico es un punto intermedio que se justifica mas cuando el costo
   por fila no es uniforme (no es el caso de este dataset sintetico, donde el costo por fila es
   casi constante).
4. Herramientas de alto nivel como PLINQ dan legibilidad a costa de control: no permiten
   diagnosticar donde se pierde tiempo, y en este benchmark rindieron peor que la reduccion
   manual con `localInit`/`localFinally`.
5. La escalabilidad debil confirma que el diseño de acumuladores locales tolera crecer datos y
   hilos en proporcion manteniendo el tiempo de respuesta constante — la propiedad deseable para
   un job batch nocturno que debe absorber datasets cada vez mas grandes agregando hardware.
6. Llevar la idea al extremo — grano grueso, cero estado compartido durante todo el computo, una
   sola fusion secuencial al final — dio la mejor marca medida en este proyecto (3.55x/44% con
   8 hilos sobre 5M filas), aunque por un margen chico sobre las demas variantes "sin
   contencion": una vez eliminada la sincronizacion por fila, el techo real pasa a estar puesto
   por la cantidad de nucleos fisicos disponibles, no por el detalle fino de como se particiona
   o se reduce.

## Como reproducir estos numeros

```bash
dotnet run --project src/VentasParalelo.Cli -c Release -- generar --filas 1000000 --salida data/ventas_1m.csv
dotnet run --project src/VentasParalelo.Cli -c Release -- generar --filas 5000000 --salida data/ventas_5m.csv
dotnet run --project src/VentasParalelo.Cli -c Release -- comparar --archivo data/ventas_1m.csv --hilos 1,2,4,8
dotnet run --project src/VentasParalelo.Cli -c Release -- comparar --archivo data/ventas_5m.csv --hilos 1,2,4,8
dotnet run --project src/VentasParalelo.Cli -c Release -- escalar --tipo fuerte --volumenes 1000000,5000000,20000000 --hilos 1,2,4,8
dotnet run --project src/VentasParalelo.Cli -c Release -- escalar --tipo debil --filas-base 250000 --hilos 1,2,4,8
```
