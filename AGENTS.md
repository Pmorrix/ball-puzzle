# Proyecto: Ball Puzzle 3D (Unity)

## Identidad del proyecto

Este workspace corresponde al proyecto Unity **Ball Puzzle 3D**.

- Ruta correcta: `C:\Users\Phillips\ball puzzle`
- No confundirlo con `C:\Users\Phillips\Documents\BallPuzzle 3D`, Tower Jumping ni otros proyectos.
- Antes de editar, confirmar que el archivo solicitado existe dentro de esta ruta.

## Contexto general

Ball Puzzle 3D es un juego de puzles 3D donde el jugador selecciona, coloca y rota piezas de recorrido para guiar una bola hasta la meta.

Para `Level01`:

- Mantener un primer nivel pequeño, de dos o tres piezas.
- Mantener la colocación libre de las piezas.
- Rotar en incrementos de 45 grados.
- Tener en cuenta las curvas de 45 grados al validar conexiones.
- Hacer obligatoria la recogida del premio o coleccionable.
- No recuperar la antigua regla de circuito cerrado entre Start y Goal.

## Prioridades de trabajo

1. Mantener la solución lo más simple posible.
2. Hacer cambios mínimos y localizados.
3. Reutilizar scripts, prefabs y componentes existentes.
4. No introducir sistemas o scripts nuevos si no son necesarios.
5. Mantener coherencia con la arquitectura actual.
6. No romper referencias del Inspector.
7. Proponer cualquier refactor grande antes de aplicarlo.

## Estilo de implementación

- Lenguaje: C#.
- Entorno: Unity.
- Enfoque: puzle 3D, UI clara y físicas predecibles.
- Preferir métodos pequeños, nombres claros y cambios contenidos.
- Evitar sobreingeniería.
- Evitar DOTween salvo petición explícita.
- No cambiar nombres públicos o serializados sin motivo.
- No eliminar campos `[SerializeField]` sin comprobar su uso.
- Añadir null-checks sencillos cuando sea razonable.

## Qué debe hacer Codex al empezar una tarea

1. Leer primero los scripts directamente relacionados.
2. Detectar dependencias entre scripts antes de editar.
3. Revisar las escenas y los prefabs afectados.
4. Comprobar referencias serializadas del Inspector.
5. Identificar dependencias de eventos, físicas, colliders, flags estáticos, `Time.timeScale`, UI y flujo de escenas.
6. Elegir la solución más pequeña y menos invasiva.
7. Aplicar únicamente lo pedido.
8. Validar la compilación y, cuando proceda, indicar las pruebas necesarias en Play Mode.

## Reglas de UI

- Crear la UI de ejecución como objetos Canvas guardados en escenas o prefabs.
- Usar referencias serializadas configurables desde el Inspector.
- No crear controles visuales durante Play Mode.
- No usar `OnGUI`.

## Qué no debe hacer Codex

- No inventar funcionalidades no solicitadas.
- No trasladar arquitectura ni reglas de otros proyectos.
- No reestructurar sistemas enteros sin autorización.
- No tocar archivos no relacionados por limpieza.
- No asumir que una referencia existe en el Inspector.
- No cambiar el flujo de escenas sin revisar la persistencia.
- No sustituir la colocación libre por el sistema antiguo de snap.

## Formato de respuesta esperado

Entregar:

1. Resumen corto del cambio.
2. Archivos modificados y validaciones realizadas.
3. Script completo si el cambio afecta a varios puntos o un copy-paste parcial puede romperlo.
4. Lista breve de lo que debe revisarse en el Inspector.
5. Posibles efectos colaterales.
6. No implementar alternativas sin permiso.
