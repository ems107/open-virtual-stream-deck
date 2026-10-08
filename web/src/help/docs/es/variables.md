# Variables y textos dinámicos

Las variables son valores en directo que OVSD mantiene actualizados: la hora, la CPU, la canción que suena, la escena de OBS, tus propios contadores… Puedes **mostrarlas** en las teclas y **usarlas** en condiciones y estados.

> La pestaña **Variables** del editor muestra todas las que existen ahora mismo con su valor real. Es la mejor forma de descubrir qué tienes disponible.

## Mostrar variables en una tecla

Escribe una expresión entre dobles llaves en cualquier texto. Se recalcula sola cada vez que cambia:

```
{{time.hhmm}}
CPU {{round(sys.cpu)}}%
{{media.title}} — {{media.artist}}
{{obs.recording ? '● REC' : ''}}
{{sys.net.down.text}} ↓
```

También funciona en la **imagen** (`{{media.art}}`), en el **icono** y en los **colores**:

```
{{sys.cpu > 80 ? '#c0392b' : '#1f232b'}}
```

## Expresiones

Las condiciones de *Si… entonces*, los estados y el paso *Asignar variable* usan expresiones (sin llaves):

| Elemento | Ejemplos |
|---|---|
| Textos y números | `'Juego'`, `"hola"`, `42`, `3.5`, `true`, `false` |
| Variables | `sys.cpu`, `obs.scene`, `user.contador` |
| Nombres raros entre corchetes | `[mqtt.casa/salon/temperatura]` |
| Comparación | `==`  `!=`  `<`  `<=`  `>`  `>=` |
| Lógica | `&&` `||` `!` (o `and`, `or`, `not`) |
| Aritmética | `+` `-` `*` `/` `%` (con textos, `+` concatena) |
| Condicional | `condición ? si : si_no` |
| Valor por defecto | `user.nombre ?? 'sin nombre'` |
| Funciones | `round(sys.cpu)`, `contains(app.title, 'YouTube')` ([todas](#reference-functions)) |

```
obs.scene == 'Juego' && !audio.mic.muted
sys.cpu > 80 ? 'alto' : 'ok'
default(user.contador, 0) + 1
```

## Variables disponibles

| Grupo | Variables |
|---|---|
| `time.*` | `hhmm`, `hhmmss`, `hh`, `mm`, `ss`, `date`, `weekday`, `day`, `month`, `year`, `unix` |
| `sys.*` | `cpu`, `ram`, `ram.used`, `ram.total`, `gpu`, `gpu.temp`, `gpu.mem`, `gpu.name`, `cpu.temp`, `cpu.power`, `cpu.clock`, `net.down`, `net.up`, `net.down.text`, `net.up.text`, `uptime` |
| `audio.*` | `volume`, `muted`, `device`, `mic.volume`, `mic.muted`, `mic.device` |
| `media.*` | `title`, `artist`, `album`, `art`, `playing`, `status`, `app` |
| `app.*` | `active` (proceso de la ventana activa, p. ej. `chrome`), `title` |
| `obs.*` | `connected`, `scene`, `streaming`, `recording`, `recordPaused`, `replay`, `virtualcam`, `studio`, `preview`, `mute.<entrada>`, `volume.<entrada>`, `visible.<escena>.<fuente>` |
| `discord.*` | `connected`, `mute`, `deaf` |
| `mqtt.*` | `connected` y `mqtt.<topic>` con el último mensaje de cada topic suscrito |
| `webhook.*` | `webhook.<nombre>` y `webhook.<nombre>.time` |
| `toggle.*` | el estado de cada [interruptor](#states) |
| `user.*` | **tus variables** (ver abajo) |
| `value` | el valor de un deslizador, dentro de sus acciones |
| `state` | el [estado](#states) de la tecla (`on`, `off` o su nombre), dentro de sus acciones |
| `index` | la vuelta actual dentro de un paso *Repetir* |

Las de temperatura de CPU necesitan activarla en *Ajustes → Este PC* (ver [Windows](#windows)).

## Tus propias variables

Cualquier variable que empiece por `user.` es tuya: créala con el paso **Asignar variable** y **se guarda aunque reinicies** el PC.

Ejemplo de contador: el botón muestra `{{default(user.cafes, 0)}} ☕` y al pulsarlo:

```
Asignar variable  user.cafes = default(user.cafes, 0) + 1
```

Y con **Mantener pulsado** lo pones a cero: `user.cafes = 0`.

> El valor de *Asignar variable* es una expresión: para guardar un texto, escríbelo entre comillas (`'hola'`).
