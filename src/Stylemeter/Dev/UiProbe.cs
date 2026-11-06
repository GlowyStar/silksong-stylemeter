using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Stylemeter.Dev
{
    // Sandbox tools for the game's menus (bridge verbs "ui"): open the pause menu and the options, and print a menu
    // screen as a tree (names, components and texts) to see where the mod's button goes.
    internal static class UiProbe
    {
        public static string Run(string[] w)
        {
            var ui = UIManager.instance;
            if (!ui)
                return "no UIManager";
            switch (w.Length > 1 ? w[1] : "")
            {
                case "pause":
                    GameManager.instance.StartCoroutine(GameManager.instance.PauseGameToggleByMenu());
                    return "pause toggled";
                case "options":
                    ui.UIGoToOptionsMenu();
                    return "options";
                case "back":
                    return "back " + ui.UIGoBack();
                case "state":
                    var system = EventSystem.current;
                    var selected = system ? system.currentSelectedGameObject : null;
                    return $"menu={ui.menuState} module="
                        + (system && system.currentInputModule ? system.currentInputModule.GetType().Name : "-")
                        + $" selected={(selected ? Path(selected.transform) : "-")}";
                case "stylemeter":
                    return OpenPanel(ui);
                case "nav":
                    return Navigate(w.Length > 2 ? w[2] : "");
                case "tree":
                    var screen = w.Length > 2 && w[2] == "pause" ? ui.pauseMenuScreen : ui.optionsMenuScreen;
                    return screen ? Tree(screen.transform, w.Length > 3 ? int.Parse(w[3]) : 6) : "no screen";
                default:
                    return "ui pause | options | back | state | stylemeter | nav <move> | tree [options|pause] [depth]";
            }
        }

        // Presses the Stylemeter button of the Options menu, as a click would.
        private static string OpenPanel(UIManager ui)
        {
            var entry = ui.optionsMenuScreen ? ui.optionsMenuScreen.transform.Find("Content/StylemeterOptions") : null;
            var button = entry ? entry.GetComponentInChildren<UnityEngine.UI.MenuButton>(true) : null;
            if (!button)
                return "no Stylemeter button";
            button.OnSubmit(new BaseEventData(EventSystem.current));
            return "submitted " + Path(button.transform);
        }

        // Sends the selected panel row what the input module would: a move, submit or cancel. Only rows of the panel
        // take it, so a stray command cannot press a title screen button.
        private static string Navigate(string move)
        {
            var system = EventSystem.current;
            var target = system ? system.currentSelectedGameObject : null;
            if (!target || !target.GetComponent<Game.Menu.PanelRow>())
                return "the selection is not a panel row: " + (target ? Path(target.transform) : "-");
            switch (move)
            {
                case "submit":
                    ExecuteEvents.Execute(target, new BaseEventData(system), ExecuteEvents.submitHandler);
                    break;
                case "cancel":
                    ExecuteEvents.Execute(target, new BaseEventData(system), ExecuteEvents.cancelHandler);
                    break;
                case "up":
                case "down":
                case "left":
                case "right":
                    var direction = (MoveDirection)System.Enum.Parse(typeof(MoveDirection), move, true);
                    ExecuteEvents.Execute(target, new AxisEventData(system) { moveDir = direction },
                        ExecuteEvents.moveHandler);
                    break;
                default:
                    return "ui nav up | down | left | right | submit | cancel";
            }
            var now = system.currentSelectedGameObject;
            return "selected=" + (now ? Path(now.transform) : "-");
        }

        private static string Tree(Transform root, int depth)
        {
            var text = new StringBuilder();
            Walk(root, 0, depth, text);
            return text.ToString();
        }

        private static void Walk(Transform t, int level, int depth, StringBuilder text)
        {
            if (level > depth)
                return;
            var components = t.GetComponents<Component>()
                .Where(c => c && !(c is Transform))
                .Select(c => c.GetType().Name);
            var label = t.GetComponent<TMProOld.TMP_Text>();
            var legacy = t.GetComponent<UnityEngine.UI.Text>();
            var words = label ? label.text : legacy ? legacy.text : null;
            var rect = t as RectTransform;
            text.Append(new string(' ', level * 2))
                .Append(t.name)
                .Append(t.gameObject.activeSelf ? "" : " (off)")
                .Append(" [").Append(string.Join(",", components)).Append(']');
            if (rect)
                text.Append(FormattableString($" pos={rect.anchoredPosition} size={rect.sizeDelta}"));
            if (!string.IsNullOrEmpty(words))
                text.Append(" \"").Append(words.Replace("\n", " ")).Append('"');
            text.Append('\n');
            foreach (Transform child in t)
                Walk(child, level + 1, depth, text);
        }

        private static string FormattableString(System.FormattableString s) => System.FormattableString.Invariant(s);

        private static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    }
}
