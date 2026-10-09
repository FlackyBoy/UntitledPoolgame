using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // In-game pause (prototype docs/ui/parcours.html, "Pause A · carte"):
    // Escape or Start during a match freezes the game (time scale 0, every
    // player's input switched off) and shows the "Pause" screen over it —
    // Reprendre, Réglages (the key bindings panel), Menu principal (with the
    // "PauseConfirm" screen first). Both screens are MenuScreens edited in
    // Tools > Pool > Menu Studio; the elements with an action are the
    // choices, in their list order. The "who" element shows who paused. In
    // split screen it stops both players. Created automatically in any scene
    // that has a PoolMatchRules; drawn above the HUD and the main menu.
    public class PauseMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        private static void TryCreate()
        {
            if (FindAnyObjectByType<PoolMatchRules>() == null || FindAnyObjectByType<PauseMenu>() != null) return;
            var go = new GameObject("Pause (Synthèse)");
            go.SetActive(false);
            go.AddComponent<UIDocument>().panelSettings = UiKit.CreatePanel(110);
            go.AddComponent<PauseMenu>();
            go.SetActive(true);
        }

        // Read elsewhere (e.g. to ignore input while paused).
        public static bool IsPaused { get; private set; }

        private class Item
        {
            public MenuNode view;
            public Action activate;
            public VisualElement Element => view.visual;
        }

        private UIDocument document;
        private VisualElement root;
        private MenuView pauseView, confirmView;
        private MenuNode who;
        private readonly List<Item> cardItems = new List<Item>(), confirmItems = new List<Item>();
        private List<Item> items;
        private int focused, lastFocused = -1;
        private KeyBindingsPanel settings;
        private float savedTimeScale = 1f;
        private float openedAt;
        private Vector2 lastMouse;
        private readonly Dictionary<Gamepad, float> padLatch = new Dictionary<Gamepad, float>();

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null) { enabled = false; return; }
            Build();
            Hide();
        }

        // Never leave the game frozen behind (component or scene going away).
        private void OnDisable()
        {
            if (IsPaused && settings != null) Resume();
            IsPaused = false;
        }

        // ---------- Building ----------

        private void Build()
        {
            root = document.rootVisualElement;
            UiKit.Fill(root);
            root.pickingMode = PickingMode.Ignore;

            pauseView = MenuRenderer.Build(root, MenuLayouts.Get(MenuLayouts.Pause));
            confirmView = MenuRenderer.Build(root, MenuLayouts.Get(MenuLayouts.PauseConfirm));
            who = pauseView.Item("who");
            Collect(pauseView, cardItems);
            Collect(confirmView, confirmItems);
            settings = new KeyBindingsPanel(root);
        }

        private void Collect(MenuView view, List<Item> list)
        {
            foreach (MenuNode i in view.Selectable)
            {
                MenuNode item = i;
                list.Add(new Item { view = item, activate = () => Do(item.data) });
            }
        }

        private void Do(MenuElement e)
        {
            MenuFeel.Play(e.feelOnPress);
            switch (e.action)
            {
                case MenuAction.Resume:
                case MenuAction.Back: Resume(); break;
                case MenuAction.OpenSettings: OpenSettings(); break;
                case MenuAction.AskMainMenu: ShowConfirm(true); break;
                case MenuAction.ConfirmStay: ShowConfirm(false); break;
                case MenuAction.ConfirmQuit: BackToMainMenu(); break;
                case MenuAction.Quit: BackToMainMenu(); break;
            }
        }

        // ---------- Open / close ----------

        private void Pause(int byPlayer)
        {
            IsPaused = true;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            foreach (PlayerInput player in PlayerInput.all) player.DeactivateInput();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            MenuSettings cfg = MenuSettings.Instance;
            if (who != null)
            {
                if (who.text != null) TextFx.Reveal(who.text, byPlayer >= 0 ? SafeFormat(cfg.pausedByFormat, byPlayer + 1) : cfg.pausedText);
                who.visual.style.backgroundColor = byPlayer == 1 ? UiKit.P2 : UiKit.P1;
            }
            root.style.display = DisplayStyle.Flex;
            ShowConfirm(false);
            openedAt = Time.unscaledTime;
        }

        private void Resume()
        {
            IsPaused = false;
            settings.Close();
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            foreach (PlayerInput player in PlayerInput.all) player.ActivateInput();
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null && rules.MatchStarted && !rules.GameOver)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
            Hide();
        }

        private void Hide()
        {
            root.style.display = DisplayStyle.None;
            pauseView.Stop();
            confirmView.Stop();
        }

        // A text from MenuSettings with its {0}; a typo in it must not throw.
        private static string SafeFormat(string format, object value)
        {
            try { return string.Format(format ?? "", value); }
            catch (FormatException) { return format; }
        }

        private void ShowConfirm(bool show)
        {
            pauseView.root.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            confirmView.root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) { pauseView.Stop(); confirmView.Replay(); }
            else { confirmView.Stop(); pauseView.Replay(); }
            items = show ? confirmItems : cardItems;
            focused = 0;
            lastFocused = -1;
            Refresh();
        }

        private void OpenSettings()
        {
            pauseView.root.style.display = DisplayStyle.None;
            confirmView.root.style.display = DisplayStyle.None;
            pauseView.Stop();
            settings.Open();
        }

        // To the main menu scene (GameFlowSettings); the current scene is
        // reloaded instead if the menu scene isn't in the build yet.
        private void BackToMainMenu()
        {
            IsPaused = false;
            GameSession.LoadMainMenu();
        }

        // ---------- Frame ----------

        private void Update()
        {
            if (root == null) return;
            PoolMatchRules rules = PoolMatchRules.Instance;

            if (!IsPaused)
            {
                if (rules == null || !rules.MatchStarted || rules.GameOver) return;
                int by = PausePressedBy();
                if (by != int.MinValue) Pause(by);
                return;
            }

            // Kept at 0 while paused: the pot slow motion or a hitstop
            // ending in real time would otherwise set it back to 1.
            Time.timeScale = 0f;
            float dt = Time.unscaledDeltaTime;

            if (settings.IsOpen)
            {
                if (!settings.Tick(dt)) ShowConfirm(false);
                return;
            }
            if (Time.unscaledTime - openedAt < 0.15f) return;   // the key that opened it
            ReadInput();
        }

        // Escape (keyboard player) or Start (any gamepad): which player asked.
        // int.MinValue = nobody this frame; -1 = unknown player.
        private static int PausePressedBy()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return PlayerUsing(kb);
            foreach (Gamepad pad in Gamepad.all)
                if (pad.startButton.wasPressedThisFrame) return PlayerUsing(pad);
            return int.MinValue;
        }

        private static int PlayerUsing(InputDevice device)
        {
            foreach (PlayerInput player in PlayerInput.all)
                foreach (InputDevice d in player.devices)
                    if (d == device) return player.playerIndex;
            return PlayerInput.all.Count == 1 ? PlayerInput.all[0].playerIndex : -1;
        }

        private void ReadInput()
        {
            if (items == null || items.Count == 0) { if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Resume(); return; }
            int nav = 0;
            bool confirmPressed = false, back = false;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) nav = -1;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) nav = 1;
                confirmPressed |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                back |= kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
            }
            foreach (Gamepad pad in Gamepad.all)
            {
                Vector2 v = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                float axis = Mathf.Abs(v.y) >= Mathf.Abs(v.x) ? -v.y : v.x;
                float dir = Mathf.Abs(axis) > 0.6f ? Mathf.Sign(axis) : 0f;
                padLatch.TryGetValue(pad, out float held);
                if (dir != 0f && dir != held) nav = (int)dir;
                padLatch[pad] = dir;
                confirmPressed |= pad.buttonSouth.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null && root.panel != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                for (int i = 0; i < items.Count; i++)
                    if (items[i].Element.worldBound.Contains(p))
                    {
                        if ((screen - lastMouse).sqrMagnitude > 1f) focused = i;
                        if (mouse.leftButton.wasPressedThisFrame) { focused = i; confirmPressed = true; }
                    }
                lastMouse = screen;
            }

            if (back)
            {
                if (items == confirmItems) ShowConfirm(false); else Resume();
                return;
            }
            if (nav != 0) focused = (focused + nav + items.Count) % items.Count;
            if (confirmPressed) { items[focused].activate?.Invoke(); return; }
            Refresh();
        }

        // The aimed choice: yellow, ink edge, a bit bigger (by its focus
        // scale); its Feel "on focus" plays when it changes.
        private void Refresh()
        {
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                bool on = i == focused;
                VisualElement e = items[i].Element;
                MenuElement data = items[i].view.data;
                if (data.kind == MenuElementKind.Pill)
                {
                    e.style.backgroundColor = on ? UiKit.Yellow : new Color(0f, 0f, 0f, 0.08f);
                    UiKit.Border(e, on ? 3 : 0, UiKit.Ink);
                    if (on) e.style.borderBottomWidth = 6;
                }
                else if (data.kind == MenuElementKind.Card)
                    e.style.backgroundColor = on ? Color.white : data.color;
                float s = on ? 1f + data.focusScale : 1f;
                e.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            if (focused != lastFocused && focused < items.Count)
            {
                if (lastFocused >= 0) MenuFeel.Play(items[focused].view.data.feelOnFocus);
                lastFocused = focused;
            }
        }
    }
}
