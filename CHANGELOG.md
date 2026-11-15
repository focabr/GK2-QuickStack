# Changelog

## 0.2.0

- After storing, the message is now one line per item, with its icon, name and amount, in the game's own notification
  list (the widget the game uses for item notifications). Before, it was a single "Items stored: N" line.
- New setting `MessageStyle`: `Items` (default, one line per item) or `Total` (the single line of 0.1.x).
  `ShowMessage = false` still turns the message off.

## 0.1.1

- The option now has the same name as the button inside the chest window ("Move all identical items", in the game's
  language). 0.1.0 used the game's gamepad tip for that button, which is worded differently ("Move All Duplicates").
- README: explains that the key is changed in the settings file (`BepInEx\config\com.focabr.gk2.quickstack.cfg`,
  `StackKey`), not in the game's Controls window.

## 0.1.0

First version.

- New option at ordinary chests, under "[E] Open": **[R] Move identical items**. It stores every inventory item the
  chest already holds, without opening the chest window (the game's own "move all identical items" function, so
  filters, free space and stack sizes apply as in the window; bags and the tool belt are not touched).
- The option only shows while there is something to store, and a short message tells how many items went in.
- Settings: `StackKey` (default `R`), `ShowHint`, `ShowMessage`.
- Seed-bag storages, the wine pallet and conveyor chests are left alone. Keyboard only.
