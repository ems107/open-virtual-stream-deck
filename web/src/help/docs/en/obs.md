# OBS Studio

OVSD connects to OBS through its WebSocket server (built into OBS 28 and later) to switch scenes, stream, record, mute sources… and show its state live.

## Connecting

1. In OBS: **Tools → WebSocket Server Settings**.
2. Tick **Enable WebSocket server**. Keep port `4455`.
3. If *Enable Authentication* is ticked, press **Show Connect Info** and copy the **password**.
4. In OVSD: **Settings → OBS Studio** → tick **Enabled**, keep the URL `ws://127.0.0.1:4455` (if OBS is on the same PC), paste the password and press **Save**.

The indicator next to the title turns **Connected**. OVSD reconnects by itself if you close and reopen OBS.

> OBS on another PC? Use `ws://IP-of-that-PC:4455` and allow OBS through that PC's firewall.

## Actions

| Action | Options |
|---|---|
| **Switch scene** | The scene list is loaded from OBS. |
| **Streaming** / **Recording** | Toggle, start or stop (recording can also pause/resume). |
| **Replay buffer** | Toggle, start, stop or **save replay**. |
| **Virtual camera** | Toggle, start or stop. |
| **Mute audio input** | Mic, desktop audio… toggle or set. |
| **Audio input volume** | 0–100; ideal on a slider with `{{value}}`. |
| **Show / hide source** | Scene + source; toggle, show or hide. |
| **Studio mode** / **Studio transition** | For those who use preview and program. |
| **Custom request** | Any obs-websocket v5 request, with its response stored in a variable. |

## Variables for the state

| Variable | Value |
|---|---|
| `obs.connected` | OVSD is connected to OBS |
| `obs.scene` | Name of the current scene |
| `obs.streaming`, `obs.recording`, `obs.recordPaused` | Streaming, recording, recording paused |
| `obs.replay`, `obs.virtualcam`, `obs.studio` | Replay buffer, virtual camera, studio mode active |
| `obs.preview` | Preview scene (studio mode) |
| `obs.mute.<input>` | Whether an audio input is muted, e.g. `[obs.mute.Mic/Aux]` |
| `obs.volume.<input>` | Volume of an input (0–100) |
| `obs.visible.<scene>.<source>` | Whether a source is visible |

> Names with spaces or slashes go in brackets in expressions: `[obs.mute.Desktop Audio]`.

## Examples

**Scene button that lights up when active** (one per scene):

```
Tap:   Switch scene · Game
State: From expression · obs.scene == 'Game'   (on: green background)
```

**Recording button that turns red**:

```
Text:  {{obs.recording ? '● REC' : 'Record'}}
Tap:   Recording · Toggle
State: From expression · obs.recording        (on: red background)
```

**Save the last minute** (replay buffer running in OBS):

```
Tap: Replay buffer · Save replay
     Show message · "Clip saved"
```
