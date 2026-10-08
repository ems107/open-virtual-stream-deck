# Teclas: botones, deslizadores y widgets

Cada tecla de la rejilla es de uno de estos tres tipos (lo eliges arriba del inspector, en **Tipo**):

| Tipo | Para qué |
|---|---|
| **Botón** | Ejecutar acciones al tocarlo. Puede mostrar texto, icono, imagen y cambiar de aspecto según su estado. |
| **Deslizador** | Un valor que se ajusta arrastrando el dedo: volumen, brillo, volumen de una fuente de OBS… |
| **Widget** | Mostrar información: texto, gráfica en el tiempo o indicador circular. También puede tener acciones. |

## Aspecto

Pestaña **Aspecto** del inspector:

- **Texto**: admite varias líneas y [textos dinámicos](#variables) como `{{round(sys.cpu)}}%`.
- **Tamaño de texto** y **posición** (arriba, centro, abajo). El tamaño es relativo a la tecla, así que se ve igual en un móvil que en una tablet.
- **Icono**: pulsa el botón para buscar entre miles de iconos (Material Design y Lucide). Busca en inglés: `play`, `mic`, `volume`, `light`… También puedes escribir el nombre directamente, por ejemplo `mdi:microphone`.
- **Imagen**: sube un PNG, JPG, GIF animado o SVG, pega una URL, o usa una variable como `{{media.art}}` para la carátula de la canción que suena. Si la imagen está vacía (por ejemplo, no suena nada), se muestra el icono.
- **Colores**: de fondo, del texto y del icono. Vacío = el del tema del perfil.

## Tamaño

Pestaña **Tamaño**: alto (filas) y ancho (columnas). Una tecla de 2×1 es perfecta para una gráfica; un deslizador de 3 filas de alto es cómodo para el volumen.

## Deslizadores

En la pestaña **Tamaño** de un deslizador:

- **Mínimo, máximo y paso**: por ejemplo 0, 100 y 1.
- **Orientación**: vertical u horizontal.
- **Valor a mostrar**: una expresión con la posición real, por ejemplo `audio.volume`. Así, si cambias el volumen desde Windows, el deslizador del móvil se mueve solo.
- **Color** de la barra.

Sus acciones van en **Al mover el deslizador**, y el valor elegido está en la variable `value`:

```
Acción: Ajustar volumen · Destino: Altavoces · Valor: {{value}}
```

> OVSD envía los cambios mientras arrastras, pero sin saturar el PC, y siempre envía el valor final al soltar.

## Widgets

En la pestaña **Tamaño** de un widget eliges el **tipo**:

- **Texto**: solo muestra su texto (útil para un reloj: `{{time.hhmm}}`).
- **Gráfica**: dibuja la evolución de un **valor numérico** (por ejemplo `sys.cpu`) durante los últimos N segundos (**Muestras**), entre un mínimo y un máximo.
- **Indicador**: un arco que se llena según el valor, como un cuentarrevoluciones.

Combínalos con el texto: una gráfica de 2×1 con el texto `CPU {{round(sys.cpu)}}%` arriba queda muy bien.

## Estados

Una tecla puede cambiar de aspecto según su estado: interruptor encendido/apagado, micrófono silenciado, escena de OBS activa… Lo explica [Estados visuales](#states).
