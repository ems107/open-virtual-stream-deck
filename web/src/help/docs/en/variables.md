# Variables and dynamic text

Variables are live values OVSD keeps up to date: the time, CPU usage, the song that is playing, the OBS scene, your own counters… You can **show** them on keys and **use** them in conditions and states.

> The editor's **Variables** tab lists every variable that exists right now with its real value. It's the best way to discover what you have available.

## Showing variables on a key

Write an expression between double braces in any text. It updates by itself whenever it changes:

```
{{time.hhmm}}
CPU {{round(sys.cpu)}}%
{{media.title}} — {{media.artist}}
{{obs.recording ? '● REC' : ''}}
{{sys.net.down.text}} ↓
```

It also works in the **image** (`{{media.art}}`), the **icon** and the **colors**:

```
{{sys.cpu > 80 ? '#c0392b' : '#1f232b'}}
```

## Expressions

The conditions of *If… then*, states and the *Set variable* step use expressions (without braces):

| Element | Examples |
|---|---|
| Text and numbers | `'Game'`, `"hello"`, `42`, `3.5`, `true`, `false` |
| Variables | `sys.cpu`, `obs.scene`, `user.counter` |
| Odd names in brackets | `[mqtt.home/living/temperature]` |
| Comparison | `==`  `!=`  `<`  `<=`  `>`  `>=` |
| Logic | `&&` `||` `!` (or `and`, `or`, `not`) |
| Arithmetic | `+` `-` `*` `/` `%` (with text, `+` joins) |
| Conditional | `condition ? yes : no` |
| Default value | `user.name ?? 'no name'` |
| Functions | `round(sys.cpu)`, `contains(app.title, 'YouTube')` ([all of them](#reference-functions)) |

```
obs.scene == 'Game' && !audio.mic.muted
sys.cpu > 80 ? 'high' : 'ok'
default(user.counter, 0) + 1
```

## Available variables

| Group | Variables |
|---|---|
| `time.*` | `hhmm`, `hhmmss`, `hh`, `mm`, `ss`, `date`, `weekday`, `day`, `month`, `year`, `unix` |
| `sys.*` | `cpu`, `ram`, `ram.used`, `ram.total`, `gpu`, `gpu.temp`, `gpu.mem`, `gpu.name`, `cpu.temp`, `cpu.power`, `cpu.clock`, `net.down`, `net.up`, `net.down.text`, `net.up.text`, `uptime` |
| `audio.*` | `volume`, `muted`, `device`, `mic.volume`, `mic.muted`, `mic.device` |
| `media.*` | `title`, `artist`, `album`, `art`, `playing`, `status`, `app` |
| `app.*` | `active` (process of the active window, e.g. `chrome`), `title` |
| `obs.*` | `connected`, `scene`, `streaming`, `recording`, `recordPaused`, `replay`, `virtualcam`, `studio`, `preview`, `mute.<input>`, `volume.<input>`, `visible.<scene>.<source>` |
| `discord.*` | `connected`, `mute`, `deaf` |
| `mqtt.*` | `connected` and `mqtt.<topic>` with the last message of each subscribed topic |
| `webhook.*` | `webhook.<name>` and `webhook.<name>.time` |
| `toggle.*` | the state of each [toggle](#states) |
| `user.*` | **your variables** (see below) |
| `value` | a slider's value, inside its actions |
| `state` | the key's [state](#states) (`on`, `off` or its name), inside its actions |
| `index` | the current round inside a *Repeat* step |

The CPU temperature ones need turning on in *Settings → This PC* (see [Windows](#windows)).

## Your own variables

Any variable starting with `user.` is yours: create it with the **Set variable** step and **it is kept across restarts**.

Counter example: the button shows `{{default(user.coffees, 0)}} ☕` and on tap:

```
Set variable  user.coffees = default(user.coffees, 0) + 1
```

And with **Long press** you reset it: `user.coffees = 0`.

> The value of *Set variable* is an expression: to store text, put it in quotes (`'hello'`).
