# Ball Puzzle

Prototipo 3D en Unity de un circuito modular para una bola, con una escena de recorrido físico y otra de construcción por piezas.

## Requisitos

- Unity `6000.4.0f1`
- Universal Render Pipeline
- Input System (incluido en el proyecto)

## Abrir el proyecto

1. Abre esta carpeta desde Unity Hub.
2. Usa Unity `6000.4.0f1` para evitar conversiones de proyecto.
3. Si Unity detecta contenido de recuperación, revísalo antes de descartarlo. Ese contenido es local y se ignora en Git.

## Escenas

- `Assets/Scenes/FinalCircuit.unity`: recorrido físico actual. Es la primera escena de Build Settings.
- `Assets/Scenes/EditCircuit.unity`: editor de circuito con pieza de salida, recta y curva de 45 grados.

## Estado actual

`FinalCircuit` contiene una bola física y un circuito fijo inclinado. `EditCircuit` permite arrastrar piezas desde una paleta, encajarlas mediante conectores y validar un circuito cerrado. El bucle de juego completo (reinicio, meta, puntuación y transición entre escenas) todavía no está implementado.

## Estructura relevante

- `Assets/Scripts`: comportamiento en ejecución del editor y del circuito.
- `Assets/Scripts/Editor`: herramientas para generar y actualizar piezas del recorrido.
- `Assets/Prefabs/CircuitEditor`: piezas disponibles para el editor.
- `Assets/Art`: mallas y materiales generados para los tramos.
