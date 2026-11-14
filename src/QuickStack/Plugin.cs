using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace QuickStack
{
    /// <summary>
    /// Adds a second option to a chest you are standing at: a key (R by default) that does what the chest window's
    /// "move all identical items" button does, without opening the window. It calls the game's own function, so the
    /// chest's filters, free space and stack sizes apply exactly as in the window. Nothing is created or deleted.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("GraveyardKeeper2.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.focabr.gk2.quickstack";
        public const string Name = "GK2 QuickStack";
        public const string Version = "0.1.0";

        /// <summary>How often the hint row is re-checked while standing at a chest (seconds).</summary>
        private const float HintRefreshInterval = 0.25f;

        internal static Plugin Instance { get; private set; }

        private ConfigEntry<KeyCode> stackKey;
        private ConfigEntry<bool> showHint;
        private ConfigEntry<bool> showMessage;

        private Harmony harmony;
        private Wgo lastChest;
        private bool lastCanStack;
        private float nextHintCheck;
        private readonly HashSet<string> loggedErrors = new HashSet<string>();

        private void Awake()
        {
            Instance = this;
            stackKey = Config.Bind("General", "StackKey", KeyCode.R,
                "Key that stores the matching items in the chest you are standing at. None = off.");
            showHint = Config.Bind("General", "ShowHint", true,
                "Show the option next to the chest (under \"Open\") while there is something to store.");
            showMessage = Config.Bind("General", "ShowMessage", true,
                "Show a short message with the number of items stored.");
            stackKey.SettingChanged += (_, __) => RedrawCurrentChest();
            showHint.SettingChanged += (_, __) => RedrawCurrentChest();

            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(ChestHintPatch));
            Logger.LogInfo($"{Name} {Version} loaded - key: {stackKey.Value}");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                LogOnce("update", e);
            }
        }

        private void Tick()
        {
            Wgo chest = CanAct() ? CurrentChest() : null;

            // The game only rebuilds the hint when the target changes; redraw it when "something to store" changes
            // while the player stands at the same chest (picked something up, stored, a zombie took items...).
            if (Time.unscaledTime >= nextHintCheck || chest != lastChest)
            {
                nextHintCheck = Time.unscaledTime + HintRefreshInterval;
                bool can = chest != null && CanStack(chest);
                if (chest != null && chest == lastChest && can != lastCanStack)
                {
                    chest.DrawWidgets();
                }
                lastChest = chest;
                lastCanStack = can;
            }

            if (chest != null && stackKey.Value != KeyCode.None && Input.GetKeyDown(stackKey.Value))
            {
                Stack(chest);
            }
        }

        // ------------------------------------------------------------------ game state

        /// <summary>Same moments the player could press E on the chest: in game, no window, controls and input on.</summary>
        private static bool CanAct()
        {
            if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.InGame
                || MainGame.PlayerController == null || MainGame.PlayerData == null || MainGame.IsGamePaused)
            {
                return false;
            }
            if (LazyWindowsStackController.ActiveWindow != null || !LazyInput.IsInputActive())
            {
                return false;
            }
            return MainGame.PlayerController.IsControlsEnabledForInteractionHints;
        }

        /// <summary>The ordinary chest the game is aiming at right now, or null.</summary>
        private static Wgo CurrentChest()
        {
            PlayerInteractionComponent pic = MainGame.PlayerController.PlayerInteractionComponent;
            Wgo wgo = pic != null ? pic.WgoUnderInteraction : null;
            return wgo != null && IsOrdinaryChest(wgo) ? wgo : null;
        }

        /// <summary>
        /// A chest that opens the ordinary chest window. Seed-bag storages, the wine pallet and conveyor chests open
        /// their own windows (ChestInteractionHandler.Interact) and are left alone.
        /// </summary>
        internal static bool IsOrdinaryChest(Wgo wgo)
        {
            if (wgo == null || !wgo.HasData || !(wgo.InteractionHandler is ChestInteractionHandler))
            {
                return false;
            }
            WgoData data = wgo.Data;
            if (data.Inventory == null || data.Definition == null)
            {
                return false;
            }
            switch (data.id)
            {
                case "garden_bags_storage_1":
                case "garden_bags_storage_2":
                case "garden_bags_storage_3":
                case "conveyor_wine_beer_pallet":
                    return false;
            }
            int conveyorType = (int)data.Definition.conveyorType;
            return conveyorType != 4 && conveyorType != 9;
        }

        /// <summary>Is there anything in the player's inventory that this chest already holds and can take?</summary>
        internal static bool CanStack(Wgo chest)
        {
            // true, true = ignore bags on both sides, the values the chest window's own button uses.
            return chest.Data.Inventory.CanTakeAnyItemsExistingInMeFromOtherInventory(MainGame.PlayerData.inventory, true, true);
        }

        // ------------------------------------------------------------------ action

        private void Stack(Wgo chest)
        {
            Inventory player = MainGame.PlayerData.inventory;
            Inventory target = chest.Data.Inventory;
            int moved = 0;
            if (target.CanTakeAnyItemsExistingInMeFromOtherInventory(player, true, true))
            {
                int before = CountItems(player);
                target.TakeAllItemsExistingInMeFromOtherInventory(player, true, true);
                moved = before - CountItems(player);
            }
            Logger.LogInfo($"Stack to {chest.Data.id}: {moved} item(s) stored");
            lastCanStack = CanStack(chest);
            chest.DrawWidgets();
            if (showMessage.Value)
            {
                Notify(moved > 0
                    ? T($"Itens guardados: {moved}", $"Items stored: {moved}")
                    : T("Nada para guardar neste baú", "Nothing to store in this chest"));
            }
        }

        /// <summary>Units in the top level of an inventory (bags count as one; their contents are not touched).</summary>
        private static int CountItems(Inventory inventory)
        {
            int total = 0;
            List<Item> items = inventory?.Data?.Inventory;
            if (items == null)
            {
                return 0;
            }
            foreach (Item it in items)
            {
                if (it != null && !it.IsEmpty)
                {
                    total += Math.Max(1, it.Count);
                }
            }
            return total;
        }

        // ------------------------------------------------------------------ hint row

        /// <summary>Called after the game builds the chest's hint ("[E] Open"): adds "[R] Move identical items".</summary>
        internal void AddHint(Wgo wgo, InteractionInfos infos)
        {
            if (infos == null || !showHint.Value || stackKey.Value == KeyCode.None || !IsOrdinaryChest(wgo))
            {
                return;
            }
            // The game's widget has two rows: only add ours when exactly one is taken.
            List<InteractionInfo> list = ChestHintPatch.ListOf(infos);
            if (list == null || list.Count != 1 || !IsKeyboardActive() || !CanStack(wgo))
            {
                return;
            }
            infos.Add(new InteractionInfo($"[{KeyLabel(stackKey.Value)}] {LLBase.L("tip_move_similar_items")}"));
        }

        /// <summary>
        /// Keyboard prompts are text ("[E]"), gamepad prompts are sprites. The mod's key is a keyboard key, so the row
        /// is hidden while the game shows gamepad prompts.
        /// </summary>
        private static bool IsKeyboardActive()
        {
            string icon = ControllerIconLibrary.GetIconId(GameKey.Interaction, null, false);
            return icon != null && icon.StartsWith("[");
        }

        private static string KeyLabel(KeyCode key)
        {
            string name = key.ToString();
            return name.StartsWith("Alpha") && name.Length == 6 ? name.Substring(5) : name;
        }

        private void RedrawCurrentChest()
        {
            try
            {
                if (lastChest != null && CanAct())
                {
                    lastChest.DrawWidgets();
                }
            }
            catch (Exception e)
            {
                LogOnce("redraw", e);
            }
        }

        // ------------------------------------------------------------------ message

        private static MethodInfo notifyMethod;
        private static bool notifyLookedUp;

        /// <summary>Shows a line in the game's own notification list (the one that says "inventory is full").</summary>
        private void Notify(string text)
        {
            try
            {
                if (!notifyLookedUp)
                {
                    notifyLookedUp = true;
                    notifyMethod = AccessTools.Method(typeof(UINotificator), "ShowSimpleTextNotification",
                        new[] { typeof(string), typeof(string) });
                    if (notifyMethod == null)
                    {
                        Logger.LogWarning("UINotificator.ShowSimpleTextNotification(string, string) not found - messages are off.");
                    }
                }
                UINotificator notificator = UINotificator.Instance;
                if (notifyMethod != null && notificator != null)
                {
                    notifyMethod.Invoke(notificator, new object[] { "quickstack_message", text });
                }
            }
            catch (Exception e)
            {
                LogOnce("notify", e);
            }
        }

        /// <summary>Brazilian Portuguese when the game is in Portuguese, English otherwise.</summary>
        private static string T(string pt, string en)
        {
            string lang = (LLBase.CurrentLang ?? "en").ToLowerInvariant();
            return lang.StartsWith("pt") || lang == "br" ? pt : en;
        }

        internal void LogOnce(string where, Exception e)
        {
            if (loggedErrors.Add(where))
            {
                Logger.LogError($"{where}: {e}");
            }
        }
    }

    /// <summary>Adds the mod's row to the hint the game shows next to a chest.</summary>
    [HarmonyPatch(typeof(ChestInteractionHandler), "FormInteractionInfo")]
    internal static class ChestHintPatch
    {
        private static readonly FieldInfo ListField = AccessTools.Field(typeof(InteractionInfos), "list");

        internal static List<InteractionInfo> ListOf(InteractionInfos infos) => ListField?.GetValue(infos) as List<InteractionInfo>;

        private static void Postfix(Wgo ___assignedWgo, InteractionInfos __result)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null)
            {
                return;
            }
            try
            {
                plugin.AddHint(___assignedWgo, __result);
            }
            catch (Exception e)
            {
                plugin.LogOnce("hint", e);
            }
        }
    }
}
