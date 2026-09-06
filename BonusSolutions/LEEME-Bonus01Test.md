# Bonus01Test: circuito fijo con piezas reales

## Estrategia actual

El montaje fijo se construye en la escena mediante
`Ball Puzzle > Bonus01Test > Construir circuito FIJO`.
Usa 14 curvas de 45 grados, 3 medias rectas, 1 recta entera y las tres
especiales existentes: curva90, curva180 y recta con elevación (21 piezas).
La recta entera sustituye las dos medias consecutivas de las filas 15 y 16
de la tabla histórica inferior, conservando exactamente sus extremos.
No se cambia la escala de ningún prefab.

Las tres especiales quedan en `Bonus Targets`, con
`BonusRotatingPieceConnector` desactivado; el resto queda en
`Circuito fijo - montaje real`. `BonusPieceChallengeController` queda
desactivado para esta fase de montaje. No activar todavía los giros.
Las piezas sueltas anteriores se conservan en un grupo desactivado.

El constructor verifica conectores, direcciones, cierre en START y límites
del tablero antes de guardar. El informe es `fixed-circuit-report.txt`.
Esta fase es un montaje fijo para revisar, no una integración de PLAY ni
una nueva prueba de vuelta física. La validación automática de la estrategia
anterior no debe confundirse con la reproducibilidad manual del nuevo bonus.

## Referencia histórica: estrategia anterior con capturas en movimiento

Proyecto: `C:\Users\Phillips\ball puzzle`.
Escena: `Assets/Scenes/Bonus01Test.unity`.

La construcción está verificada en Play Mode utilizando la paleta, posiciones
de puntero redondeadas, rotaciones de 45 grados, ajuste al soltar y PLACE.
Las tres móviles se capturan mediante su giro normal. La prueba física real
ha superado la vuelta completa en 32,78 segundos: 22/22 salidas en orden,
3/3 móviles recorridas y exactamente un evento de finalización.

Solo Bonus01Test activa el impulso continuo autorizado: velocidad objetivo
3,5 unidades/s, aceleración máxima 6 unidades/s² y masa 1. Se aplica una fuerza
en la dirección de la velocidad física, únicamente sobre una pieza de pista.
Las colisiones determinan los giros; no se guía la bola por una ruta calculada.
Se mantienen gravedad, rozamiento y colisiones. En el Inspector del controlador:
Continuous Ball Impulse activado, Launch Speed 3.5 y Ball Drive Acceleration 6.
Los demás niveles no activan este impulso.

## Cómo reproducir el montaje

Usar 14 curvas de 45 grados y 5 rectas cortas. Las tres piezas móviles ya están
en la escena: curva de 90 grados, curva de 180 grados y recta con joroba.
La recta larga normal también sigue disponible en la paleta, aunque esta
solución concreta usa rectas cortas.

Construir desde START siguiendo el orden de la tabla. Al llegar a una móvil,
esperar a que uno de sus extremos coincida con el extremo libre colocado.
Después continuar desde su otro extremo. No mover las móviles en el Inspector.
Las coordenadas son una referencia del montaje, no datos que el jugador deba
introducir: el arrastre y el ajuste de conexión producen las posiciones finales.

| Orden | Pieza | Giro Y | X | Z |
|---:|---|---:|---:|---:|
| 1 | Curva 45 | 225° | 6.156 | -8.071 |
| 2 | Curva 45 | 180° | 7.621 | -4.535 |
| 3 | Móvil 90 | 0° | 7.621 | -4.535 |
| 4 | Curva 45 | 270° | 2.621 | 0.465 |
| 5 | Curva 45 | 315° | -0.915 | 1.929 |
| 6 | Curva 45 | 0° | -2.379 | 5.465 |
| 7 | Curva 45 | 45° | -0.915 | 9.000 |
| 8 | Curva 45 | 90° | 2.621 | 10.465 |
| 9 | Curva 45 | 270° | 9.692 | 7.536 |
| 10 | Móvil 180 | 90° | 9.692 | 7.536 |
| 11 | Recta corta | 270° | 7.692 | 17.536 |
| 12 | Curva 45 | 45° | 2.156 | 16.071 |
| 13 | Recta corta | 225° | 0.742 | 14.657 |
| 14 | Móvil con joroba | 225° o 45° | -3.501 | 10.414 |
| 15 | Recta corta | 225° | -7.743 | 6.172 |
| 16 | Recta corta | 225° | -10.572 | 3.343 |
| 17 | Curva 45 | 0° | -13.450 | -1.606 |
| 18 | Curva 45 | 315° | -11.986 | -5.142 |
| 19 | Curva 45 | 270° | -8.450 | -6.606 |
| 20 | Curva 45 | 90° | -8.450 | -6.606 |
| 21 | Curva 45 | 270° | -1.379 | -9.535 |
| 22 | Recta corta | 90° | 0.621 | -9.535 |

Las posiciones son las de los pivotes, no las de los centros visuales. Por eso
algunas piezas consecutivas comparten posición de pivote sin superponerse.
Las dos orientaciones de la joroba representan la misma geometría simétrica.

## Verificación reproducible

Fuera de Play Mode, el menú de Unity
`Ball Puzzle > Bonus01Test > Probar solución elegida (Play Mode)` ejecuta el
montaje mediante las funciones reales de colocación, comprueba el cierre y
lanza la bola. Guarda su resultado en `BonusSolutions/playtest-report.txt`.
La escena normal sigue empezando con las tres móviles y la paleta para jugar.

La prueba comprueba además que una móvil ausente o una pieza fuera del tablero
bloquean PLAY, y que una caída produce fallo sin emitir la finalización del nivel.

Las mallas corregidas de los bordes de la curva de 180 están en
`Assets/Art/ClassicReferenceTrack/Bonus01Test`. Solo esta escena las utiliza;
las mallas originales y sus texturas se conservan.

La copia `Bonus01Test.before-curve90-realignment-20260906.unity` conserva las
posiciones guardadas antes de realinear las móviles en esta revisión.
