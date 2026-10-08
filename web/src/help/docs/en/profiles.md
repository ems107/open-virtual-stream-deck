# Profiles and devices

## Several profiles

A **profile** is a complete panel, with its grid, theme and pages. Have as many as you like: one for everyday use, one for gaming, one for streaming, one for video editing…

In **Profiles**, the drop-down in the top bar switches profile; you can create a new one there or, in the ⋯ menu, **duplicate** the current one to use it as a template.

## Which profile each device shows

The **Devices** tab lists your paired phones and tablets. For each one you can choose:

- The device **name**.
- **Profile**: the one it normally shows. If you choose none, it uses the *default profile* from *Settings*.
- **Automatic profile from the active app**: turns on automatic switching (see below).

So the desk tablet can show the streaming panel while the phone shows the music one, at the same time.

You can also switch profile from the phone itself: menu **⋮ → Profiles**.

## Automatic switching by application

Each profile can have **rules**: *Profile → Automatic switching → Add rule*. A rule matches when the PC's active application meets:

- **Process**: the executable name, with or without `.exe`, for example `obs64`, `chrome`, `Photoshop`. `*` works as a wildcard: `game*`, `*steam*`. Not case sensitive.
- **Title contains…**: part of the window title, for example `YouTube`.

Fill in either one or both (then both must match). A profile can have several rules: one matching is enough.

Devices with **Automatic profile** turned on switch to that profile when a rule matches, and **go back to their own profile** when none matches any more.

> To find an app's process name, open it, bring it to the front and look at the `app.active` variable in the **Variables** tab.

### Example

| Profile | Rule |
|---|---|
| Streaming | Process `obs64` |
| Games | One rule per game: `eldenring`, `cs2`, `RocketLeague`… |
| Browser | Two rules: process `chrome` and process `firefox` |
| YouTube | Process `chrome` and title contains `YouTube` |

## Switching profile from a button

The **Switch profile** action (*Deck* category) shows another profile on the device where you tap it. Handy for a "Games" button in your main profile and a "Back" button in the games one.

If the device has automatic switching on, the chosen profile stays until a rule picks another one.
