# Windows: media, audio and system

All of this works with no setup.

## Media

Control any player Windows recognizes (Spotify, YouTube in the browser, Apple Music, VLC…): the same media keys as on your keyboard, plus information about what's playing.

- Actions: **Play / pause**, **Next track**, **Previous track**, **Stop**.
- Variables: `media.title`, `media.artist`, `media.album`, `media.playing`, `media.app` and `media.art` (album art).

A complete music button:

```
Image:  {{media.art}}
Icon:   mdi:play-pause         (shown when there is no album art)
Text:   {{truncate(media.title, 18)}}
Tap:    Play / pause
Double tap: Next track
```

## Audio

- **Set volume**: of the speakers, the microphone or **one specific app** (only Discord, or only the game). Absolute value `0`–`100` or relative with a sign: `+5`, `-5`. On a slider use `{{value}}`.
- **Mute**: mute, unmute or toggle the speakers, microphone or an app.
- **Set default audio device**: switch from speakers to headphones with one button.
- Variables: `audio.volume`, `audio.muted`, `audio.device`, `audio.mic.volume`, `audio.mic.muted`, `audio.mic.device`.

> A volume slider that follows changes made in Windows: *Value to show* `audio.volume`, and in *On slider move*: *Set volume · Speakers · {{value}}*.

## Programs, websites and commands

- **Open app or file**: an `.exe`, a document, a folder… with optional arguments.
- **Open URL**: in the default browser. Also works with app links (`steam://`, `spotify:`…).
- **Run command**: PowerShell, cmd or an executable, optionally hidden, storing the output in a variable.
- **Type text**: types text as if you were typing it (variables allowed).

## System metrics

Updated every second:

| Variable | What it is |
|---|---|
| `sys.cpu` | CPU usage (%) |
| `sys.ram`, `sys.ram.used`, `sys.ram.total` | Memory: % used, GB used and total |
| `sys.gpu`, `sys.gpu.temp`, `sys.gpu.mem` | GPU usage, temperature and memory (*Settings → System metrics*) |
| `sys.net.down`, `sys.net.up` | Network in KB/s (and `.text` formatted: "2.4 MB/s") |
| `sys.cpu.temp`, `sys.cpu.power`, `sys.cpu.clock` | CPU temperature (°C), power (W) and clock (MHz) |
| `sys.uptime` | Seconds since the PC started |

## CPU temperature

Windows only lets a special driver (PawnIO) read the processor sensors, and only a program with administrator rights can use it. So that OVSD doesn't have to run as administrator, it installs a small separate **sensor service** that only reads the temperature and passes it on.

Turn it on in **Settings → This PC → CPU temperature → Turn on**. Windows asks for confirmation once. You can turn it off from there too.

## Active application

`app.active` is the process of the foreground window (`chrome`, `obs64`…) and `app.title` its title. Use it for [automatic profile switching](#profiles) and in conditions, for example to show a different button while you're in the game.
