# GK2 QuickStack - notes for Claude

BepInEx 5 mod (Graveyard Keeper 2, Unity 6 Mono): a key (R) at a chest does the chest window's "move all identical
items" without opening the window. One file: `src/QuickStack/Plugin.cs`.

## Game code it relies on (checked in IL, game build of 2026-11-14)
- Button of the chest window: `UIBaseChestWindowData` ctor lambda →
  `chestInventory.TakeAllItemsExistingInMeFromOtherInventory(playerInventory, true, true)` (both `ignore...Bags` true).
  `CanTakeAnyItemsExistingInMeFromOtherInventory` = is there anything to move.
- Hint: `ChestInteractionHandler.FormInteractionInfo()` returns `InteractionInfos` (private `list`); the widget shows at
  most 2 rows (`UIInteractionHintWidgetData`). Keyboard prompt text = `"[E] " + LLBase.L(key)`
  (`ControllerIconLibrary.GetTextIcon`; gamepad = `<sprite name=...>`). Row text: game key `tip_move_similar_items`.
- The hint is only rebuilt on `Wgo.DrawWidgets()` → the plugin calls it when "can stack" changes.
- Special chests with their own windows (`ChestInteractionHandler.Interact`): ids `garden_bags_storage_1..3`,
  `conveyor_wine_beer_pallet`, `WGODef.conveyorType` 4 and 9.
- Message: private `UINotificator.ShowSimpleTextNotification(string locale, string text)` by reflection.

## Rules
- Code, CHANGELOG and docs in English; talk to the user in pt-BR.
- Version in `Directory.Build.props` AND `Plugin.Version`; every change = new version + CHANGELOG entry.
- Commits as focabr <focabr@gmail.com>, no trailers. Push, tag and Release only after the user says it was tested in
  game. Published tags never move.
- Build in the cloud: dotnet-sdk-8.0 (apt), game DLLs staged from the PC, `--source <empty dir>` (no NuGet needed).
