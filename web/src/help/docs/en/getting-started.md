# Getting started

## 1. OVSD is running

While OVSD runs you'll see its icon (four squares) in the **system tray**, next to the Windows clock. If it's not there, check the `^` arrow with the hidden icons.

- **Double-click** the icon: opens this editor.
- **Right-click**: connect a device, start with Windows, open the data folder and exit.

> If you see a yellow notice "Your phones and tablets can't connect yet", press **Allow connections** and accept the Windows prompt. You only need to do this once (see [Troubleshooting](#troubleshooting)).

## 2. Connect your phone or tablet

1. The phone must be on the **same WiFi** as the PC.
2. On the PC: right-click the tray icon → **Connect a device (QR)** (or *Devices → Connect device* in the editor).
3. Scan the QR code with the phone camera and open the link. If you can't scan it, type the address shown under the QR into the phone browser and enter the 6-digit PIN.

The device stays paired: next time just open the same address. To keep it handy, use **Add to Home screen** in the phone browser.

> Only paired devices can press buttons. You can unpair any of them from *Devices*.

## 3. Your first button

Let's make a button that mutes the microphone and turns red while muted.

1. Go to **Profiles** (the first tab of this editor). You'll see the grid of the sample profile.
2. Click an **empty cell** and choose **Add button**.
3. In the right panel, **Appearance** tab: type `Mic` as the text and use the icon button to pick `microphone`.
4. **Actions** tab → *Tap* → **Add step** → **Action** → search for **Mute** (Audio category). Choose *Target: Microphone* and *Mode: Toggle*.
5. **State** tab → **From expression** and type `audio.mic.muted`. In the appearance of the **on** state set a red background and the `microphone-off` icon.

Changes save automatically and show up on the phone instantly. Tap the button and watch it change color.

## 4. What next

- Explore the [sample profile](#editor): each key shows a different feature.
- Learn how to use [the deck on your phone](#on-the-phone): fullscreen, keep the screen on and editing from the phone itself.
- Get ideas from the [recipes](#recipes).
