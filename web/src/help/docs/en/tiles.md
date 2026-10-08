# Keys: buttons, sliders and widgets

Every key on the grid is one of these three types (choose it at the top of the inspector, under **Type**):

| Type | For |
|---|---|
| **Button** | Running actions when tapped. It can show text, an icon, an image and change its look depending on its state. |
| **Slider** | A value you set by dragging: volume, brightness, the volume of an OBS source… |
| **Widget** | Showing information: text, a graph over time or a circular gauge. It can have actions too. |

## Look

**Look** tab of the inspector:

- **Text**: several lines allowed, and [dynamic text](#variables) such as `{{round(sys.cpu)}}%`.
- **Text size** and **position** (top, center, bottom). The size is relative to the key, so it looks the same on a phone and on a tablet.
- **Icon**: use the button to search thousands of icons (Material Design and Lucide), e.g. `play`, `mic`, `volume`, `light`… You can also type the name directly, for example `mdi:microphone`.
- **Image**: upload a PNG, JPG, animated GIF or SVG, paste a URL, or use a variable such as `{{media.art}}` for the album art of the song that is playing. When the image is empty (nothing playing, for instance), the icon is shown.
- **Colors**: background, text and icon. Empty = the profile theme's.

## Size

**Size** tab: height (rows) and width (columns). A 2×1 key is perfect for a graph; a slider 3 rows tall is comfortable for volume.

## Sliders

In the **Size** tab of a slider:

- **Min, max and step**: for example 0, 100 and 1.
- **Orientation**: vertical or horizontal.
- **Value to show**: an expression with the real position, for example `audio.volume`. That way, if you change the volume in Windows, the slider on the phone moves by itself.
- Bar **color**.

Its actions go in **On slider move**, and the chosen value is in the `value` variable:

```
Action: Set volume · Target: Speakers · Value: {{value}}
```

> OVSD sends changes while you drag without flooding the PC, and always sends the final value when you let go.

## Widgets

In the **Size** tab of a widget choose the **type**:

- **Text**: just shows its text (handy for a clock: `{{time.hhmm}}`).
- **Graph**: plots a **numeric value** (for example `sys.cpu`) over the last N seconds (**Samples**), between a min and a max.
- **Gauge**: an arc that fills up with the value, like a rev counter.

Combine them with text: a 2×1 graph with the text `CPU {{round(sys.cpu)}}%` at the top looks great.

## States

A key can change its look depending on its state: toggle on/off, microphone muted, OBS scene active… See [Visual states](#states).
