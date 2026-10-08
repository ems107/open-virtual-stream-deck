# El editor

El editor se abre en el navegador del PC (doble clic en el icono de la bandeja). Tiene cinco pestañas: **Perfiles**, **Dispositivos**, **Ajustes**, **Variables** y **Ayuda**.

## La pantalla de Perfiles

```
┌ barra: perfil ▾ · filas × columnas · Perfil · deshacer/rehacer · ⋯ ─────────┐
│ Páginas        │        Rejilla (vista previa en vivo)        │  Inspector   │
│ ▸ Inicio       │   ┌───┬───┬───┬───┐                          │  Aspecto     │
│   ▸ Apps       │   │   │   │   │   │                          │  Acciones    │
│ + página       │   └───┴───┴───┴───┘                          │  Estado      │
│                │                                              │  Tamaño      │
└────────────────┴──────────────────────────────────────────────┴──────────────┘
```

- **Barra superior**: elige el perfil, cambia **filas × columnas**, abre los ajustes del **Perfil** (nombre, tema y cambio automático) y el resto de opciones: duplicar, exportar/importar `.zip`, versiones anteriores y eliminar.
- **Páginas** (izquierda): las pantallas del perfil. Ver más abajo.
- **Rejilla** (centro): lo que verá el móvil, con los valores reales en directo.
- **Inspector** (derecha): las propiedades de la tecla seleccionada.

Los cambios **se guardan solos** (verás «Guardado» en la barra) y llegan al instante a los dispositivos que muestran ese perfil.

## Trabajar con la rejilla

| Quieres… | Haz esto |
|---|---|
| Crear una tecla | Clic en un hueco → *Añadir botón / deslizador / widget* |
| Editarla | Clic en la tecla y usa el inspector |
| Moverla o intercambiar dos | Arrástrala |
| Moverla a otra página | Arrástrala sobre la página en el panel izquierdo |
| Copiar / cortar / pegar | `Ctrl+C` o `Ctrl+X` sobre la tecla; luego selecciona un hueco y `Ctrl+V` (también en otra página u otro perfil) |
| Duplicar | `Ctrl+D` |
| Eliminar | `Supr` |
| Deshacer / rehacer | `Ctrl+Z` / `Ctrl+Y` |

Una tecla puede ocupar varias casillas: pestaña **Tamaño** → alto (filas) y ancho (columnas).

> Si reduces el número de filas o columnas, OVSD te avisa de las teclas que quedarían fuera antes de borrarlas.

## Páginas y carpetas

Un perfil puede tener varias páginas:

- **Nueva página**: otra pantalla independiente.
- **Nueva carpeta dentro**: una página «hija». Al abrirla, el deck añade en la esquina superior izquierda una tecla **atrás** automática.
- **Usar como inicio**: la página que se muestra al abrir el perfil.

Para ir a una página desde un botón, usa la acción **Abrir página / carpeta** (categoría *Deck*). También tienes **Volver atrás** e **Ir al inicio**.

## Ajustes del perfil

Botón **Perfil** de la barra:

- **Nombre** del perfil.
- **Tema**: fondo del deck, fondo y color de texto por defecto de las teclas, separación y redondeo.
- **Cambio automático**: reglas para que el perfil aparezca solo cuando usas cierta aplicación (ver [Perfiles y dispositivos](#profiles)).

## Seguridad de tus cambios

- **Deshacer** sin límite práctico durante la sesión.
- **Versiones anteriores**: cada guardado conserva la versión previa (hasta 50 por perfil). Menú ⋯ → *Versiones anteriores* → *Restaurar*.
- Si editas el mismo perfil desde dos sitios a la vez (PC y móvil), OVSD detecta el conflicto y te deja elegir qué versión conservar.

Más en [Copias de seguridad](#backups).
