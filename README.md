# GK2 QuickStack

A small [BepInEx 5](https://github.com/BepInEx/BepInEx) mod for **Graveyard Keeper 2**.

Stand at a chest and a second option appears under **[E] Open**. Press **R** and every item in your inventory that the
chest already holds goes into it, without opening the chest window.

```
[E] Open
[R] Move all identical items
```

It is the chest window's own "move all identical items" button, on a key:

- **Only the chest in front of you.** You choose where things go, chest by chest.
- **Same rules as the button.** The mod calls the game's own function, so the chest's filters, free space and stack
  sizes apply exactly as in the window. Items inside your bags and on your tool belt stay with you.
- **The option only shows when there is something to store.** After storing, the game's own notification list shows
  one line per item that went in: its icon, name and amount.
- **Nothing is created, deleted or written to your save.**

Seed-bag storages, the wine pallet and conveyor chests open their own windows and are left alone.
The option is a keyboard key: it is hidden while the game shows gamepad prompts.

## Install

The game ships with BepInEx since version 1.011.

1. Download `GK2_QuickStack-x.y.z.zip` from the [Releases](https://github.com/focabr/GK2-QuickStack/releases) page.
2. Extract it into the game folder, so the file ends up at
   `Graveyard Keeper 2\BepInEx\plugins\QuickStack\QuickStack.dll`.
3. Start the game.

## Changing the key

The key is **not** in the game's Controls window (that window only lists the game's own keys). It is changed in the
mod's settings file:

1. Close the game.
2. Open `Graveyard Keeper 2\BepInEx\config\com.focabr.gk2.quickstack.cfg` in Notepad
   (the file is created the first time the game runs with the mod).
3. Change the line `StackKey = R` to the key you want, for example `StackKey = G` or `StackKey = Mouse3`.
   The file lists every accepted key name.
4. Save and start the game.

Another mod already using **R** (for example a quick stack to every chest of the area)? Change `StackKey` here or the
key in the other mod.

## Settings

All in the same file, `BepInEx\config\com.focabr.gk2.quickstack.cfg`:

| Setting | Default | What it does |
|---|---|---|
| `StackKey` | `R` | Key that stores the matching items. `None` turns the mod off. |
| `ShowHint` | `true` | Shows the option next to the chest. |
| `ShowMessage` | `true` | Shows the message after storing. `false` = no message at all. |
| `MessageStyle` | `Items` | `Items` = one line per item stored (icon, name, amount). `Total` = a single line with the total. |

## Build

```
dotnet build src/QuickStack/QuickStack.csproj -c Release "-p:GamePath=<game folder>"
```

The game's DLLs are only referenced at build time and are never redistributed.

## About

Code written with Claude (Anthropic) and tested in-game by the author. MIT license.
Bugs and suggestions: [open an issue](https://github.com/focabr/GK2-QuickStack/issues).
