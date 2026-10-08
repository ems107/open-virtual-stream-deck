# Discord

There are two ways to control Discord. The first needs no setup; the second also shows the real state.

## Option 1: keyboard shortcuts (best to start with)

Discord lets you assign global shortcuts that work even when it's not in the foreground.

1. In Discord: **User Settings → Keybinds → Add a Keybind**.
2. Choose **Toggle Mute** and press a combination you don't use, for example `Ctrl+Alt+Shift+M` (or `F13`, if you record it from OVSD).
3. Repeat with **Toggle Deafen** (`Ctrl+Alt+Shift+D`).
4. In OVSD, create a button with the **Hotkey** action and that combination.

You can also use Discord's **Push to Talk** with the *Hold keys down* (on press) and *Release keys* (on release) actions.

Drawback: OVSD doesn't know whether you're muted. If you mute from Discord, the button doesn't reflect it.

## Option 2: direct connection (real state)

OVSD can talk to the Discord desktop app to mute/deafen and know at all times whether you are (`discord.mute`, `discord.deaf`). Discord requires every user to register their own "application" for this:

1. Go to [discord.com/developers/applications](https://discord.com/developers/applications) with your account and press **New Application** (any name, for example "OVSD").
2. In **OAuth2**, copy the **Client ID** and press **Reset Secret** to get the **Client Secret**.
3. In **OAuth2 → Redirects**, add `http://localhost` and save.
4. In OVSD: **Settings → Discord** → tick **Enabled**, paste the Client ID and Client Secret, keep the redirect `http://localhost` and press **Save**.
5. Discord shows a window asking for permission: accept it. It's only asked the first time.

Now you have:

- The **Discord mute** and **Discord deafen** actions (toggle, on, off).
- The variables `discord.connected`, `discord.mute` and `discord.deaf`.

```
Tap:   Discord mute · Toggle
State: From expression · discord.mute     (on: red background, microphone-off icon)
```

> If you switch accounts or want to revoke the permission, use **Forget authorization** in *Settings → Discord*.
