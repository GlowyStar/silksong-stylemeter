using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Stylemeter.Game.Menu
{
    // The Stylemeter button in the game's Options menu: a copy of the Keyboard button placed under it and added to the
    // menu's own button list, so the game's navigation, sounds and cursor fleurs work on it as on any other button.
    [HarmonyPatch]
    internal static class OptionsEntry
    {
        private const string Guard = "mod menu";
        private const string SourcePath = "Content/KeyboardOptions";
        private const string BackButtonName = "ApplyButton";

        private static readonly FieldInfo EntriesField = AccessTools.Field(typeof(MenuButtonList), "entries");
        private static readonly Type EntryType = AccessTools.Inner(typeof(MenuButtonList), "Entry");
        private static readonly FieldInfo EntrySelectable =
            EntryType == null ? null : AccessTools.Field(EntryType, "selectable");

        private static MenuButton button;

        // The Options menu is reached through this from the title screen and the pause menu alike; the button is added
        // the first time, before the menu builds its list of buttons.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.UIGoToOptionsMenu))]
        private static void BeforeOptionsMenu(UIManager __instance) => HookGuard.Run(Guard, () => Attach(__instance));

        private static void Attach(UIManager ui)
        {
            var screen = ui.optionsMenuScreen;
            if (button || !screen)
                return;
            var list = screen.GetComponent<MenuButtonList>();
            var source = screen.transform.Find(SourcePath);
            if (!list || !source || EntriesField == null || EntrySelectable == null)
            {
                Plugin.Log.LogWarning("Stylemeter: the Options menu changed shape, the Stylemeter button is not added");
                return;
            }

            var copy = Object.Instantiate(source.gameObject, source.parent);
            copy.name = "StylemeterOptions";
            copy.SetActive(true);
            copy.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
            foreach (var condition in copy.GetComponents<MenuButtonListCondition>())
                Object.DestroyImmediate(condition);
            foreach (var localize in copy.GetComponentsInChildren<AutoLocalizeTextUI>(true))
                Object.DestroyImmediate(localize);
            foreach (var trigger in copy.GetComponentsInChildren<EventTrigger>(true))
                Object.DestroyImmediate(trigger);

            var copied = copy.GetComponentInChildren<MenuButton>(true);
            copied.name = "StylemeterButton";
            copied.GetComponentInChildren<Text>(true).text = Plugin.Name;
            copied.OnSubmitPressed = new UnityEvent();
            copied.OnSubmitPressed.AddListener(() => ModPanel.Open(screen, copied));
            AddBeforeBack(list, copied);
            button = copied;
        }

        // The list's entries are private: a new entry goes in through reflection, just above the Back button.
        private static void AddBeforeBack(MenuButtonList list, Selectable selectable)
        {
            var entry = Activator.CreateInstance(EntryType, true);
            EntrySelectable.SetValue(entry, selectable);
            var entries = (Array)EntriesField.GetValue(list);
            var index = entries.Length;
            for (var i = 0; i < entries.Length; i++)
            {
                var existing = EntrySelectable.GetValue(entries.GetValue(i)) as Selectable;
                if (existing && existing.name == BackButtonName)
                {
                    index = i;
                    break;
                }
            }
            var grown = Array.CreateInstance(EntryType, entries.Length + 1);
            Array.Copy(entries, 0, grown, 0, index);
            grown.SetValue(entry, index);
            Array.Copy(entries, index, grown, index + 1, entries.Length - index);
            EntriesField.SetValue(list, grown);
        }
    }
}
