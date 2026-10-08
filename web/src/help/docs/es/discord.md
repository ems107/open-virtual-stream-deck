# Discord

Hay dos formas de controlar Discord. La primera no necesita configurar nada; la segunda además muestra el estado real.

## Opción 1: atajos de teclado (recomendada para empezar)

Discord permite asignar atajos globales que funcionan aunque no esté en primer plano.

1. En Discord: **Ajustes de usuario → Atajos de teclado → Añadir un atajo**.
2. Elige **Activar/desactivar silencio** y pulsa una combinación que no uses, por ejemplo `Ctrl+Alt+Shift+M` (o `F13`, si la grabas desde OVSD).
3. Repite con **Activar/desactivar ensordecer** (`Ctrl+Alt+Shift+D`).
4. En OVSD, crea un botón con la acción **Atajo de teclado** y esa combinación.

También puedes usar **Pulsar para hablar** de Discord con las acciones *Mantener teclas pulsadas* (al presionar) y *Soltar teclas* (al soltar).

Inconveniente: OVSD no sabe si estás silenciado. Si silencias desde Discord, el botón no lo refleja.

## Opción 2: conexión directa (estado real)

OVSD puede hablar con la aplicación de escritorio de Discord para silenciar/ensordecer y saber en todo momento si lo estás (`discord.mute`, `discord.deaf`). Discord exige que cada usuario registre su propia «aplicación» para esto:

1. Entra en [discord.com/developers/applications](https://discord.com/developers/applications) con tu cuenta y pulsa **New Application** (cualquier nombre, por ejemplo «OVSD»).
2. En **OAuth2**, copia el **Client ID** y pulsa **Reset Secret** para obtener el **Client Secret**.
3. En **OAuth2 → Redirects**, añade `http://localhost` y guarda.
4. En OVSD: **Ajustes → Discord** → marca **Activado**, pega Client ID y Client Secret, deja la redirección `http://localhost` y pulsa **Guardar**.
5. Discord mostrará una ventana pidiendo permiso: acéptala. Solo se pide la primera vez.

Ahora tienes:

- Acciones **Silenciar en Discord** y **Ensordecer en Discord** (alternar, activar, desactivar).
- Variables `discord.connected`, `discord.mute` y `discord.deaf`.

```
Pulsar: Silenciar en Discord · Alternar
Estado: Según expresión · discord.mute     (on: fondo rojo, icono microphone-off)
```

> Si cambias de cuenta o quieres revocar el permiso, usa **Olvidar autorización** en *Ajustes → Discord*.
