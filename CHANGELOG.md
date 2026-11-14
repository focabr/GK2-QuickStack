# Changelog

## 0.1.1

- The option now has the same name as the button inside the chest window ("Move all identical items", in the game's
  language). 0.1.0 used the game's gamepad tip for that button, which is worded differently ("Move All Duplicates").

## 0.1.0

First version.

- New option at ordinary chests, under "[E] Open": **[R] Move identical items**. It stores every inventory item the
  chest already holds, without opening the chest window (the game's own "move all identical items" function, so
  filters, free space and stack sizes apply as in the window; bags and the tool belt are not touched).
- The option only shows while there is something to store, and a short message tells how many items went in.
- Settings: `StackKey` (default `R`), `ShowHint`, `ShowMessage`.
- Seed-bag storages, the wine pallet and conveyor chests are left alone. Keyboard only.
