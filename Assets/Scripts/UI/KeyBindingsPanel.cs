using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UntitledPoolGame.Core
{
    // "Réglages > Touches" (prototype docs/ui/parcours.html, settings page),
    // shared by the main menu and the pause menu: one row per action, a
    // keyboard / mouse cell and a gamepad cell. Confirm on a cell, then press
    // the new key or button (Input System interactive rebinding on the first
    // player's actions; KeyBindings saves the overrides and copies them to the
    // other players). A key already used by another action is swapped with
    // it. Movement, look and pause are shown but not rebindable here.
    // Reads its own input (keyboard, any gamepad, mouse) while open; the host
    // calls Tick every frame and closes it when Tick returns false.
    public class KeyBindingsPanel
    {
        private const string KeyboardGroup = "Keyboard&Mouse", GamepadGroup = "Gamepad";

        private class Row
        {
            public string id, action, label, sub, kbFixed, padFixed;
            public readonly Label[] caps = new Label[2];
            public bool Fixed => action == null;
            public string Id => id ?? action;
        }

        private class Cell
        {
            public VisualElement element;
            public Label label;
            public Action activate;
        }

        private readonly List<Row> rows = new List<Row>
        {
            new Row { id = "Move", label = "Se déplacer", sub = "en visée : point de frappe", kbFixed = "Z Q S D · flèches", padFixed = "Stick G" },
            new Row { id = "Look", label = "Regarder", sub = "en visée : tourner autour de la bille", kbFixed = "Souris", padFixed = "Stick D" },
            new Row { action = "Interact", label = "Interagir", sub = "ramasser · viser · valider" },
            new Row { action = "Attack", label = "Tirer · lancer · coup de queue", sub = "maintenir puis relâcher" },
            new Row { action = "Punch", label = "Coup de poing", sub = "mains vides" },
            new Row { action = "Kick", label = "Coup de pied", sub = "maintenir : spartiate" },
            new Row { action = "Next", label = "Pouvoir", sub = "mode Pouvoirs" },
            new Row { action = "Sprint", label = "Courir", sub = "" },
            new Row { action = "Jump", label = "Sauter", sub = "prévu" },
            new Row { id = "Pause", label = "Pause", sub = "", kbFixed = "Échap", padFixed = "Start" },
        };

        // On the keyboard, the shot and the punch share the left click on
        // purpose (what's in the hands decides): not a conflict to swap.
        private static readonly HashSet<string> SharedPairs = new HashSet<string> { "Attack|Punch", "Punch|Attack" };

        public VisualElement Root { get; }
        private readonly MenuElement frame;
        private readonly List<List<Cell>> grid = new List<List<Cell>>();
        private int focusRow, focusCol;
        private Label message;

        private InputActionRebindingExtensions.RebindingOperation rebind;
        private (Row row, int col)? pending;
        private InputAction rebindAction;
        private int rebindIndex = -1;
        private string rebindOldPath;
        private bool rebindWasEnabled;
        private float inputCooldown;
        private Vector2 lastMouse;
        private readonly Dictionary<Gamepad, Vector2> padLatch = new Dictionary<Gamepad, Vector2>();
        private bool closeRequested;

        public bool IsOpen => Root.style.display == DisplayStyle.Flex;

        public KeyBindingsPanel(VisualElement parent)
        {
            Root = UiKit.PopCard(parent, "key-bindings");
            // Frame from the "panel" element of the Settings screen (Menu
            // Studio): place, size, colours, tilt, title.
            frame = MenuLayouts.Get(MenuLayouts.Settings).Find("panel");
            if (frame != null)
            {
                UiKit.Place(Root, frame.position.x, frame.position.y);
                Root.style.width = frame.size.x;
                Root.style.height = frame.size.y;
                Root.style.backgroundColor = frame.color;
                if (frame.borderWidth > 0f) UiKit.Border(Root, frame.borderWidth, frame.borderColor);
                Root.style.borderBottomWidth = Mathf.Max(frame.borderWidth, 10f);
                Root.style.rotate = new Rotate(new Angle(frame.rotation, AngleUnit.Degree));
            }
            else UiKit.Rect(Root, 9f, 5f, 9f, 5f);
            Root.style.flexDirection = FlexDirection.Column;
            UiKit.Pad(Root, 40, 24);
            Root.style.display = DisplayStyle.None;
            Build();
        }

        private void Build()
        {
            // Texts from MenuSettings; a row it doesn't list keeps its default.
            MenuSettings cfg = MenuSettings.Instance;
            foreach (Row row in rows)
            {
                KeyRowText text = cfg.KeyRow(row.Id);
                if (text == null) continue;
                if (!string.IsNullOrEmpty(text.label)) row.label = text.label;
                row.sub = text.sub;
            }

            string titleText = frame != null && !string.IsNullOrEmpty(frame.text) ? frame.text : cfg.settingsTitle;
            Label title = UiKit.Text(Root, titleText, UiKit.TitleFont, 60, frame != null ? frame.textColor : UiKit.Ink, false);
            title.style.unityTextAlign = TextAnchor.MiddleLeft;

            VisualElement tabs = UiKit.Box(Root, "tabs");
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginTop = 8;
            tabs.style.marginBottom = 14;
            var tabNames = new List<(string, bool)>();
            for (int i = 0; i < cfg.settingsTabs.Count; i++) tabNames.Add((cfg.settingsTabs[i], i == 0));
            foreach (var (name, on) in tabNames)
            {
                VisualElement tab = UiKit.Box(tabs, "tab");
                UiKit.Pad(tab, 22, 6);
                tab.style.marginRight = 12;
                UiKit.Radius(tab, 12);
                UiKit.Border(tab, 3, on ? UiKit.Ink : new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.35f));
                tab.style.backgroundColor = on ? UiKit.Yellow : Color.white;
                if (on) tab.style.borderBottomWidth = 6;
                Label l = UiKit.Text(tab, on ? name : name + cfg.tabSoonSuffix, UiKit.BodyFont, 24, on ? UiKit.Ink : new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.45f), false);
            }

            // Column headers
            VisualElement head = Line(Root);
            HeadText(head, cfg.actionColumn, true);
            HeadText(head, cfg.keyboardColumn, false);
            HeadText(head, cfg.gamepadColumn, false);

            foreach (Row row in rows)
            {
                VisualElement line = Line(Root);
                VisualElement text = UiKit.Box(line, "label");
                text.style.flexGrow = 1;
                text.style.flexDirection = FlexDirection.Row;
                text.style.alignItems = Align.Center;
                Label main = UiKit.Text(text, row.label, UiKit.BodyFont, 25, UiKit.Ink, false);
                main.style.unityTextAlign = TextAnchor.MiddleLeft;
                if (!string.IsNullOrEmpty(row.sub))
                {
                    Label sub = UiKit.Text(text, "  " + row.sub, UiKit.BodyFont, 18, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.5f), false);
                    sub.style.unityTextAlign = TextAnchor.MiddleLeft;
                }
                var cells = new List<Cell>();
                for (int col = 0; col < 2; col++)
                {
                    VisualElement cap = UiKit.Box(line, "cap");
                    cap.style.width = 290;
                    cap.style.height = 50;
                    cap.style.marginLeft = 16;
                    cap.style.justifyContent = Justify.Center;
                    UiKit.Radius(cap, 12);
                    if (row.Fixed)
                    {
                        UiKit.Border(cap, 3, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.3f));
                    }
                    else
                    {
                        UiKit.Border(cap, 3, UiKit.Ink);
                        cap.style.borderBottomWidth = 6;
                        cap.style.backgroundColor = Color.white;
                    }
                    Label capText = UiKit.Text(cap, "", UiKit.BodyFont, 24, row.Fixed ? new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.5f) : UiKit.Ink, false);
                    row.caps[col] = capText;
                    if (!row.Fixed)
                    {
                        int c = col;
                        Row r = row;
                        cells.Add(new Cell { element = cap, label = capText, activate = () => RequestRebind(r, c) });
                    }
                }
                if (cells.Count > 0) grid.Add(cells);
            }

            VisualElement foot = UiKit.Box(Root, "foot");
            foot.style.flexDirection = FlexDirection.Row;
            foot.style.alignItems = Align.Center;
            foot.style.marginTop = StyleKeyword.Auto;
            var footCells = new List<Cell>
            {
                Pill(foot, cfg.resetButton, () => { CancelRebind(); KeyBindings.ResetAll(); Refresh(); Say("Touches par défaut rétablies"); }),
                Pill(foot, cfg.backButton, () => closeRequested = true),
            };
            grid.Add(footCells);
            message = UiKit.Text(foot, "", UiKit.BodyFont, 22, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.7f), false);
            message.style.flexGrow = 1;
            message.style.unityTextAlign = TextAnchor.MiddleRight;
            message.style.whiteSpace = WhiteSpace.Normal;
        }

        private static VisualElement Line(VisualElement parent)
        {
            VisualElement line = UiKit.Box(parent, "row");
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.marginBottom = 6;
            return line;
        }

        private static void HeadText(VisualElement parent, string text, bool grow)
        {
            Label l = UiKit.Text(parent, text, UiKit.BodyFont, 22, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.55f), false);
            if (grow) { l.style.flexGrow = 1; l.style.unityTextAlign = TextAnchor.MiddleLeft; }
            else { l.style.width = 290; l.style.marginLeft = 16; }
        }

        private Cell Pill(VisualElement parent, string text, Action activate)
        {
            VisualElement pill = UiKit.Box(parent, "pill");
            UiKit.Pad(pill, 30, 8);
            pill.style.marginRight = 16;
            UiKit.Radius(pill, 30);
            UiKit.Border(pill, 3, UiKit.Ink);
            pill.style.borderBottomWidth = 6;
            pill.style.backgroundColor = Color.white;
            Label l = UiKit.Text(pill, text, UiKit.BodyFont, 28, UiKit.Ink, false);
            return new Cell { element = pill, label = l, activate = activate };
        }

        // ---------- Open / close ----------

        public void Open()
        {
            Root.style.display = DisplayStyle.Flex;
            closeRequested = false;
            focusRow = 0; focusCol = 0;
            inputCooldown = 0.25f;
            Refresh();
            Say(SourceActions == null ? "Actions des joueurs introuvables : assigne-les dans GameFlowSettings (Player Actions)."
                                      : "Entrée / A sur une case pour la changer · Échap / B : retour");
        }

        public void Close()
        {
            CancelRebind();
            Root.style.display = DisplayStyle.None;
        }

        // The first player's actions are edited; KeyBindings copies to the
        // others. No player yet (main menu scene): a copy of the project's
        // player actions (GameFlowSettings), with the saved bindings on it.
        private static InputActionAsset menuCopy;
        private static InputActionAsset SourceActions
        {
            get
            {
                if (PlayerInput.all.Count > 0) return PlayerInput.all[0].actions;
                if (menuCopy == null)
                {
                    InputActionAsset project = GameFlowSettings.Instance.playerActions;
                    if (project == null) return null;
                    menuCopy = UnityEngine.Object.Instantiate(project);
                    KeyBindings.ApplyTo(menuCopy);
                }
                return menuCopy;
            }
        }

        private void Say(string text) => message.text = text;

        // ---------- Display ----------

        private void Refresh()
        {
            InputActionAsset actions = SourceActions;
            foreach (Row row in rows)
            {
                for (int col = 0; col < 2; col++)
                {
                    if (row.Fixed) { row.caps[col].text = col == 0 ? row.kbFixed : row.padFixed; continue; }
                    InputAction action = actions?.FindAction(row.action);
                    int index = action != null ? BindingIndex(action, col) : -1;
                    row.caps[col].text = index >= 0 ? Pretty(action.bindings[index].effectivePath) : "—";
                }
            }
            UpdateFocusVisuals();
        }

        private void UpdateFocusVisuals()
        {
            for (int r = 0; r < grid.Count; r++)
                for (int c = 0; c < grid[r].Count; c++)
                {
                    Cell cell = grid[r][c];
                    bool focused = r == focusRow && c == focusCol;
                    bool waiting = pending.HasValue && cell.element == CellFor(pending.Value.row, pending.Value.col)?.element;
                    cell.element.style.backgroundColor = waiting ? UiKit.Ink : focused ? UiKit.Yellow : Color.white;
                    cell.label.style.color = waiting ? UiKit.Yellow : UiKit.Ink;
                    float s = focused ? 1.05f : 1f;
                    cell.element.style.scale = new Scale(new Vector3(s, s, 1f));
                }
        }

        private Cell CellFor(Row row, int col)
        {
            int r = 0;
            foreach (Row each in rows)
            {
                if (each.Fixed) continue;
                if (each == row) return grid[r][col];
                r++;
            }
            return null;
        }

        private static string GroupOf(int col) => col == 0 ? KeyboardGroup : GamepadGroup;

        // The first plain (non-composite) binding of that device group.
        private static int BindingIndex(InputAction action, int col)
        {
            string group = GroupOf(col);
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite || string.IsNullOrEmpty(b.groups)) continue;
                foreach (string g in b.groups.Split(';'))
                    if (g == group) return i;
            }
            return -1;
        }

        public static string Pretty(string path)
        {
            if (string.IsNullOrEmpty(path)) return "—";
            string lower = path.ToLowerInvariant();
            string control = path.Substring(path.LastIndexOf('/') + 1);
            if (lower.StartsWith("<mouse>"))
                return control switch
                {
                    "leftButton" => "Clic G", "rightButton" => "Clic D", "middleButton" => "Clic milieu",
                    "forwardButton" => "Souris avant", "backButton" => "Souris arrière", _ => control,
                };
            if (lower.StartsWith("<gamepad>"))
            {
                if (lower.Contains("/dpad/"))
                    return "Croix " + (control == "up" ? "↑" : control == "down" ? "↓" : control == "left" ? "←" : "→");
                return control switch
                {
                    "buttonSouth" => "A", "buttonEast" => "B", "buttonWest" => "X", "buttonNorth" => "Y",
                    "leftShoulder" => "LB", "rightShoulder" => "RB", "leftTrigger" => "LT", "rightTrigger" => "RT",
                    "start" => "Start", "select" => "Select", "leftStickPress" => "L3", "rightStickPress" => "R3", _ => control,
                };
            }
            if (lower.StartsWith("<keyboard>"))
            {
                switch (control)
                {
                    case "space": return "Espace";
                    case "enter": return "Entrée";
                    case "escape": return "Échap";
                    case "tab": return "Tab";
                    case "backspace": return "Retour arr.";
                    case "leftShift": return "Maj G";
                    case "rightShift": return "Maj D";
                    case "leftCtrl": return "Ctrl G";
                    case "rightCtrl": return "Ctrl D";
                    case "leftAlt": return "Alt";
                    case "rightAlt": return "Alt Gr";
                }
            }
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
        }

        // ---------- Rebinding ----------

        private void RequestRebind(Row row, int col)
        {
            if (rebind != null || pending.HasValue) return;
            InputActionAsset actions = SourceActions;
            if (actions == null) { Say("Aucun joueur pour l'instant."); return; }
            InputAction action = actions.FindAction(row.action);
            int index = action != null ? BindingIndex(action, col) : -1;
            if (index < 0) { Say("Pas de touche à modifier ici."); return; }
            pending = (row, col);
            rebindAction = action;
            rebindIndex = index;
            row.caps[col].text = col == 0 ? "Appuie…" : "Bouton…";
            Say(col == 0 ? "Appuie sur une touche ou un bouton de souris (Échap : annuler)" : "Appuie sur un bouton de la manette (Échap : annuler)");
            UpdateFocusVisuals();
        }

        // Started once the key that opened it is released (it would
        // otherwise be taken as the new binding).
        private void StartRebind()
        {
            var (row, col) = pending.Value;
            rebindOldPath = rebindAction.bindings[rebindIndex].effectivePath;
            rebindWasEnabled = rebindAction.enabled;
            rebindAction.Disable();
            rebind = rebindAction.PerformInteractiveRebinding(rebindIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f);
            if (col == 0)
                rebind.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>")
                      .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")
                      .WithControlsExcluding("<Mouse>/scroll").WithControlsExcluding("<Keyboard>/anyKey");
            else
                rebind.WithControlsHavingToMatchPath("<Gamepad>")
                      .WithControlsExcluding("<Gamepad>/leftStick").WithControlsExcluding("<Gamepad>/rightStick");
            rebind.OnComplete(_ => FinishRebind(true)).OnCancel(_ => FinishRebind(false)).Start();
        }

        private void FinishRebind(bool done)
        {
            var (row, col) = pending.Value;
            InputAction action = rebindAction;
            rebind?.Dispose();
            rebind = null;
            pending = null;
            if (rebindWasEnabled) action.Enable();
            inputCooldown = 0.25f;

            if (!done) { Refresh(); Say("Annulé"); return; }
            string newPath = action.bindings[rebindIndex].effectivePath;
            string said = $"{row.label} : {Pretty(newPath)}";
            // Already used by another action of the same device: swapped.
            InputActionAsset actions = action.actionMap.asset;
            foreach (Row other in rows)
            {
                if (other.Fixed || other == row || SharedPairs.Contains(row.action + "|" + other.action)) continue;
                InputAction otherAction = actions.FindAction(other.action);
                int otherIndex = otherAction != null ? BindingIndex(otherAction, col) : -1;
                if (otherIndex < 0 || otherAction.bindings[otherIndex].effectivePath != newPath) continue;
                otherAction.ApplyBindingOverride(otherIndex, rebindOldPath);
                said = $"« {Pretty(newPath)} » était pris par {other.label} : échangé";
            }
            KeyBindings.SaveFrom(actions);
            Refresh();
            Say(said);
        }

        private void CancelRebind()
        {
            if (rebind != null) rebind.Cancel();
            pending = null;
        }

        // ---------- Input ----------

        // Call every frame while open. False = the player asked to leave.
        public bool Tick(float dt)
        {
            if (!IsOpen) return false;
            if (closeRequested) { Close(); return false; }

            if (pending.HasValue && rebind == null)
            {
                if (!AnyHeld()) StartRebind();
                return true;
            }
            if (rebind != null) return true;
            if (inputCooldown > 0f) { inputCooldown -= dt; return true; }

            Vector2 nav = Vector2.zero;
            bool confirm = false, back = false;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) nav = Vector2.down;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) nav = Vector2.up;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) nav = Vector2.left;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) nav = Vector2.right;
                confirm |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                back |= kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
            }
            foreach (Gamepad pad in Gamepad.all)
            {
                Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                Vector2 dir = stick.magnitude > 0.6f
                    ? (Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2(Mathf.Sign(stick.x), 0f) : new Vector2(0f, -Mathf.Sign(stick.y)))
                    : Vector2.zero;
                padLatch.TryGetValue(pad, out Vector2 held);
                if (dir != Vector2.zero && dir != held) nav = dir;
                padLatch[pad] = dir;
                confirm |= pad.buttonSouth.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null && Root.panel != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Vector2 p = RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(screen.x, Screen.height - screen.y));
                for (int r = 0; r < grid.Count; r++)
                    for (int c = 0; c < grid[r].Count; c++)
                        if (grid[r][c].element.worldBound.Contains(p))
                        {
                            if ((screen - lastMouse).sqrMagnitude > 1f) { focusRow = r; focusCol = c; }
                            if (mouse.leftButton.wasPressedThisFrame) { focusRow = r; focusCol = c; confirm = true; }
                        }
                lastMouse = screen;
            }

            if (back) { Close(); return false; }
            if (nav.y != 0f) { focusRow = Mathf.Clamp(focusRow + (int)nav.y, 0, grid.Count - 1); }
            if (nav.x != 0f) { focusCol += (int)nav.x; }
            focusCol = Mathf.Clamp(focusCol, 0, grid[focusRow].Count - 1);
            if (confirm) grid[focusRow][focusCol].activate?.Invoke();
            if (closeRequested) { Close(); return false; }
            UpdateFocusVisuals();
            return true;
        }

        private static bool AnyHeld()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed)) return true;
            foreach (Gamepad pad in Gamepad.all)
                if (pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.startButton.isPressed) return true;
            return false;
        }
    }
}
