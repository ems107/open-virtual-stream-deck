# Perfiles y dispositivos

## Varios perfiles

Un **perfil** es un panel completo, con su rejilla, su tema y sus páginas. Puedes tener tantos como quieras: uno para el día a día, otro para jugar, otro para emitir, otro para editar vídeo…

En **Perfiles**, el desplegable de la barra superior cambia de perfil; ahí mismo puedes crear uno nuevo o, en el menú ⋯, **duplicar** el actual para usarlo como plantilla.

## Qué perfil ve cada dispositivo

En la pestaña **Dispositivos** aparecen tus móviles y tablets emparejados. Para cada uno puedes elegir:

- **Nombre** del dispositivo.
- **Perfil**: el que muestra normalmente. Si no eliges ninguno, usa el *perfil predeterminado* de *Ajustes*.
- **Perfil automático según la app activa**: activa el cambio automático (ver abajo).

Así, la tablet del escritorio puede mostrar el panel de streaming mientras el móvil muestra el de música, ambos a la vez.

Desde el propio móvil también puedes cambiar de perfil: menú **⋮ → Perfiles**.

## Cambio automático según la aplicación

Cada perfil puede tener **reglas**: *Perfil → Cambio automático → Añadir regla*. Una regla coincide cuando la aplicación activa del PC cumple:

- **Proceso**: el nombre del ejecutable, con o sin `.exe`, por ejemplo `obs64`, `chrome`, `Photoshop`. Admite `*` como comodín: `game*`, `*steam*`. No distingue mayúsculas.
- **El título contiene…**: parte del título de la ventana, por ejemplo `YouTube`.

Puedes rellenar uno de los dos o ambos (entonces tienen que cumplirse los dos). Un perfil puede tener varias reglas: basta con que coincida una.

Los dispositivos con **Perfil automático** activado cambian a ese perfil cuando la regla coincide, y **vuelven a su perfil** cuando ya no coincide ninguna.

> Para saber el nombre de proceso de una aplicación, ábrela, ponla en primer plano y mira la variable `app.active` en la pestaña **Variables**.

### Ejemplo

| Perfil | Regla |
|---|---|
| Streaming | Proceso `obs64` |
| Juegos | Una regla por juego: `eldenring`, `cs2`, `RocketLeague`… |
| Navegador | Dos reglas: proceso `chrome` y proceso `firefox` |
| YouTube | Proceso `chrome` y título contiene `YouTube` |

## Cambiar de perfil desde un botón

La acción **Cambiar de perfil** (categoría *Deck*) muestra otro perfil en el dispositivo en el que pulsas. Útil para un botón «Juegos» en tu perfil principal y un botón «Volver» en el de juegos.

Si el dispositivo tiene el cambio automático activado, el perfil elegido se mantiene hasta que una regla elija otro.
