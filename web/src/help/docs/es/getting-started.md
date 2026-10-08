# Primeros pasos

## 1. OVSD en marcha

Cuando OVSD está funcionando verás su icono (cuatro cuadrados) en la **bandeja del sistema**, junto al reloj de Windows. Si no lo ves, mira en la flechita `^` de iconos ocultos.

- **Doble clic** en el icono: abre este editor.
- **Clic derecho**: conectar un dispositivo, iniciar con Windows, abrir la carpeta de datos y salir.

> Si al arrancar ves un aviso amarillo «Tus móviles y tablets todavía no pueden conectarse», pulsa **Permitir conexiones** y acepta el aviso de Windows. Solo hay que hacerlo una vez (ver [Problemas frecuentes](#troubleshooting)).

## 2. Conectar el móvil o la tablet

1. El móvil tiene que estar en la **misma WiFi** que el PC.
2. En el PC: clic derecho en el icono de la bandeja → **Conectar dispositivo (QR)** (o *Dispositivos → Conectar dispositivo* en el editor).
3. Escanea el código QR con la cámara del móvil y abre el enlace. Si no puedes escanear, escribe en el navegador del móvil la dirección que aparece debajo del QR e introduce el PIN de 6 dígitos.

El dispositivo queda emparejado: la próxima vez basta con abrir la misma dirección. Para tenerlo a mano, en el navegador del móvil usa **Añadir a la pantalla de inicio**.

> Solo los dispositivos emparejados pueden pulsar botones. Puedes desvincular cualquiera desde *Dispositivos*.

## 3. Tu primer botón

Vamos a crear un botón que silencie el micrófono y se ponga rojo cuando está silenciado.

1. Ve a **Perfiles** (la primera pestaña de este editor). Verás la rejilla del perfil de ejemplo.
2. Haz clic en un **hueco vacío** y elige **Añadir botón**.
3. En el panel de la derecha, pestaña **Aspecto**: escribe `Mic` como texto y pulsa el botón del icono para elegir `microphone`.
4. Pestaña **Acciones** → *Pulsar* → **Añadir paso** → **Acción** → busca **Silenciar** (categoría Audio). Elige *Destino: Micrófono* y *Modo: Alternar*.
5. Pestaña **Estado** → **Según expresión** y escribe `audio.mic.muted`. En el aspecto del estado **on** pon un fondo rojo y el icono `microphone-off`.

Los cambios se guardan solos y aparecen al instante en el móvil. Pulsa el botón y verás cómo cambia de color.

## 4. Y ahora…

- Explora el [perfil de ejemplo](#editor): cada tecla muestra una función distinta.
- Aprende a usar [el deck en el móvil](#on-the-phone): pantalla completa, pantalla siempre encendida y edición desde el propio móvil.
- Inspírate con las [recetas](#recipes).
