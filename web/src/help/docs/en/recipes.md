# Recipes

Ready-to-build examples. Each one says what to put in each tab of the inspector.

## Microphone with state

```
Look:   Text "Mic" · Icon mdi:microphone
Tap:    Mute · Microphone · Toggle
State:  From expression · audio.mic.muted
        on → Background #7a1f1f · Icon mdi:microphone-off · Text "Muted"
```

## Push to talk

```
On press:   Hold keys down · F13
On release: Release keys · F13
```

Set `F13` as the *push to talk* key in Discord, the game or whatever app you use (record it from OVSD if your keyboard has no F13).

## PC monitor

One 2×1 **Graph** widget per metric:

```
CPU:   Value sys.cpu   · Text "CPU {{round(sys.cpu)}}%" at the top · Color #3ecf8e
GPU:   Value sys.gpu   · Text "GPU {{round(sys.gpu)}}% · {{sys.gpu.temp}}°"
RAM:   Value sys.ram   · Text "RAM {{sys.ram.used}} / {{sys.ram.total}} GB"
```

And a state as an alert: `sys.cpu.temp > 85` → red background.

## Music player

```
Image: {{media.art}} · Icon mdi:music · Text {{truncate(media.title, 18)}}
Tap: Play / pause
Double tap: Next track
Long press: Previous track
```

Next to it, a vertical **slider** 3 rows tall: *Value to show* `audio.volume`, *On slider move*: *Set volume · Speakers · {{value}}*.

## Volume of a single app

To turn down only the game or only Discord: *Set volume · Target: Application · Application: Discord · Value: {{value}}* on a slider, or `-10` / `+10` on two buttons.

## Switch between speakers and headphones

```
If   audio.device == 'Speakers (Realtek)'
     Set default audio device · Headphones
Else
     Set default audio device · Speakers (Realtek)
Text: {{contains(audio.device, 'Headphones') ? '🎧' : '🔊'}}
```

(Copy the exact names of `audio.device` from the Variables tab.)

## Streaming panel (OBS)

- One key per scene with *Switch scene* and state `obs.scene == 'Name'`.
- **Go live**: *Streaming · Toggle*, text `{{obs.streaming ? 'LIVE' : 'Go live'}}`, state `obs.streaming`.
- **Record**: the same with `obs.recording`.
- **Clip**: *Replay buffer · Save replay*.
- OBS **mic volume** slider: *Audio input volume · Mic/Aux · {{value}}*, value to show `[obs.volume.Mic/Aux]`.

See [OBS Studio](#obs).

## Counter

```
Text:       {{default(user.counter, 0)}}
Tap:        Set variable · user.counter = default(user.counter, 0) + 1
Long press: Set variable · user.counter = 0
```

## Type frequent texts

*Type text* with your email, a signature or a long command. With variables: `Today is {{time.date}}`.

## "Do not disturb" mode

A toggle that mutes Discord and pauses the music when turned on, and restores them when turned off:

```
State: Toggle (on: purple background, icon mdi:moon-waning-crescent)
Tap:
  If   state == 'on'
       Discord mute · On
       Play / pause
  Else
       Discord mute · Off
       Play / pause
```

> Inside a key's actions, `state` is its current state (`on`, `off` or the state name). A toggle flips before the tap's actions run, so `state` already has the new value.

## Apps folder

Create a **folder** in the pages panel, fill it with *Open app or file* buttons (Calculator, Explorer, your games…) and put a button with *Open page / folder* on the main page.
