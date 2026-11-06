using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GlobalEnums;
using Stylemeter.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stylemeter.Game.Menu
{
    // The mod's settings, opened from Options > Stylemeter: the points multiplier and every bonus by section. It lies
    // over the Options screen, which stays untouchable meanwhile, and closes by itself when the game leaves that screen
    // (unpausing from inside it, for one).
    internal sealed class ModPanel : MonoBehaviour, IScrollHandler
    {
        private const string Guard = "mod menu";
        private const float PanelWidth = 1180f;
        private const float PanelHeight = 900f;
        private const float RowWidth = 980f;
        private const float ViewTop = 188f;
        private const float ViewHeight = 556f;
        private const float MultiplierHeight = 64f;
        private const float SectionHeight = 58f;
        private const float WholeSectionHeight = 46f;
        private const float BonusHeight = 44f;
        private const float RevealMargin = 44f;
        private const float WheelStep = 88f;
        private const float FadeTime = 0.15f;
        private const float ScrollSharpness = 14f;
        private const float GlowAlpha = 0.17f;
        private const float MouseRestTime = 0.4f;

        private static readonly float[] MultiplierSteps =
            { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 2.75f, 3f };

        private static ModPanel instance;

        private readonly List<PanelRow> listRows = new List<PanelRow>();
        private readonly List<PanelRow> shownRows = new List<PanelRow>();
        private readonly HashSet<BonusGroup> openSections = new HashSet<BonusGroup>();
        private CanvasGroup fade;
        private RectTransform body;
        private RectTransform content;
        private Font font;
        private Font titleFont;
        private PanelRow multiplierRow;
        private PanelRow focus;
        private MenuScreen screen;
        private MenuButton returnTo;
        private bool wasScreenInteractable;
        private bool isOpen;
        private bool isSelectSoundMuted;
        private bool isFocusReturnPending;
        private Vector3 lastMousePosition;
        private float mouseMovedAt = float.NegativeInfinity;
        private readonly List<(CanvasGroup Group, float Alpha)> coveredGroups = new List<(CanvasGroup, float)>();
        private float scroll;
        private float targetScroll;
        private float contentHeight;
        private Action tick;

        private static StyleSettings Settings => EngineHost.Engine.Settings;

        public static void Open(MenuScreen screen, MenuButton returnTo) => HookGuard.Run(Guard, () =>
        {
            if (!instance)
                instance = Create(screen);
            instance.Show(screen, returnTo);
        });

        private static ModPanel Create(MenuScreen screen)
        {
            var root = new GameObject("Stylemeter Panel", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            root.AddComponent<GraphicRaycaster>();
            var panel = root.AddComponent<ModPanel>();
            try
            {
                panel.Build(screen);
            }
            catch
            {
                Destroy(root);
                throw;
            }
            root.SetActive(false);
            return panel;
        }

        private void Awake() => tick = Tick;

        private void Update()
        {
            if (fade && (isOpen || fade.alpha > 0f))
                HookGuard.Run(Guard, tick);
        }

        private void Tick()
        {
            var deltaTime = Time.unscaledDeltaTime;
            fade.alpha = Mathf.MoveTowards(fade.alpha, isOpen ? 1f : 0f, deltaTime / FadeTime);
            // Inactive rows leave the game's list of selectables, so no other menu can navigate into a hidden panel.
            if (!isOpen && fade.alpha <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            if (isOpen && !IsScreenShowing())
                Leave();
            if (isOpen)
                KeepFocus();
            scroll = Mathf.Lerp(scroll, targetScroll, 1f - Mathf.Exp(-ScrollSharpness * deltaTime));
            content.anchoredPosition = new Vector2(0f, scroll);
            if (focus)
            {
                var glow = focus.Glow.color;
                glow.a = Mathf.MoveTowards(glow.a, GlowAlpha, deltaTime * GlowAlpha / FadeTime);
                focus.Glow.color = glow;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!isOpen)
                return;
            targetScroll -= eventData.scrollDelta.y * WheelStep;
            ClampScroll();
        }

        private void Show(MenuScreen optionsScreen, MenuButton button)
        {
            screen = optionsScreen;
            returnTo = button;
            focus = multiplierRow;
            lastMousePosition = Input.mousePosition;
            mouseMovedAt = float.NegativeInfinity;
            Refresh();
            scroll = targetScroll = 0f;
            Relayout();

            var screenGroup = screen.ScreenCanvasGroup;
            wasScreenInteractable = screenGroup && screenGroup.interactable;
            if (screenGroup)
                screenGroup.interactable = false;
            CoverScreen();
            gameObject.SetActive(true);
            fade.blocksRaycasts = true;
            fade.interactable = true;
            isOpen = true;
            isSelectSoundMuted = true;
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(multiplierRow.gameObject);
        }

        // Back from the panel: the Options screen takes the input again, on the Stylemeter button.
        public void Close()
        {
            if (!isOpen)
                return;
            Hide();
            var screenGroup = screen ? screen.ScreenCanvasGroup : null;
            if (screenGroup)
                screenGroup.interactable = wasScreenInteractable;
            Sound(audio => audio.PlayCancel());
            if (returnTo)
                UIManager.HighlightSelectableNoSound(returnTo);
        }

        // The game left the Options screen under the panel: let go of the selection so no hidden row takes input.
        private void Leave()
        {
            Hide();
            var current = EventSystem.current;
            var selection = current ? current.currentSelectedGameObject : null;
            if (selection && selection.transform.IsChildOf(transform))
                current.SetSelectedGameObject(null);
        }

        private void Hide()
        {
            isOpen = false;
            fade.blocksRaycasts = false;
            fade.interactable = false;
            UncoverScreen();
        }

        // The Options screen's own title, buttons and ornament fade out under the panel and come back after it.
        private void CoverScreen()
        {
            coveredGroups.Clear();
            foreach (Transform part in screen.transform)
            {
                if (!part.gameObject.activeSelf)
                    continue;
                var group = part.GetComponent<CanvasGroup>();
                if (!group)
                    group = part.gameObject.AddComponent<CanvasGroup>();
                coveredGroups.Add((group, group.alpha));
                group.alpha = 0f;
            }
        }

        private void UncoverScreen()
        {
            foreach (var (group, alpha) in coveredGroups)
            {
                if (group)
                    group.alpha = alpha;
            }
            coveredGroups.Clear();
        }

        // The game's input module selects whatever lies under the mouse, and clears the selection over empty space.
        // In a list that scrolls under a resting mouse that would steal the keyboard's place, so the panel keeps its
        // own focus: hover counts only while the mouse moves or clicks, and an empty selection goes back to the focus.
        private void KeepFocus()
        {
            var system = EventSystem.current;
            if (!system)
                return;
            var current = system.currentSelectedGameObject;
            var isInPanel = current && current.transform.IsChildOf(transform);
            if (isInPanel && !isFocusReturnPending)
                return;
            isFocusReturnPending = false;
            isSelectSoundMuted = true;
            system.SetSelectedGameObject((focus ? focus : multiplierRow).gameObject);
        }

        private bool IsMouseInUse()
        {
            var position = Input.mousePosition;
            if (position != lastMousePosition)
            {
                lastMousePosition = position;
                mouseMovedAt = Time.unscaledTime;
            }
            return Input.GetMouseButton(0) || Time.unscaledTime - mouseMovedAt <= MouseRestTime;
        }

        private bool IsScreenShowing()
        {
            if (!screen || !screen.isActiveAndEnabled)
                return false;
            var screenGroup = screen.ScreenCanvasGroup;
            var ui = UIManager.instance;
            return (!screenGroup || screenGroup.alpha > 0.99f) && ui && ui.menuState == MainMenuState.OPTIONS_MENU;
        }

        public void Activate(PanelRow row)
        {
            if (!isOpen)
                return;
            switch (row.Kind)
            {
                case RowKind.Multiplier:
                    StepMultiplier(1, wraps: true);
                    break;
                case RowKind.Section:
                    SetSectionOpen(row, !openSections.Contains(row.Group));
                    break;
                case RowKind.WholeSection:
                    ToggleSection(row.Group);
                    break;
                case RowKind.Bonus:
                    Settings.Toggle(row.Bonus.Name);
                    Sound(audio => audio.PlaySubmit());
                    break;
            }
            Refresh();
        }

        // A click on the multiplier row steps toward the side of the value it landed on.
        public void Click(PanelRow row, PointerEventData eventData)
        {
            if (row.Kind != RowKind.Multiplier)
            {
                Activate(row);
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(row.Value.rectTransform, eventData.position,
                eventData.pressEventCamera, out var local);
            Adjust(row, local.x < 0f ? -1 : 1);
        }

        public void Adjust(PanelRow row, int direction)
        {
            if (!isOpen)
                return;
            switch (row.Kind)
            {
                case RowKind.Multiplier:
                    StepMultiplier(direction, wraps: false);
                    break;
                case RowKind.Section:
                    if (openSections.Contains(row.Group) != direction > 0)
                        SetSectionOpen(row, direction > 0);
                    break;
                default:
                    Activate(row);
                    return;
            }
            Refresh();
        }

        public void OnRowSelected(PanelRow row, BaseEventData eventData)
        {
            var isResting = eventData is PointerEventData && !IsMouseInUse();
            if (isResting && focus && focus != row)
            {
                isFocusReturnPending = true;
                return;
            }
            var previous = focus;
            focus = row;
            if (previous && previous != row)
                Paint(previous);
            Paint(row);
            if (row.Kind != RowKind.Multiplier)
                Reveal(row);
            if (isSelectSoundMuted)
                isSelectSoundMuted = false;
            else
                Sound(audio => audio.PlaySelect());
        }

        private void StepMultiplier(int direction, bool wraps)
        {
            var current = Settings.PointsMultiplier;
            var next = direction > 0
                ? MultiplierSteps.Where(step => step > current + 0.001f).DefaultIfEmpty(float.NaN).First()
                : MultiplierSteps.Where(step => step < current - 0.001f).DefaultIfEmpty(float.NaN).Last();
            if (float.IsNaN(next) && wraps)
                next = direction > 0 ? MultiplierSteps[0] : MultiplierSteps[MultiplierSteps.Length - 1];
            if (float.IsNaN(next))
                return;
            Settings.PointsMultiplier = next;
            Sound(audio => audio.PlaySlider());
        }

        private void SetSectionOpen(PanelRow section, bool isOpening)
        {
            if (isOpening)
                openSections.Add(section.Group);
            else
                openSections.Remove(section.Group);
            Relayout();
            Reveal(section);
            Sound(audio => audio.PlaySubmit());
        }

        // A section with any bonus off turns fully on; a fully on section turns off.
        private static void ToggleSection(BonusGroup group)
        {
            var names = SectionNames(group);
            if (IsSectionFullyOn(group))
                Settings.Disable(names);
            else
                Settings.Enable(names);
            Sound(audio => audio.PlaySubmit());
        }

        private static List<string> SectionNames(BonusGroup group) =>
            Bonuses.Catalog.Where(bonus => bonus.Group == group).Select(bonus => bonus.Name).ToList();

        private static bool IsSectionFullyOn(BonusGroup group) => SectionNames(group).All(Settings.IsEnabled);

        // Values and colours of every row from the settings.
        private void Refresh()
        {
            var multiplier = Settings.PointsMultiplier;
            multiplierRow.Value.text = "×" + multiplier.ToString("0.##", CultureInfo.InvariantCulture);
            multiplierRow.Value.color = Mathf.Approximately(multiplier, 1f) ? PanelArt.Ink : PanelArt.Gold;
            multiplierRow.LeftArrow.color = Faded(PanelArt.Gold, multiplier > MultiplierSteps[0] + 0.001f);
            multiplierRow.RightArrow.color =
                Faded(PanelArt.Gold, multiplier < MultiplierSteps[MultiplierSteps.Length - 1] - 0.001f);

            foreach (var row in listRows)
            {
                switch (row.Kind)
                {
                    case RowKind.Section:
                        var names = SectionNames(row.Group);
                        var total = names.Count;
                        var enabled = names.Count(Settings.IsEnabled);
                        row.Value.text = enabled == 0 ? PanelText.Off : $"{enabled}/{total}";
                        row.Value.color = enabled == 0 ? PanelArt.Off : enabled == total ? PanelArt.Dim : PanelArt.Gold;
                        row.Chevron.rectTransform.localEulerAngles =
                            new Vector3(0f, 0f, openSections.Contains(row.Group) ? -90f : 0f);
                        break;
                    case RowKind.WholeSection:
                        var sectionNames = SectionNames(row.Group);
                        var isFullyOn = sectionNames.All(Settings.IsEnabled);
                        var isFullyOff = !sectionNames.Any(Settings.IsEnabled);
                        row.Value.text = isFullyOn ? PanelText.On : isFullyOff ? PanelText.Off : PanelText.Partly;
                        row.Value.color = isFullyOn ? PanelArt.Gold : isFullyOff ? PanelArt.Off : PanelArt.Dim;
                        break;
                    case RowKind.Bonus:
                        var isOn = Settings.IsEnabled(row.Bonus.Name);
                        row.Value.text = isOn ? PanelText.On : PanelText.Off;
                        row.Value.color = isOn ? PanelArt.Gold : PanelArt.Off;
                        break;
                }
                Paint(row);
            }
            Paint(multiplierRow);
        }

        // Brighter text on the focused row, dim text on a bonus that is off.
        private void Paint(PanelRow row)
        {
            var isFocused = row == focus;
            if (!isFocused)
                row.Glow.color = new Color(PanelArt.Gold.r, PanelArt.Gold.g, PanelArt.Gold.b, 0f);
            var labelColor = row.Kind == RowKind.Section ? PanelArt.Gold : PanelArt.Ink;
            if (row.Kind == RowKind.Bonus && !Settings.IsEnabled(row.Bonus.Name))
                labelColor = PanelArt.Dim;
            row.Label.color = isFocused ? Color.Lerp(labelColor, Color.white, 0.35f) : labelColor;
        }

        // Which rows show (a closed section shows only its header), where they sit, and how up and down move.
        private void Relayout()
        {
            shownRows.Clear();
            shownRows.Add(multiplierRow);
            var top = 0f;
            foreach (var row in listRows)
            {
                var isShown = row.Kind == RowKind.Section || openSections.Contains(row.Group);
                row.gameObject.SetActive(isShown);
                if (!isShown)
                    continue;
                row.Top = top;
                row.Rect.anchoredPosition = new Vector2(0f, -top - row.Height / 2f);
                top += row.Height;
                shownRows.Add(row);
            }
            contentHeight = top;
            content.sizeDelta = new Vector2(RowWidth, contentHeight);
            for (var i = 0; i < shownRows.Count; i++)
            {
                shownRows[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = shownRows[(i + shownRows.Count - 1) % shownRows.Count],
                    selectOnDown = shownRows[(i + 1) % shownRows.Count],
                };
            }
            ClampScroll();
        }

        private void Reveal(PanelRow row)
        {
            if (row.Top - RevealMargin < targetScroll)
                targetScroll = row.Top - RevealMargin;
            else if (row.Top + row.Height + RevealMargin > targetScroll + ViewHeight)
                targetScroll = row.Top + row.Height + RevealMargin - ViewHeight;
            ClampScroll();
        }

        private void ClampScroll() =>
            targetScroll = Mathf.Clamp(targetScroll, 0f, Mathf.Max(0f, contentHeight - ViewHeight));

        private static void Sound(Action<MenuAudioController> play)
        {
            var ui = UIManager.instance;
            if (ui && ui.uiAudioPlayer)
                play(ui.uiAudioPlayer);
        }

        private static Color Faded(Color color, bool isActive) =>
            new Color(color.r, color.g, color.b, isActive ? 1f : 0.25f);

        private void Build(MenuScreen optionsScreen)
        {
            var anyButton = optionsScreen.GetComponentInChildren<MenuButton>(true);
            var label = anyButton ? anyButton.GetComponentInChildren<Text>(true) : null;
            font = label && label.font ? label.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var titleObject = optionsScreen.transform.Find("Title");
            var titleText = titleObject ? titleObject.GetComponent<Text>() : null;
            titleFont = titleText && titleText.font ? titleText.font : font;

            fade = gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.blocksRaycasts = false;

            var dim = NewRect("Dim", transform).gameObject.AddComponent<Image>();
            Stretch(dim.rectTransform);
            dim.color = new Color(0f, 0f, 0f, 0.62f);

            body = NewRect("Body", transform);
            body.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var frame = body.gameObject.AddComponent<Image>();
            frame.sprite = PanelArt.Frame;
            frame.type = Image.Type.Sliced;

            var title = NewText(body, "Title", 60, TextAnchor.MiddleCenter);
            title.font = titleFont;
            title.text = Plugin.Name.ToUpperInvariant();
            Place(title.rectTransform, new Vector2(0f, 386f), new Vector2(PanelWidth, 80f));
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            AddOrnament(optionsScreen);

            multiplierRow = NewRow(body, RowKind.Multiplier, MultiplierHeight);
            multiplierRow.Rect.anchoredPosition = new Vector2(0f, 255f);
            multiplierRow.Label = NewText(multiplierRow.Rect, "Label", 34, TextAnchor.MiddleLeft);
            multiplierRow.Label.text = PanelText.Multiplier;
            PlaceInRow(multiplierRow.Label, 24f, 600f);
            multiplierRow.LeftArrow = NewArrow(multiplierRow.Rect, RowWidth - 214f, 180f);
            multiplierRow.Value = NewText(multiplierRow.Rect, "Value", 34, TextAnchor.MiddleCenter);
            PlaceInRow(multiplierRow.Value, RowWidth - 196f, 140f);
            multiplierRow.RightArrow = NewArrow(multiplierRow.Rect, RowWidth - 40f, 0f);

            AddRule(body, 212f);
            AddRule(body, ViewTop - ViewHeight - 14f);

            var view = NewRect("View", body);
            view.anchoredPosition = new Vector2(0f, ViewTop - ViewHeight / 2f);
            view.sizeDelta = new Vector2(RowWidth + 40f, ViewHeight);
            view.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 18);
            content = NewRect("Content", view);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            BuildList();

            var hint = NewText(body, "Hint", 21, TextAnchor.MiddleCenter);
            hint.text = PanelText.Hint;
            hint.color = PanelArt.Dim;
            Place(hint.rectTransform, new Vector2(0f, -404f), new Vector2(PanelWidth - 60f, 30f));
            var version = NewText(body, "Version", 17, TextAnchor.MiddleRight);
            version.color = new Color(PanelArt.Dim.r, PanelArt.Dim.g, PanelArt.Dim.b, 0.6f);
            version.text = PluginInfo.Version;
            Place(version.rectTransform, new Vector2(PanelWidth / 2f - 110f, -428f), new Vector2(160f, 24f));
        }

        // One section row per group in the catalog's order, each followed by its whole-section switch and its bonuses.
        private void BuildList()
        {
            foreach (var group in Bonuses.Catalog.Select(bonus => bonus.Group).Distinct())
            {
                var section = NewRow(content, RowKind.Section, SectionHeight);
                section.Group = group;
                section.Chevron = NewImage(section.Rect, "Chevron", PanelArt.Arrow, new Vector2(18f, 18f));
                section.Chevron.color = PanelArt.Gold;
                section.Chevron.rectTransform.anchoredPosition = new Vector2(-RowWidth / 2f + 30f, 0f);
                section.Label = NewText(section.Rect, "Label", 32, TextAnchor.MiddleLeft);
                section.Label.text = PanelText.Section(group);
                PlaceInRow(section.Label, 56f, 700f);
                section.Value = NewText(section.Rect, "Value", 24, TextAnchor.MiddleRight);
                PlaceInRow(section.Value, RowWidth - 220f, 200f);
                listRows.Add(section);

                var whole = NewRow(content, RowKind.WholeSection, WholeSectionHeight);
                whole.Group = group;
                whole.Label = NewText(whole.Rect, "Label", 25, TextAnchor.MiddleLeft);
                whole.Label.text = PanelText.WholeSection;
                whole.Label.fontStyle = FontStyle.Italic;
                PlaceInRow(whole.Label, 84f, 600f);
                whole.Value = NewText(whole.Rect, "Value", 24, TextAnchor.MiddleRight);
                PlaceInRow(whole.Value, RowWidth - 220f, 200f);
                listRows.Add(whole);

                foreach (var bonus in Bonuses.Catalog.Where(bonus => bonus.Group == group))
                {
                    var row = NewRow(content, RowKind.Bonus, BonusHeight);
                    row.Group = group;
                    row.Bonus = bonus;
                    row.Label = NewText(row.Rect, "Label", 27, TextAnchor.MiddleLeft);
                    row.Label.text = bonus.Name;
                    PlaceInRow(row.Label, 84f, 620f);
                    row.Points = NewText(row.Rect, "Points", 21, TextAnchor.MiddleRight);
                    row.Points.text = "+" + bonus.Points.ToString("0", CultureInfo.InvariantCulture);
                    row.Points.color = PanelArt.Dim;
                    PlaceInRow(row.Points, RowWidth - 330f, 100f);
                    row.Value = NewText(row.Rect, "Value", 24, TextAnchor.MiddleRight);
                    PlaceInRow(row.Value, RowWidth - 220f, 200f);
                    listRows.Add(row);
                }
            }
        }

        private PanelRow NewRow(RectTransform parent, RowKind kind, float height)
        {
            var rect = NewRect(kind.ToString(), parent);
            if (parent == content)
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(RowWidth, height);
            var glow = rect.gameObject.AddComponent<Image>();
            glow.sprite = PanelArt.Glow;
            glow.color = new Color(PanelArt.Gold.r, PanelArt.Gold.g, PanelArt.Gold.b, 0f);
            var row = rect.gameObject.AddComponent<PanelRow>();
            row.Panel = this;
            row.Kind = kind;
            row.Rect = rect;
            row.Glow = glow;
            row.Height = height;
            row.transition = Selectable.Transition.None;
            return row;
        }

        // The Options screen's own ornament under the title; a gold rule when the game has none to lend.
        private void AddOrnament(MenuScreen optionsScreen)
        {
            var source = optionsScreen.topFleur ? optionsScreen.topFleur.GetComponent<Image>() : null;
            if (!source || !source.sprite)
            {
                AddRule(body, 330f);
                return;
            }
            var size = source.rectTransform.rect.size * 0.6f;
            var fleur = NewImage(body, "Fleur", source.sprite, size);
            fleur.preserveAspect = true;
            fleur.rectTransform.anchoredPosition = new Vector2(0f, 322f);
        }

        private void AddRule(RectTransform parent, float y)
        {
            var rule = NewImage(parent, "Rule", PanelArt.Rule, new Vector2(RowWidth, 4f));
            rule.color = new Color(PanelArt.Gold.r, PanelArt.Gold.g, PanelArt.Gold.b, 0.55f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, y);
        }

        private Image NewArrow(RectTransform row, float left, float rotation)
        {
            var arrow = NewImage(row, "Arrow", PanelArt.Arrow, new Vector2(22f, 22f));
            arrow.rectTransform.anchoredPosition = new Vector2(left - RowWidth / 2f, 0f);
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
            return arrow;
        }

        private Text NewText(RectTransform parent, string name, int size, TextAnchor alignment)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = PanelArt.Ink;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Image NewImage(RectTransform parent, string name, Sprite sprite, Vector2 size)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Positions a text inside its row by its left edge, measured from the row's left edge.
        private static void PlaceInRow(Text text, float left, float width) =>
            Place(text.rectTransform, new Vector2(left + width / 2f - RowWidth / 2f, 0f), new Vector2(width, 60f));
    }
}
