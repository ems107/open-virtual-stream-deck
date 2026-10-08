# Windows: multimedia, audio y sistema

Todo esto funciona sin configurar nada.

## Multimedia

Controla cualquier reproductor que Windows reconozca (Spotify, YouTube en el navegador, Apple Music, VLC…): las mismas teclas multimedia que en el teclado y la información de lo que suena.

- Acciones: **Reproducir / pausar**, **Siguiente pista**, **Pista anterior**, **Detener**.
- Variables: `media.title`, `media.artist`, `media.album`, `media.playing`, `media.app` y `media.art` (la carátula).

Botón de música completo:

```
Imagen:  {{media.art}}
Icono:   mdi:play-pause         (se ve cuando no hay carátula)
Texto:   {{truncate(media.title, 18)}}
Pulsar:  Reproducir / pausar
Doble toque: Siguiente pista
```

## Audio

- **Ajustar volumen**: de los altavoces, del micrófono o de **una aplicación concreta** (por ejemplo, solo Discord o solo el juego). Valor absoluto `0`–`100` o relativo con signo: `+5`, `-5`. En un deslizador usa `{{value}}`.
- **Silenciar**: activar, desactivar o alternar el silencio de altavoces, micrófono o una aplicación.
- **Cambiar dispositivo de audio**: pasar de los altavoces a los cascos con un botón.
- Variables: `audio.volume`, `audio.muted`, `audio.device`, `audio.mic.volume`, `audio.mic.muted`, `audio.mic.device`.

> Deslizador de volumen que se mueve solo si cambias el volumen desde Windows: *Valor a mostrar* `audio.volume`, y en *Al mover el deslizador*: *Ajustar volumen · Altavoces · {{value}}*.

## Programas, webs y comandos

- **Abrir aplicación o archivo**: un `.exe`, un documento, una carpeta… con argumentos opcionales.
- **Abrir URL**: en el navegador predeterminado. También sirve para enlaces de apps (`steam://`, `spotify:`…).
- **Ejecutar comando**: PowerShell, cmd o un ejecutable, opcionalmente oculto, guardando la salida en una variable.
- **Escribir texto**: escribe un texto como si lo tecleases (admite variables).

## Métricas del sistema

Se actualizan cada segundo:

| Variable | Qué es |
|---|---|
| `sys.cpu` | Uso de CPU (%) |
| `sys.ram`, `sys.ram.used`, `sys.ram.total` | Memoria: % usado, GB usados y totales |
| `sys.gpu`, `sys.gpu.temp`, `sys.gpu.mem` | Uso, temperatura y memoria de la GPU (*Ajustes → Métricas*) |
| `sys.net.down`, `sys.net.up` | Red en KB/s (y `.text` con formato: «2.4 MB/s») |
| `sys.cpu.temp`, `sys.cpu.power`, `sys.cpu.clock` | Temperatura (°C), consumo (W) y frecuencia (MHz) de la CPU |
| `sys.uptime` | Segundos desde que se encendió el PC |

## Temperatura de la CPU

Windows solo deja leer los sensores del procesador a un driver especial (PawnIO), y solo un programa con permisos de administrador puede usarlo. Para no tener que ejecutar OVSD como administrador, OVSD instala aparte un pequeño **servicio de sensores** que solo lee la temperatura y se la pasa.

Actívalo en **Ajustes → Este PC → Temperatura de CPU → Activar**. Windows pedirá confirmación una vez. Desde ahí también puedes desactivarlo.

## Aplicación activa

`app.active` es el proceso de la ventana en primer plano (`chrome`, `obs64`…) y `app.title` su título. Sirve para el [cambio automático de perfil](#profiles) y para condiciones, por ejemplo mostrar un botón distinto si estás en el juego.
