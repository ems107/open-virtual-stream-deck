# Estados visuales

Un estado hace que una tecla cambie de aspecto: un micrófono que se pone rojo al silenciarse, un botón de OBS que parpadea en «REC», una luz que se enciende… Se configura en la pestaña **Estado** del inspector.

## Los tres modos

| Modo | Cómo funciona |
|---|---|
| **Ninguno** | La tecla siempre se ve igual. |
| **Interruptor** | Cada toque alterna entre apagado y **on**. Defines cómo se ve en «on». |
| **Según expresión** | El estado sale de una [expresión](#variables) que OVSD vigila continuamente. |

## Interruptor

Útil para cosas que OVSD no puede saber por sí mismo (por ejemplo, «modo noche» de un programa). El estado se guarda en la variable `toggle.<id>`. La pestaña **Estado** te muestra el nombre exacto, por ejemplo `toggle.khgw85mg5x`, para que lo uses en condiciones de otras teclas.

Para cambiar el interruptor desde otra tecla o una macro, usa la acción **Cambiar estado de interruptor** (indica el id de la tecla, lo que va después de `toggle.`, o déjalo vacío para la propia).

## Según expresión

La expresión se recalcula sola cada vez que cambian sus variables:

- Si devuelve **verdadero / falso**, la tecla usa el aspecto **on** o el normal.
- Si devuelve un **texto**, se usa el estado con ese nombre. Así una sola tecla puede tener muchos aspectos.

```
audio.mic.muted                    → on cuando el micro está silenciado
obs.recording                      → on mientras graba
sys.cpu > 80                       → on con la CPU por encima del 80 %
obs.scene                          → estados «Juego», «Chat», «Pausa»… según la escena
```

### Varios estados con nombre

1. Elige **Según expresión** y escribe, por ejemplo, `obs.scene`.
2. En **nuevo estado** escribe `Juego` y añádelo; repite con `Chat` y `Pausa`.
3. Selecciona cada estado y define su aspecto (color, icono, texto…).

Cuando la escena de OBS cambie, la tecla mostrará el aspecto del estado que coincida, o el normal si ninguno coincide.

## Qué se puede cambiar en cada estado

Cualquier cosa del aspecto: texto, icono, imagen, colores, tamaño y posición del texto. **Lo que dejes vacío se hereda** del aspecto normal, así que normalmente basta con cambiar el fondo y el icono.

> Los estados también funcionan en deslizadores y widgets. Por ejemplo, un widget de temperatura que se pone rojo con `sys.cpu.temp > 80`.
