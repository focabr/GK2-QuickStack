using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
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
        public const string Version = "0.2.0";

        /// <summary>How often the hint row is re-checked while standing at a chest (seconds).</summary>
        private const float HintRefreshInterval = 0.25f;

        internal static Plugin Instance { get; private set; }

        private ConfigEntry<KeyCode> stackKey;
        private ConfigEntry<bool> showHint;
        private ConfigEntry<bool> showMessage;
        private ConfigEntry<MessageStyle> messageStyle;

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
                "Show a message after storing. false = no message at all.");
            messageStyle = Config.Bind("General", "MessageStyle", MessageStyle.Items,
                "Items = one line per item stored, with its icon, name and amount. Total = a single line with the total.");
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
            var moved = new List<KeyValuePair<string, int>>();
            int total = 0;
            if (target.CanTakeAnyItemsExistingInMeFromOtherInventory(player, true, true))
            {
                Dictionary<string, int> before = CountItems(player, out List<string> order);
                target.TakeAllItemsExistingInMeFromOtherInventory(player, true, true);
                Dictionary<string, int> after = CountItems(player, out _);
                foreach (string id in order)
                {
                    after.TryGetValue(id, out int left);
                    int n = before[id] - left;
                    if (n > 0)
                    {
                        moved.Add(new KeyValuePair<string, int>(id, n));
                        total += n;
                    }
                }
            }
            Logger.LogInfo($"Stack to {chest.Data.id}: {total} item(s) stored, {moved.Count} kind(s)");
            lastCanStack = CanStack(chest);
            chest.DrawWidgets();
            if (!showMessage.Value)
            {
                return;
            }
            if (total == 0)
            {
                Notify(T("Nada para guardar neste baú", "Nothing to store in this chest"));
            }
            else if (messageStyle.Value == MessageStyle.Total || !NotifyItems(moved))
            {
                Notify(T($"Itens guardados: {total}", $"Items stored: {total}"));
            }
        }

        /// <summary>
        /// Units per item id in the top level of an inventory, and the ids in inventory order (bags count as one item;
        /// their contents are not touched by the move).
        /// </summary>
        private static Dictionary<string, int> CountItems(Inventory inventory, out List<string> order)
        {
            var counts = new Dictionary<string, int>();
            order = new List<string>();
            List<Item> items = inventory?.Data?.Inventory;
            if (items == null)
            {
                return counts;
            }
            foreach (Item it in items)
            {
                if (it == null || it.IsEmpty || string.IsNullOrEmpty(it.id))
                {
                    continue;
                }
                if (!counts.TryGetValue(it.id, out int have))
                {
                    order.Add(it.id);
                }
                counts[it.id] = have + Math.Max(1, it.Count);
            }
            return counts;
        }

        // ------------------------------------------------------------------ hint row

        /// <summary>Called after the game builds the chest's hint ("[E] Open"): adds "[R] Move all identical items".</summary>
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
            infos.Add(new InteractionInfo($"[{KeyLabel(stackKey.Value)}] {HintText()}"));
        }

        /// <summary>
        /// The name the game gives the button inside the chest window (its mouse-over text, "Move all identical
        /// items."), in the game's language, without the final period: world hints have none ("[E] Open").
        /// </summary>
        private static string HintText()
        {
            string text = LLBase.L("btn_hint_move_all_identical_items");
            return string.IsNullOrEmpty(text) ? text : text.TrimEnd('.', '\u3002', ' ');
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
        private static MethodInfo showNotificationMethod;
        private static FieldInfo removeCountLabel;
        private static FieldInfo addCountLabel;
        private static int itemNotificationKind = -1;   // -1 not checked, 0 none, 1 "item removed" widget, 2 "item added" widget

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

        /// <summary>
        /// One line per item in the game's notification list, built with the game's own item notification (icon, name,
        /// amount) - the one shown when an item is picked up. Returns false when it cannot be used (the caller then
        /// shows the total instead).
        /// </summary>
        private bool NotifyItems(List<KeyValuePair<string, int>> moved)
        {
            try
            {
                UINotificator notificator = UINotificator.Instance;
                if (notificator == null || !ResolveItemNotification())
                {
                    return false;
                }
                foreach (KeyValuePair<string, int> kv in moved)
                {
                    UIBaseNotification line;
                    FieldInfo labelField;
                    if (itemNotificationKind == 1)
                    {
                        UIItemRemoveNotification n = LazyPooler.GetObject<UIItemRemoveNotification>();
                        n.ItemId = kv.Key;
                        n.DisplayingCount = kv.Value;
                        line = n;
                        labelField = removeCountLabel;
                    }
                    else
                    {
                        UIItemAddNotification n = LazyPooler.GetObject<UIItemAddNotification>();
                        n.ItemId = kv.Key;
                        n.DisplayingCount = kv.Value;
                        line = n;
                        labelField = addCountLabel;
                    }
                    showNotificationMethod.Invoke(notificator, new object[] { line });
                    // The game writes "-26" / "+26" and hides the amount when it is 1: show the plain amount, always.
                    if (labelField?.GetValue(line) is TMP_Text label)
                    {
                        label.gameObject.SetActive(true);
                        label.text = kv.Value.ToString();
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                LogOnce("notify items", e);
                return false;
            }
        }

        /// <summary>
        /// Picks the game's widget for the item lines, once: the "item removed" one when the game has a pool for it
        /// (the game never merges other notifications into it), otherwise the "item added" one.
        /// </summary>
        private bool ResolveItemNotification()
        {
            if (itemNotificationKind >= 0)
            {
                return itemNotificationKind > 0;
            }
            itemNotificationKind = 0;
            showNotificationMethod = AccessTools.Method(typeof(UINotificator), "ShowNotification", new[] { typeof(UIBaseNotification) });
            removeCountLabel = AccessTools.Field(typeof(UIItemRemoveNotification), "countLabel");
            addCountLabel = AccessTools.Field(typeof(UIItemAddNotification), "countLabel");
            var pools = AccessTools.Field(typeof(LazyPooler), "pools")?.GetValue(LazyPooler.Instance) as System.Collections.IDictionary;
            if (showNotificationMethod == null || pools == null)
            {
                Logger.LogWarning("Item notifications are not available in this game version - showing the total instead.");
                return false;
            }
            if (pools.Contains(typeof(UIItemRemoveNotification)))
            {
                itemNotificationKind = 1;
            }
            else if (pools.Contains(typeof(UIItemAddNotification)))
            {
                itemNotificationKind = 2;
            }
            Logger.LogInfo("Item notification widget: " + (itemNotificationKind == 1 ? "UIItemRemoveNotification"
                : itemNotificationKind == 2 ? "UIItemAddNotification" : "none (showing the total instead)"));
            return itemNotificationKind > 0;
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

    /// <summary>What the message shown after storing looks like.</summary>
    public enum MessageStyle
    {
        /// <summary>One line per item: icon, name and amount.</summary>
        Items,

        /// <summary>A single line with the total number of items.</summary>
        Total,
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
