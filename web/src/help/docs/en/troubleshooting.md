# Troubleshooting

## The phone doesn't connect or the page doesn't load

1. **Same network**: the phone must be on the same WiFi as the PC. It doesn't work over mobile data, or on a guest network that isolates devices.
2. **Firewall**: in the editor, **Settings → This PC → Connections from your network** must be green. If not, press **Allow connections** and accept the Windows prompt.
3. **Public network**: if Windows marked your WiFi as *public*, it blocks incoming connections. *Settings → This PC* detects it and offers **Mark network as private** (only on trusted networks, such as your home one).
4. **Right address**: the PC may have several addresses (VPN, virtual machines…). In *Connect device* pick from the drop-down the one on the same network as the phone (usually `192.168.x.x`).
5. Is OVSD running? Look for its icon in the tray.

## It says "Device not paired"

The device was unpaired or you cleared the browser data. Pair it again: *Connect device* on the PC and enter the PIN.

## The phone screen turns off

Menu **⋮ → Screen stays on** must be on. After opening the deck you need to **tap the screen once** for the browser to allow it. On some phones, extreme battery saving prevents it: turn it off for the browser.

## Keys don't reach a program or game

- **Programs running as administrator**: Windows doesn't let a normal program send keys to one running as administrator. Run that program without administrator rights.
- **Games**: some games ignore simulated presses of certain keys. Try `F13`–`F24` shortcuts bound inside the game, or combinations with `Ctrl`/`Alt`.
- **Press too short**: if a game doesn't detect the key, use *Hold keys down*, *Wait* 50 ms and *Release keys*.

## A button is slow to respond

If the key has **Long press** or **Double tap** actions, a normal tap waits to tell them apart. Remove those gestures or shorten the timings in *Settings → Deck*.

## The album art or song title doesn't show

It only works with players that integrate with the Windows media controls (the ones that appear in the Windows volume flyout). Spotify, browsers, Apple Music and most others do.

## OBS, MQTT or Discord shows "Error"

Look at the message next to the indicator in *Settings*. Usually: wrong password, the program isn't open, or the address/port is wrong. OVSD keeps retrying by itself.

## CPU temperature is empty

Turn it on in *Settings → This PC → CPU temperature*. If it says the driver is missing or the service is stopped, turn it off and on again.

## Another program uses port 7341

OVSD tells you when it starts. Change `Port` in the `appsettings.json` file next to `OVSD.exe` and pair the devices again with the new address.

## Where to look when something fails

Right-click the tray icon → **Open data folder** → `logs` folder. The `ovsd.log` file records errors from actions and integrations.
