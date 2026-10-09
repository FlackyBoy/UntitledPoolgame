using UnityEngine;
using UntitledPoolGame.Pool;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.Core
{
    // The built-in layout of every menu screen — what the menus looked like
    // before Menu Studio, texts taken from MenuSettings. Used to create the
    // Resources/Menus assets, to reset a screen from Menu Studio, and as a
    // fallback when an asset is missing.
    public static class MenuDefaults
    {
        public static MenuScreen Create(string id)
        {
            MenuScreen s = ScriptableObject.CreateInstance<MenuScreen>();
            s.name = id;
            MenuSettings m = MenuSettings.Instance;
            switch (id)
            {
                case MenuLayouts.Title: Title(s, m); break;
                case MenuLayouts.NewGame: Slots(s, m, false); break;
                case MenuLayouts.Load: Slots(s, m, true); break;
                case MenuLayouts.Level: Level(s, m); break;
                case MenuLayouts.Mode: Mode(s, m); break;
                case MenuLayouts.Multiplayer: Multiplayer(s, m); break;
                case MenuLayouts.Lobby: Lobby(s, m); break;
                case MenuLayouts.Settings: Settings(s, m); break;
                case MenuLayouts.Pause: Pause(s, m); break;
                case MenuLayouts.PauseConfirm: PauseConfirm(s, m); break;
                case MenuLayouts.Loading: Loading(s); break;
            }
            // The main menu pages keep the background chosen in MenuSettings
            // before Menu Studio existed.
            if (s.backdrop == MenuBackdrop.Bar && id != MenuLayouts.Loading)
            {
                s.barLights = m.barLights;
                if (m.background != null)
                {
                    s.backdrop = MenuBackdrop.Image;
                    s.backgroundImage = m.background;
                }
            }
            return s;
        }

        // ---------- Screens ----------

        private static void Title(MenuScreen s, MenuSettings m)
        {
            Cue(s, 84f, 84f, 64f);
            if (m.logo != null)
            {
                MenuElement logo = Add(s, "logo", MenuElementKind.Image, 50f, 27f, m.logoHeight * m.logo.width / Mathf.Max(1f, m.logo.height), m.logoHeight);
                logo.image = m.logo;
                logo.entrance = MenuEntrance.Drop;
            }
            else
            {
                Heading(s, "title-small", m.titleSmall, 50f, 20.9f, 56f).entrance = MenuEntrance.Drop;
                MenuElement big = Heading(s, "title-big", m.titleBig, 50f, 30.1f, 110f);
                big.titleFont = true;
                big.entrance = MenuEntrance.Drop;
                big.entranceDelay = 0.08f;
            }
            Ball(s, "new-game", 30f, 47f, m.newGameBall, MenuAction.OpenScreen, 0).targetScreen = MenuLayouts.NewGame;
            Ball(s, "load", 50f, 47f, m.loadBall, MenuAction.OpenScreen, 1).targetScreen = MenuLayouts.Load;
            Ball(s, "just-for-fun", 70f, 47f, m.justForFunBall, MenuAction.JustForFun, 2);
            Ball(s, "multiplayer", 30f, 70f, m.multiplayerBall, MenuAction.OpenScreen, 3).targetScreen = MenuLayouts.Multiplayer;
            Ball(s, "settings", 50f, 70f, m.settingsBall, MenuAction.OpenScreen, 4).targetScreen = MenuLayouts.Settings;
            Ball(s, "quit", 70f, 70f, m.quitBall, MenuAction.Quit, 5);
            AimHint(s, m);
            KeysHint(s, m.titleKeysHint);
        }

        private static void Slots(MenuScreen s, MenuSettings m, bool loading)
        {
            s.backScreen = MenuLayouts.Title;
            Heading(s, "heading", loading ? m.loadHeading : m.newGameHeading, 50f, 23f, 80f);
            for (int i = 0; i < 3; i++)
            {
                MenuElement card = Add(s, "slot" + (i + 1), MenuElementKind.Card, 30f + i * 20f, 48f, 330f, 300f);
                card.rotation = i == 0 ? -2f : i == 2 ? 2f : 0f;
                card.color = loading ? new Color(K.Card.r, K.Card.g, K.Card.b, 0.6f) : K.Card;
                card.text = Safe(m.slotFormat, i + 1);
                card.titleFont = true;
                card.textSize = 34f;
                card.textColor = K.Ink;
                card.textShadow = false;
                card.subText = (loading ? "–  " : "+  ") + (loading ? m.slotEmpty : m.slotFree);
                card.subTextSize = 30f;
                card.action = loading ? MenuAction.LoadSlot : MenuAction.NewGameSlot;
                card.focusScale = 0.06f;
                card.entranceDelay = 0.06f * i;
            }
            MenuElement note = Add(s, "note", MenuElementKind.Panel, 50f, 76f, 883f, 120f);
            note.color = K.Cube;
            note.rotation = -1f;
            note.text = loading ? m.loadNote : m.newGameNote;
            note.textSize = 28f;
            note.textColor = Color.white;
            note.textShadow = false;
            note.entrance = MenuEntrance.Rise;
            note.entranceDelay = 0.2f;
            KeysHint(s, m.pageKeysHint);
        }

        private static void Level(MenuScreen s, MenuSettings m)
        {
            s.backScreen = MenuLayouts.Title;   // Lobby in local multiplayer (decided by the menu)
            Context(s);
            Heading(s, "heading", m.levelHeading, 50f, 23f, 80f);
            MenuElement levels = Add(s, "levels", MenuElementKind.Slot, 50f, 54f, 1500f, 480f);
            levels.entrance = MenuEntrance.None;
            KeysHint(s, m.pageKeysHint);
        }

        private static void Mode(MenuScreen s, MenuSettings m)
        {
            s.backScreen = MenuLayouts.Level;
            Cue(s, 50f, 84f, 70f);
            Context(s);
            Heading(s, "heading", m.modeHeading, 30f, 25f, 70f);
            ModeBall(s, "classic", 18f, 44f, m.classicMode, PoolGameMode.EightBall, 0);
            ModeBall(s, "powers", 34f, 44f, m.powersMode, PoolGameMode.Party, 1);
            ModeBall(s, "nine-ball", 18f, 71f, m.nineBallMode, PoolGameMode.NineBall, 2);
            ModeBall(s, "fourteen-one", 34f, 71f, m.fourteenOneMode, PoolGameMode.FourteenOne, 3);

            MenuElement rules = Add(s, "rules", MenuElementKind.Panel, 67.5f, 47.5f, 634f, 300f);
            rules.color = K.Cube;
            rules.rotation = 2f;
            rules.titleFont = true;
            rules.textSize = 54f;
            rules.subTextSize = 30f;
            rules.textColor = Color.white;
            rules.textShadow = false;
            rules.entrance = MenuEntrance.SlideFromRight;
            AimHint(s, m);
            KeysHint(s, m.pageKeysHint);
        }

        private static void Multiplayer(MenuScreen s, MenuSettings m)
        {
            s.backScreen = MenuLayouts.Title;
            Cue(s, 50f, 85f, 64f);
            Heading(s, "heading", m.multiplayerHeading, 50f, 23f, 80f);
            Ball(s, "local", 36f, 49f, m.localBall, MenuAction.PlayLocal, 0).size = new Vector2(170f, 170f);
            Ball(s, "online", 64f, 49f, m.onlineBall, MenuAction.PlayOnline, 1).size = new Vector2(170f, 170f);
            AimHint(s, m);
            KeysHint(s, m.pageKeysHint);
        }

        private static void Lobby(MenuScreen s, MenuSettings m)
        {
            s.backScreen = MenuLayouts.Multiplayer;
            Cue(s, 17f, 82f, 70f);
            Heading(s, "heading", m.lobbyHeading, 50f, 24f, 84f);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 32f : 68f;
                Add(s, "slot" + (i + 1), MenuElementKind.Slot, x, 48f, 170f, 170f).entrance = MenuEntrance.None;
                MenuElement label = Add(s, "slot" + (i + 1) + "-label", MenuElementKind.Text, x, 66f, 0f, 0f);
                label.textSize = 40f;
                label.entrance = MenuEntrance.Fade;
            }
            MenuElement go = Add(s, "go", MenuElementKind.Pill, 50f, 84f, 430f, 88f);
            go.color = K.Hex("#f07b12");
            go.number = "5";
            go.text = m.goText;
            go.titleFont = true;
            go.textSize = 46f;
            go.textColor = K.Chalk;
            go.action = MenuAction.LobbyGo;
            go.focusScale = 0.08f;
            go.entrance = MenuEntrance.Rise;
            AimHint(s, m);
            KeysHint(s, m.pageKeysHint);
        }

        private static void Settings(MenuScreen s, MenuSettings m)
        {
            s.backScreen = MenuLayouts.Title;
            MenuElement panel = Add(s, "panel", MenuElementKind.Slot, 50f, 50f, 1574f, 972f);
            panel.color = K.Card;
            panel.borderColor = K.Ink;
            panel.borderWidth = 4f;
            panel.text = m.settingsTitle;
            panel.textColor = K.Ink;
            panel.entrance = MenuEntrance.Pop;
        }

        private static void Pause(MenuScreen s, MenuSettings m)
        {
            s.backdrop = MenuBackdrop.Dim;
            MenuElement who = Add(s, "who", MenuElementKind.Panel, 50f, 10.5f, 520f, 54f);
            who.color = K.P1;
            who.borderWidth = 3f;
            who.borderColor = K.Ink;
            who.cornerRadius = 12f;
            who.textSize = 26f;
            who.textColor = Color.white;
            who.textShadow = false;
            who.entrance = MenuEntrance.Drop;

            MenuElement card = Add(s, "card", MenuElementKind.Panel, 50f, 51f, 560f, 470f);
            card.color = K.Card;
            card.borderWidth = 4f;
            card.borderColor = K.Ink;
            card.cornerRadius = 16f;
            card.rotation = -1.5f;
            MenuElement title = Heading(s, "title", m.pauseTitle, 50f, 36.5f, 96f);
            title.titleFont = true;
            title.textColor = K.Ink;
            title.textShadow = false;
            title.rotation = -1.5f;
            Pill(s, "resume", 50f, 47.5f, m.resumeItem, MenuAction.Resume, -1.5f, 0);
            Pill(s, "settings", 50f, 56.3f, m.pauseSettingsItem, MenuAction.OpenSettings, -1.5f, 1);
            Pill(s, "main-menu", 50f, 65.1f, m.mainMenuItem, MenuAction.AskMainMenu, -1.5f, 2);
        }

        private static void PauseConfirm(MenuScreen s, MenuSettings m)
        {
            s.backdrop = MenuBackdrop.Dim;
            MenuElement card = Add(s, "card", MenuElementKind.Panel, 50f, 50f, 760f, 330f);
            card.color = K.Card;
            card.borderWidth = 4f;
            card.borderColor = K.Ink;
            card.cornerRadius = 16f;
            card.rotation = 1f;
            MenuElement title = Heading(s, "title", m.confirmTitle, 50f, 41f, 58f);
            title.titleFont = true;
            title.textColor = K.Ink;
            title.textShadow = false;
            MenuElement text = Heading(s, "text", m.confirmText, 50f, 48.5f, 28f);
            text.textColor = new Color(K.Ink.r, K.Ink.g, K.Ink.b, 0.7f);
            text.textShadow = false;
            Pill(s, "stay", 40.5f, 57.5f, m.confirmStayItem, MenuAction.ConfirmStay, 0f, 0).size = new Vector2(330f, 80f);
            Pill(s, "quit", 59.5f, 57.5f, m.confirmQuitItem, MenuAction.ConfirmQuit, 0f, 1).size = new Vector2(330f, 80f);
        }

        private static void Loading(MenuScreen s)
        {
            s.table = false;
            MenuElement context = Add(s, "context", MenuElementKind.Panel, 18.5f, 8.6f, 540f, 58f);
            context.color = K.Yellow;
            context.borderWidth = 3f;
            context.borderColor = K.Ink;
            context.cornerRadius = 10f;
            context.rotation = -3f;
            context.textSize = 28f;
            context.textColor = K.Ink;
            context.textShadow = false;
            context.entrance = MenuEntrance.Drop;

            MenuElement postcard = Add(s, "postcard", MenuElementKind.Slot, 50f, 36f, 760f, 520f);
            postcard.rotation = -2f;
            postcard.entrance = MenuEntrance.Pop;
            MenuElement tip = Add(s, "tip", MenuElementKind.Slot, 85f, 15.5f, 422f, 230f);
            tip.color = K.Cube;
            tip.rotation = 2f;
            tip.entrance = MenuEntrance.SlideFromRight;
            MenuElement status = Add(s, "status", MenuElementKind.Text, 50f, 69f, 0f, 0f);
            status.textSize = 46f;
            status.entrance = MenuEntrance.None;
            Add(s, "ready", MenuElementKind.Slot, 50f, 69f, 900f, 100f).entrance = MenuEntrance.None;
            MenuElement gutter = Add(s, "gutter", MenuElementKind.Slot, 50f, 89.6f, 1459f, 96f);
            gutter.color = K.Hex("#4a2a16");
            gutter.entrance = MenuEntrance.Rise;
            MenuElement go = Add(s, "go", MenuElementKind.Text, 50f, 40f, 0f, 0f);
            go.titleFont = true;
            go.textSize = 150f;
            go.textColor = K.Yellow;
            go.entrance = MenuEntrance.None;
        }

        // ---------- Pieces ----------

        private static MenuElement Add(MenuScreen s, string id, MenuElementKind kind, float x, float y, float w, float h)
        {
            var e = new MenuElement
            {
                id = id, kind = kind, position = new Vector2(x, y), size = new Vector2(w, h),
                text = "", subText = "", textColor = K.Chalk, borderColor = K.Ink, entrance = MenuEntrance.Pop,
                focusScale = kind == MenuElementKind.Ball ? 0.18f : 0.06f,
            };
            s.elements.Add(e);
            return e;
        }

        private static MenuElement Heading(MenuScreen s, string id, string text, float x, float y, float size)
        {
            MenuElement h = Add(s, id, MenuElementKind.Text, x, y, 0f, 0f);
            h.text = text;
            h.textSize = size;
            h.entrance = MenuEntrance.Drop;
            return h;
        }

        private static MenuElement Ball(MenuScreen s, string id, float x, float y, MenuBall b, MenuAction action, int order)
        {
            MenuElement e = Add(s, id, MenuElementKind.Ball, x, y, 110f, 110f);
            e.color = b.color;
            e.number = b.number;
            e.stripe = b.stripe;
            e.text = b.label;
            e.subText = b.sub ?? "";
            e.action = action;
            e.entranceDelay = 0.05f * order;
            return e;
        }

        private static void ModeBall(MenuScreen s, string id, float x, float y, MenuModeBall b, PoolGameMode mode, int order)
        {
            MenuElement e = Ball(s, id, x, y, b.ball, MenuAction.StartMode, order);
            e.mode = mode;
            e.description = b.rules;
        }

        private static MenuElement Pill(MenuScreen s, string id, float x, float y, MenuBall b, MenuAction action, float rotation, int order)
        {
            MenuElement e = Add(s, id, MenuElementKind.Pill, x, y, 480f, 80f);
            e.color = b.color;
            e.number = b.number;
            e.stripe = b.stripe;
            e.text = b.label;
            e.titleFont = true;
            e.textSize = 40f;
            e.textColor = K.Ink;
            e.textShadow = false;
            e.action = action;
            e.rotation = rotation;
            e.focusScale = 0.05f;
            e.entrance = MenuEntrance.SlideFromLeft;
            e.entranceDelay = 0.05f * order;
            return e;
        }

        private static void Context(MenuScreen s)
        {
            MenuElement c = Add(s, "context", MenuElementKind.Panel, 22.9f, 19.3f, 420f, 60f);
            c.color = K.Yellow;
            c.borderWidth = 3f;
            c.borderColor = K.Ink;
            c.cornerRadius = 10f;
            c.rotation = -3f;
            c.textSize = 24f;
            c.textColor = K.Ink;
            c.textShadow = false;
            c.entrance = MenuEntrance.Drop;
        }

        private static void Cue(MenuScreen s, float x, float y, float size)
        {
            s.hasCue = true;
            s.cuePosition = new Vector2(x, y);
            s.cueSize = size;
        }

        private static void AimHint(MenuScreen s, MenuSettings m)
        {
            MenuElement hint = Add(s, "aim-hint", MenuElementKind.Text, 84f, 91f, 0f, 0f);
            hint.text = m.aimHint;
            hint.textSize = 34f;
            hint.entrance = MenuEntrance.Fade;
            hint.entranceDelay = 0.4f;
        }

        private static void KeysHint(MenuScreen s, string text)
        {
            MenuElement keys = Add(s, "keys-hint", MenuElementKind.Text, 50f, 97.5f, 0f, 0f);
            keys.text = text;
            keys.textSize = 20f;
            keys.textColor = new Color(1f, 1f, 1f, 0.6f);
            keys.textShadow = false;
            keys.entrance = MenuEntrance.Fade;
        }

        private static string Safe(string format, object value)
        {
            try { return string.Format(format ?? "", value); }
            catch (System.FormatException) { return format; }
        }
    }
}
