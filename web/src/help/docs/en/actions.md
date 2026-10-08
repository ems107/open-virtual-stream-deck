# Actions and macros

Actions are what a key does. You set them up in the **Actions** tab of the inspector.

## Gestures

Each gesture has its own list of actions:

| Gesture | When it runs |
|---|---|
| **Tap** | A normal touch. |
| **Long press** | When you hold your finger (0.5 s by default, adjustable in *Settings*). |
| **Double tap** | Two quick taps. |
| **On press** | The moment you touch, with no waiting. |
| **On release** | The moment you lift your finger. |
| **On slider move** | While dragging a slider (value in `value`). |

For example, a music button can *play/pause* on tap, skip to the *next* track on double tap and *stop* on long press.

## Steps

Press **Add step** to build a sequence (macro). There are five kinds of step:

| Step | What it does |
|---|---|
| **Action** | Runs an action: keyboard shortcut, open a program, media, OBS… (see [all actions](#reference-actions)). |
| **Wait** | Pauses a few milliseconds before the next step. |
| **If… then** | Runs some steps or others depending on a [condition](#variables). |
| **Set variable** | Stores a value in a variable, for example `user.mode`. |
| **Repeat** | Repeats some steps N times. Inside, `index` is 0, 1, 2… |

Steps run in order. You can move them up and down, delete them and nest steps inside *If* and *Repeat*.

### Example: start the streaming setup

```
1. Action · Open app or file · C:\Program Files\obs-studio\bin\64bit\obs64.exe
2. Wait   · 3000 ms
3. Action · Switch scene (OBS) · Starting
4. Action · Show message · "Ready to stream"
```

### Example: switch between two modes

```
If  user.mode == 'game'
    Set variable  user.mode = 'work'
    Action · Switch profile · Work
Else
    Set variable  user.mode = 'game'
    Action · Switch profile · Games
```

## Dynamic text in options

Text fields of actions accept variables (when empty they show "supports {{templates}}"). For example, *Type text* with `It is {{time.hhmm}}` types the current time, and *HTTP request* can send `{"volume": {{value}}}`.

## When triggered again while running

For long macros, the **When triggered again while running** setting decides what happens:

| Option | Behavior |
|---|---|
| **Run in parallel** | Each tap runs on its own (default). |
| **Ignore** | While it's running, new taps do nothing. |
| **Restart** | Cancels the current run and starts again. |
| **Queue** | They run one after another. |

## Keyboard shortcuts

The **Hotkey** action has a **Record** button: press it, then the combination on the PC keyboard. You can also type it, for example `Ctrl+Shift+M`. Several combinations separated by spaces are pressed one after another. See the [key names](#reference-keys).

> **Tip**: use keys nobody uses, such as `F13`–`F24` or `Ctrl+Alt+Shift+F1`, and assign them as shortcuts in your programs (OBS, Discord, games…). That way the button works even when the program isn't in the foreground.

For **push to talk**: in *On press* add **Hold keys down** and in *On release* **Release keys**, with the same combination.

## Commands

**Run command** runs a PowerShell or cmd command (or an executable). Its output can be stored in a variable (*Store output in variable*) to show it on a key:

```
Command: (Get-Date).DayOfYear
Store output in variable: user.day
Key text: Day {{user.day}}
```

> Actions run on your PC with your user account. Only paired devices can trigger them.
