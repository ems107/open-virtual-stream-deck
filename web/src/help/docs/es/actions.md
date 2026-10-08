# Acciones y macros

Las acciones son lo que hace una tecla. Se configuran en la pestaña **Acciones** del inspector.

## Gestos

Cada gesto tiene su propia lista de acciones:

| Gesto | Cuándo se ejecuta |
|---|---|
| **Pulsar** | Un toque normal. |
| **Mantener pulsado** | Al mantener el dedo (0,5 s por defecto, ajustable en *Ajustes*). |
| **Doble toque** | Dos toques seguidos. |
| **Al presionar** | Justo al tocar, sin esperar. |
| **Al soltar** | Justo al levantar el dedo. |
| **Al mover el deslizador** | Mientras arrastras un deslizador (valor en `value`). |

Por ejemplo, un botón de música puede *reproducir/pausar* al pulsar, pasar a la *siguiente* con doble toque y *detener* al mantener.

## Pasos

Pulsa **Añadir paso** para construir una secuencia (macro). Hay cinco tipos de paso:

| Paso | Qué hace |
|---|---|
| **Acción** | Ejecuta una acción: atajo de teclado, abrir programa, multimedia, OBS… (ver [todas las acciones](#reference-actions)). |
| **Esperar** | Pausa unos milisegundos antes del siguiente paso. |
| **Si… entonces** | Ejecuta unos pasos u otros según una [condición](#variables). |
| **Asignar variable** | Guarda un valor en una variable, por ejemplo `user.modo`. |
| **Repetir** | Repite unos pasos N veces. Dentro, `index` vale 0, 1, 2… |

Los pasos se ejecutan en orden. Puedes subirlos, bajarlos, borrarlos y meter pasos dentro de *Si* y *Repetir*.

### Ejemplo: abrir el escritorio de streaming

```
1. Acción  · Abrir aplicación  · C:\Program Files\obs-studio\bin\64bit\obs64.exe
2. Esperar · 3000 ms
3. Acción  · Cambiar escena (OBS) · Empezando
4. Acción  · Mostrar mensaje · «Listo para emitir»
```

### Ejemplo: alternar entre dos modos

```
Si  user.modo == 'juego'
    Asignar variable  user.modo = 'trabajo'
    Acción  · Cambiar de perfil · Trabajo
Si no
    Asignar variable  user.modo = 'juego'
    Acción  · Cambiar de perfil · Juego
```

## Textos dinámicos en las opciones

Los campos de texto de las acciones aceptan variables (cuando están vacíos verás «admite {{plantillas}}»). Por ejemplo, la acción *Escribir texto* con `Son las {{time.hhmm}}` escribe la hora actual, y *Petición HTTP* puede enviar `{"volumen": {{value}}}`.

## Si se pulsa otra vez mientras se ejecuta

Para macros largas, el ajuste **Si se vuelve a activar mientras se ejecuta** decide qué pasa:

| Opción | Comportamiento |
|---|---|
| **Ejecutar en paralelo** | Cada pulsación se ejecuta por su cuenta (por defecto). |
| **Ignorar** | Mientras se está ejecutando, las nuevas pulsaciones no hacen nada. |
| **Reiniciar** | Cancela la ejecución en curso y empieza de nuevo. |
| **Poner en cola** | Se ejecutan una detrás de otra. |

## Atajos de teclado

La acción **Atajo de teclado** tiene un botón **Grabar**: púlsalo y luego la combinación en el teclado del PC. También puedes escribirla, por ejemplo `Ctrl+Shift+M`. Varias combinaciones separadas por espacios se pulsan seguidas. Consulta los [nombres de teclas](#reference-keys).

> **Truco**: usa teclas que nadie usa, como `F13`–`F24` o `Ctrl+Alt+Shift+F1`, y asígnalas como atajo en tus programas (OBS, Discord, juegos…). Así el botón funciona aunque el programa no esté en primer plano.

Para **pulsar para hablar**: en *Al presionar* pon **Mantener teclas pulsadas** y en *Al soltar* **Soltar teclas**, con la misma combinación.

## Comandos

**Ejecutar comando** lanza un comando de PowerShell o cmd (o un ejecutable). Su salida puede guardarse en una variable (*Guardar resultado en la variable*) para mostrarla en una tecla:

```
Comando: (Get-Date).DayOfYear
Guardar resultado en la variable: user.dia
Texto de la tecla: Día {{user.dia}}
```

> Las acciones se ejecutan en tu PC con tu usuario. Solo los dispositivos emparejados pueden lanzarlas.
