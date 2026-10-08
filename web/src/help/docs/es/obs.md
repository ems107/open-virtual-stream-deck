# OBS Studio

OVSD se conecta a OBS mediante su servidor WebSocket (incluido en OBS 28 y posteriores) para cambiar escenas, emitir, grabar, silenciar fuentes… y mostrar su estado en directo.

## Conectar

1. En OBS: **Herramientas → Ajustes del servidor WebSocket**.
2. Marca **Activar servidor WebSocket**. Deja el puerto en `4455`.
3. Si está marcada *Activar autenticación*, pulsa **Mostrar información de conexión** y copia la **contraseña**.
4. En OVSD: **Ajustes → OBS Studio** → marca **Activado**, deja la URL `ws://127.0.0.1:4455` (si OBS está en el mismo PC), pega la contraseña y pulsa **Guardar**.

El indicador junto al título pasa a **Conectado**. OVSD se reconecta solo si cierras y abres OBS.

> ¿OBS en otro PC? Usa `ws://IP-de-ese-PC:4455` y permite OBS en el firewall de ese PC.

## Acciones

| Acción | Opciones |
|---|---|
| **Cambiar escena** | La lista de escenas se carga de OBS. |
| **Emisión** / **Grabación** | Alternar, iniciar o detener (la grabación también pausar/reanudar). |
| **Búfer de repetición** | Alternar, iniciar, detener o **guardar repetición**. |
| **Cámara virtual** | Alternar, iniciar o detener. |
| **Silenciar entrada de audio** | Micro, audio del escritorio… alternar o fijar. |
| **Volumen de entrada de audio** | 0–100; ideal en un deslizador con `{{value}}`. |
| **Mostrar / ocultar fuente** | Escena + fuente; alternar, mostrar u ocultar. |
| **Modo estudio** / **Transición de estudio** | Para quien usa vista previa y programa. |
| **Petición personalizada** | Cualquier petición de obs-websocket v5, con su respuesta en una variable. |

## Variables para el estado

| Variable | Valor |
|---|---|
| `obs.connected` | OVSD está conectado a OBS |
| `obs.scene` | Nombre de la escena actual |
| `obs.streaming`, `obs.recording`, `obs.recordPaused` | Emitiendo, grabando, grabación en pausa |
| `obs.replay`, `obs.virtualcam`, `obs.studio` | Búfer de repetición, cámara virtual, modo estudio activos |
| `obs.preview` | Escena de vista previa (modo estudio) |
| `obs.mute.<entrada>` | Si una entrada de audio está silenciada, p. ej. `[obs.mute.Mic/Aux]` |
| `obs.volume.<entrada>` | Volumen de una entrada (0–100) |
| `obs.visible.<escena>.<fuente>` | Si una fuente está visible |

> Los nombres con espacios o barras van entre corchetes en las expresiones: `[obs.mute.Audio del escritorio]`.

## Ejemplos

**Botón de escena que se ilumina cuando está activa** (uno por escena):

```
Pulsar: Cambiar escena · Juego
Estado: Según expresión · obs.scene == 'Juego'   (on: fondo verde)
```

**Botón de grabación que se pone rojo**:

```
Texto:  {{obs.recording ? '● REC' : 'Grabar'}}
Pulsar: Grabación · Alternar
Estado: Según expresión · obs.recording        (on: fondo rojo)
```

**Guardar el último minuto** (búfer de repetición activo en OBS):

```
Pulsar: Búfer de repetición · Guardar repetición
        Mostrar mensaje · «Clip guardado»
```
