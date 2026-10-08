# Visual states

A state makes a key change its look: a microphone that turns red when muted, an OBS button that shows "REC", a light that switches on… Set it up in the **State** tab of the inspector.

## The three modes

| Mode | How it works |
|---|---|
| **None** | The key always looks the same. |
| **Toggle** | Each tap switches between off and **on**. You define how it looks when "on". |
| **From expression** | The state comes from an [expression](#variables) that OVSD keeps watching. |

## Toggle

Useful for things OVSD can't know by itself (for example a program's "night mode"). The state is stored in the `toggle.<id>` variable. The **State** tab shows you the exact name, for example `toggle.khgw85mg5x`, so you can use it in conditions of other keys.

To change the toggle from another key or a macro, use the **Set toggle state** action (enter the key id, the part after `toggle.`, or leave it empty for the key itself).

## From expression

The expression is re-evaluated whenever its variables change:

- If it returns **true / false**, the key uses the **on** look or the normal one.
- If it returns **text**, the state with that name is used. So a single key can have many looks.

```
audio.mic.muted                    → on while the mic is muted
obs.recording                      → on while recording
sys.cpu > 80                       → on when CPU is above 80 %
obs.scene                          → states "Game", "Chat", "Break"… by scene
```

### Several named states

1. Choose **From expression** and type, for example, `obs.scene`.
2. In **new state** type `Game` and add it; repeat with `Chat` and `Break`.
3. Select each state and define its look (color, icon, text…).

When the OBS scene changes, the key shows the look of the matching state, or the normal one if none matches.

## What each state can change

Anything in the look: text, icon, image, colors, text size and position. **Whatever you leave empty is inherited** from the normal look, so usually changing the background and the icon is enough.

> States work on sliders and widgets too. For example, a temperature widget that turns red with `sys.cpu.temp > 80`.
