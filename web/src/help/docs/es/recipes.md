# Recetas

Ejemplos listos para montar. Cada uno indica qué poner en cada pestaña del inspector.

## Micrófono con estado

```
Aspecto:  Texto «Mic» · Icono mdi:microphone
Pulsar:   Silenciar · Micrófono · Alternar
Estado:   Según expresión · audio.mic.muted
          on → Fondo #7a1f1f · Icono mdi:microphone-off · Texto «Muted»
```

## Pulsar para hablar

```
Al presionar: Mantener teclas pulsadas · F13
Al soltar:    Soltar teclas · F13
```

Asigna `F13` como tecla de *pulsar para hablar* en Discord, el juego o la app que uses (grábala desde OVSD si tu teclado no tiene F13).

## Monitor del PC

Un widget **Gráfica** de 2×1 por métrica:

```
CPU:   Valor sys.cpu       · Texto «CPU {{round(sys.cpu)}}%» arriba · Color #3ecf8e
GPU:   Valor sys.gpu       · Texto «GPU {{round(sys.gpu)}}% · {{sys.gpu.temp}}°»
RAM:   Valor sys.ram       · Texto «RAM {{sys.ram.used}} / {{sys.ram.total}} GB»
```

Y un estado para alertar: `sys.cpu.temp > 85` → fondo rojo.

## Reproductor de música

```
Imagen: {{media.art}} · Icono mdi:music · Texto {{truncate(media.title, 18)}}
Pulsar: Reproducir / pausar
Doble toque: Siguiente pista
Mantener: Pista anterior
```

Al lado, un **deslizador** vertical de 3 filas: *Valor a mostrar* `audio.volume`, *Al mover*: *Ajustar volumen · Altavoces · {{value}}*.

## Volumen de una sola aplicación

Para bajar solo el juego o solo Discord: *Ajustar volumen · Destino: Aplicación · Aplicación: Discord · Valor: {{value}}* en un deslizador, o `-10` / `+10` en dos botones.

## Cambiar entre altavoces y cascos

```
Si   audio.device == 'Altavoces (Realtek)'
     Cambiar dispositivo de audio · Auriculares
Si no
     Cambiar dispositivo de audio · Altavoces (Realtek)
Texto: {{contains(audio.device, 'Auriculares') ? '🎧' : '🔊'}}
```

(Copia los nombres exactos de `audio.device` en la pestaña Variables.)

## Panel de streaming (OBS)

- Una tecla por escena con *Cambiar escena* y estado `obs.scene == 'Nombre'`.
- **Emitir**: *Emisión · Alternar*, texto `{{obs.streaming ? 'EN DIRECTO' : 'Emitir'}}`, estado `obs.streaming`.
- **Grabar**: igual con `obs.recording`.
- **Clip**: *Búfer de repetición · Guardar repetición*.
- Deslizador de **volumen del micro de OBS**: *Volumen de entrada de audio · Mic/Aux · {{value}}*, valor a mostrar `[obs.volume.Mic/Aux]`.

Ver [OBS Studio](#obs).

## Contador

```
Texto:    {{default(user.contador, 0)}}
Pulsar:   Asignar variable · user.contador = default(user.contador, 0) + 1
Mantener: Asignar variable · user.contador = 0
```

## Escribir textos frecuentes

*Escribir texto* con tu correo, una firma o un comando largo. Con variables: `Hoy es {{time.date}}`.

## Modo «no molestar»

Un interruptor que, al activarse, silencia Discord y pausa la música, y al desactivarse los recupera:

```
Estado: Interruptor (on: fondo morado, icono mdi:moon-waning-crescent)
Pulsar:
  Si   state == 'on'
       Silenciar en Discord · Activar
       Reproducir / pausar
  Si no
       Silenciar en Discord · Desactivar
       Reproducir / pausar
```

> Dentro de las acciones de una tecla, `state` es su estado actual (`on`, `off` o el nombre del estado). El interruptor cambia antes de ejecutar las acciones del toque, así que `state` ya tiene el valor nuevo.

## Carpeta de aplicaciones

Crea una **carpeta** en el panel de páginas, llénala de botones *Abrir aplicación* (Calculadora, Explorador, tus juegos…) y pon en la página principal un botón con *Abrir página / carpeta*.
