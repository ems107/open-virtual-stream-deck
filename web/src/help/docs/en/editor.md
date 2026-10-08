# The editor

The editor opens in the PC browser (double-click the tray icon). It has five tabs: **Profiles**, **Devices**, **Settings**, **Variables** and **Help**.

## The Profiles screen

```
┌ bar: profile ▾ · rows × columns · Profile · undo/redo · ⋯ ───────────────────┐
│ Pages          │          Grid (live preview)                 │  Inspector   │
│ ▸ Home         │   ┌───┬───┬───┬───┐                          │  Look        │
│   ▸ Apps       │   │   │   │   │   │                          │  Actions     │
│ + page         │   └───┴───┴───┴───┘                          │  State       │
│                │                                              │  Size        │
└────────────────┴──────────────────────────────────────────────┴──────────────┘
```

- **Top bar**: pick the profile, change **rows × columns**, open the **Profile** settings (name, theme and automatic switching) and the other options: duplicate, export/import `.zip`, previous versions and delete.
- **Pages** (left): the screens of the profile. See below.
- **Grid** (center): what the phone will show, with real live values.
- **Inspector** (right): the properties of the selected key.

Changes **save automatically** (you'll see "Saved" in the bar) and reach the devices showing that profile instantly.

## Working with the grid

| You want to… | Do this |
|---|---|
| Create a key | Click an empty cell → *Add button / slider / widget* |
| Edit it | Click the key and use the inspector |
| Move it or swap two | Drag it |
| Move it to another page | Drag it onto the page in the left panel |
| Copy / cut / paste | `Ctrl+C` or `Ctrl+X` on the key; then select an empty cell and `Ctrl+V` (also on another page or profile) |
| Duplicate | `Ctrl+D` |
| Delete | `Del` |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` |

A key can span several cells: **Size** tab → height (rows) and width (columns).

> If you reduce the number of rows or columns, OVSD warns you about the keys that would fall outside before removing them.

## Pages and folders

A profile can have several pages:

- **New page**: another independent screen.
- **New folder inside**: a "child" page. When it opens, the deck adds an automatic **back** key in the top-left corner.
- **Use as home**: the page shown when the profile opens.

To open a page from a button use the **Open page / folder** action (*Deck* category). There's also **Go back** and **Go to home page**.

## Profile settings

**Profile** button in the bar:

- Profile **name**.
- **Theme**: deck background, default key background and text color, gap and corner radius.
- **Automatic switching**: rules so the profile appears by itself when you use a certain app (see [Profiles and devices](#profiles)).

## Keeping your changes safe

- **Undo** as much as you want during the session.
- **Previous versions**: every save keeps the previous version (up to 50 per profile). Menu ⋯ → *Previous versions* → *Restore*.
- If you edit the same profile from two places at once (PC and phone), OVSD detects the conflict and lets you choose which version to keep.

More in [Backups](#backups).
