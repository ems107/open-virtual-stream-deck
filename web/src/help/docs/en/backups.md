# Backups

## Where your data lives

Everything is stored in `%APPDATA%\OVSD` (right-click the tray icon → **Open data folder**):

| Folder / file | Contents |
|---|---|
| `profiles` | Your profiles, one file each |
| `media` | The images you uploaded |
| `backups` | Previous versions of each profile |
| `config.json` | Settings, paired devices and integrations |
| `variables.json` | Your `user.*` variables |
| `logs` | Activity and error log |

For a full backup, or to move OVSD to another PC, **copy that whole folder** (with OVSD closed) and paste it in the same place on the other PC.

## Previous versions

Every time a profile is saved, OVSD keeps the previous version (up to 50). If you make a mistake or delete something:

**Profiles → ⋯ menu → Previous versions → Restore**.

A copy is also kept when you **delete** a profile: in that same dialog, the **Deleted profiles** section lets you bring it back.

## Exporting and importing profiles

**Profiles → ⋯ menu → Export (.zip)** creates a file with the profile and all its images. Use it to:

- Keep a copy of a specific profile.
- Share it with someone else.
- Move it to another PC with OVSD.

**Import (.zip)** adds it as a new profile; it never overwrites yours.

> Exported profiles don't include your settings or passwords (OBS, MQTT, Discord), only the panel.
