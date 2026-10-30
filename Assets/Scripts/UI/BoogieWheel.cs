using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.Core
{
    // Boogie Night's wheel of fortune, over the level before each match: it
    // spins, lands on a mode, then the type and the powers are drawn too.
    // The players start the match or spin again (BoogieNightSettings'
    // Rerolls Per Spin). Opened by the loading screen for the first match
    // and by the HUD's end card for the next ones; starts the match through
    // PoolMatchRules (RequestStart, then RequestRestart with the new setup).
    public class BoogieWheel : MonoBehaviour
    {
        private const float WheelSize = 620f;

        private static BoogieWheel instance;
        public static bool IsOpen => instance != null && instance.phase != Phase.Closed;

        public static void Open()
        {
            if (instance == null)
            {
                var go = new GameObject("Roue (Boogie Night)");
                go.SetActive(false);
                // Over the HUD (90), under the menu and the pause (100).
                go.AddComponent<UIDocument>().panelSettings = K.CreatePanel(95);
                instance = go.AddComponent<BoogieWheel>();
                go.SetActive(true);
            }
            instance.Begin();
        }

        private enum Phase { Closed, Spinning, Reveal, Waiting }

        private BoogieNightSettings cfg;
        private MenuSettings menu;
        private UIDocument document;
        private VisualElement root, wheel, resultCard;
        private Label roundLabel, score1, score2, modeLabel, typeLabel, powersLabel, hintLabel;
        private readonly List<Color> sliceColors = new List<Color>();
        private List<PoolGameMode> slices;

        private Phase phase = Phase.Closed;
        private PoolMatchSetup drawn;
        private int rerollsLeft;
        private float time, angle, startAngle, targetAngle;
        private bool externalMenuBefore;

        private void OnEnable()
        {
            cfg = BoogieNightSettings.Instance;
            menu = MenuSettings.Instance;
            document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning("[BoogieNight] Pas de panneau UI Toolkit : pas de roue.");
                return;
            }
            Build();
            root.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (phase != Phase.Closed) PoolMatchRules.ExternalMenu = externalMenuBefore;
        }

        // ---------- Building ----------

        private void Build()
        {
            root = document.rootVisualElement;
            K.Fill(root);
            root.style.backgroundColor = new Color(0.03f, 0.02f, 0.01f, 0.72f);

            K.Place(K.Shadowed(K.Text(root, cfg.wheelTitle, K.TitleFont, 96, K.Yellow)).parent, 50f, 9f);
            roundLabel = K.Shadowed(K.Text(root, "", K.BodyFont, 36, K.Chalk), 3f);
            K.Place(roundLabel.parent, 50f, 18f);

            // J1 n – n J2.
            VisualElement scores = K.Box(root, "scores");
            scores.style.flexDirection = FlexDirection.Row;
            scores.style.alignItems = Align.Center;
            K.Place(scores, 50f, 27f);
            K.BallVisual(scores, 74, K.P1, "J1", false);
            score1 = K.Text(scores, "0", K.TitleFont, 60, K.Chalk, false);
            score1.style.marginLeft = score1.style.marginRight = 18;
            K.Text(scores, "–", K.TitleFont, 60, K.Chalk, false);
            score2 = K.Text(scores, "0", K.TitleFont, 60, K.Chalk, false);
            score2.style.marginLeft = score2.style.marginRight = 18;
            K.BallVisual(scores, 74, K.P2, "J2", false);

            BuildWheel();

            resultCard = K.PopCard(root, "result");
            resultCard.style.width = 600;
            resultCard.style.alignItems = Align.FlexStart;
            K.Pad(resultCard, 34, 24);
            K.Place(resultCard, 74f, 58f);
            resultCard.style.rotate = new Rotate(new Angle(2f, AngleUnit.Degree));
            modeLabel = K.Text(resultCard, "?", K.TitleFont, 64, K.Ink, false);
            typeLabel = Chip(resultCard);
            powersLabel = Chip(resultCard);

            hintLabel = K.Shadowed(K.Text(root, "", K.BodyFont, 32, K.Chalk), 3f);
            K.Place(hintLabel.parent, 74f, 84f);
        }

        private static Label Chip(VisualElement parent)
        {
            Label chip = K.Text(parent, "", K.BodyFont, 32, Color.white, false);
            chip.style.marginTop = 14;
            chip.style.backgroundColor = K.Cube;
            K.Pad(chip, 18, 6);
            K.Radius(chip, 12);
            return chip;
        }

        // The slices turn under a fixed pointer; slice i spans
        // [i, i + 1] × step degrees clockwise from the top.
        private void BuildWheel()
        {
            VisualElement holder = K.Box(root, "wheel-holder");
            holder.style.width = holder.style.height = WheelSize;
            K.Place(holder, 37f, 60f);

            wheel = K.Box(holder, "wheel");
            K.Fill(wheel);
            List<PoolGameMode> modes = slices = BoogieNight.Slices(cfg);
            int n = Mathf.Max(1, modes.Count);
            float step = 360f / n, radius = WheelSize / 2f;
            for (int i = 0; i < modes.Count; i++)
            {
                MenuBall ball = menu.ModeBall(modes[i]).ball;
                sliceColors.Add(ball.color);
                float mid = -90f + (i + 0.5f) * step;
                Vector2 dir = new Vector2(Mathf.Cos(mid * Mathf.Deg2Rad), Mathf.Sin(mid * Mathf.Deg2Rad));
                Label name = K.Text(wheel, PoolMatchSetup.ModeName(modes[i]), K.BodyFont, 26, ReadableOn(ball.color));
                VisualElement at = name.parent;
                at.style.position = Position.Absolute;
                at.style.left = radius + dir.x * radius * 0.6f;
                at.style.top = radius + dir.y * radius * 0.6f;
                at.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                // Along the radius, never upside down.
                at.style.rotate = new Rotate(new Angle(dir.x < -0.01f ? mid + 180f : mid, AngleUnit.Degree));
            }
            wheel.generateVisualContent += DrawSlices;

            VisualElement hub = K.Disc(holder, 50f, 50f, 110f, K.BallCream);
            K.Border(hub, 6, K.Ink);
            hub.style.justifyContent = Justify.Center;
            K.Text(hub, "?", K.TitleFont, 54, K.Ink, false).style.unityTextAlign = TextAnchor.MiddleCenter;

            VisualElement pointer = K.Box(holder, "pointer");
            pointer.style.position = Position.Absolute;
            pointer.style.width = 70;
            pointer.style.height = 80;
            pointer.style.left = radius - 35f;
            pointer.style.top = -46;
            pointer.generateVisualContent += DrawPointer;
        }

        private static Color ReadableOn(Color c) => c.grayscale > 0.6f ? K.Ink : Color.white;

        private void DrawSlices(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 1f || sliceColors.Count == 0) return;
            Painter2D p = ctx.painter2D;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) / 2f - 5f;
            float step = 360f / sliceColors.Count;
            for (int i = 0; i < sliceColors.Count; i++)
            {
                p.fillColor = sliceColors[i];
                p.BeginPath();
                p.MoveTo(c);
                p.Arc(c, radius, new Angle(-90f + i * step, AngleUnit.Degree), new Angle(-90f + (i + 1) * step, AngleUnit.Degree));
                p.ClosePath();
                p.Fill();
            }
            p.strokeColor = K.Ink;
            p.lineWidth = 5f;
            for (int i = 0; i < sliceColors.Count; i++)
            {
                float a = (-90f + i * step) * Mathf.Deg2Rad;
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                p.Stroke();
            }
            p.lineWidth = 10f;
            p.BeginPath();
            p.Arc(c, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.Stroke();
        }

        private static void DrawPointer(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 1f) return;
            Painter2D p = ctx.painter2D;
            p.fillColor = K.Yellow;
            p.strokeColor = K.Ink;
            p.lineWidth = 5f;
            p.BeginPath();
            p.MoveTo(new Vector2(4f, 4f));
            p.LineTo(new Vector2(r.width - 4f, 4f));
            p.LineTo(new Vector2(r.width / 2f, r.height - 4f));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        // ---------- Flow ----------

        private void Begin()
        {
            if (root == null) return;
            if (phase == Phase.Closed) externalMenuBefore = PoolMatchRules.ExternalMenu;
            // No OnGUI mode select under the wheel before the first match.
            PoolMatchRules.ExternalMenu = true;
            root.style.display = DisplayStyle.Flex;
            rerollsLeft = cfg.rerollsPerSpin;
            TextFx.Set(roundLabel, BoogieNight.FinalRound ? cfg.finalRoundText : Format(cfg.roundFormat, BoogieNight.Played + 1, BoogieNight.Rounds));
            TextFx.Set(score1, BoogieNight.Wins(0).ToString());
            TextFx.Set(score2, BoogieNight.Wins(1).ToString());
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            Spin();
        }

        private void Spin()
        {
            drawn = BoogieNight.Draw(cfg, slices, out int slice);
            float step = 360f / Mathf.Max(1, sliceColors.Count);
            // The pointer (top) ends somewhere inside the slice, never on an edge.
            float inSlice = (Mathf.Max(0, slice) + Random.Range(0.15f, 0.85f)) * step;
            startAngle = Mathf.Repeat(angle, 360f);
            float delta = Mathf.Repeat(-inSlice - startAngle, 360f);
            targetAngle = startAngle + 360f * cfg.spinTurns + delta;
            time = 0f;
            phase = Phase.Spinning;
            TextFx.Set(modeLabel, "?");
            TextFx.Set(typeLabel, "…");
            TextFx.Set(powersLabel, "…");
            TextFx.Set(hintLabel, "");
            resultCard.style.opacity = 0.55f;
        }

        private void Update()
        {
            if (phase == Phase.Closed || root == null) return;
            float dt = Time.unscaledDeltaTime;
            time += dt;
            switch (phase)
            {
                case Phase.Spinning:
                    float k = Mathf.Clamp01(time / cfg.spinDuration);
                    angle = Mathf.Lerp(startAngle, targetAngle, 1f - Mathf.Pow(1f - k, 3f));
                    wheel.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
                    if (k >= 1f)
                    {
                        phase = Phase.Reveal;
                        time = 0f;
                        resultCard.style.opacity = 1f;
                        TextFx.Reveal(modeLabel, PoolMatchSetup.ModeName(drawn.mode));
                    }
                    break;
                case Phase.Reveal:
                    // Type and powers flicker a moment, then settle.
                    bool settle = time > 0.8f;
                    bool flip = !settle && Mathf.Repeat(time, 0.16f) < 0.08f;
                    PoolMatchType type = flip ? PoolMatchSetup.OtherType(drawn.type) : drawn.type;
                    bool powers = flip ? !drawn.powers : drawn.powers;
                    TextFx.Set(typeLabel, PoolMatchSetup.TypeName(type));
                    TextFx.Set(powersLabel, powers ? cfg.powersOnText : cfg.powersOffText);
                    if (settle)
                    {
                        phase = Phase.Waiting;
                        TextFx.Reveal(hintLabel, rerollsLeft > 0 ? cfg.playHint + "     " + Format(cfg.rerollHintFormat, rerollsLeft) : cfg.playHint);
                    }
                    break;
                case Phase.Waiting:
                    ReadInput(out bool play, out bool reroll);
                    if (reroll && rerollsLeft > 0)
                    {
                        rerollsLeft--;
                        Spin();
                    }
                    else if (play) Play();
                    break;
            }
        }

        private static void ReadInput(out bool play, out bool reroll)
        {
            play = reroll = false;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                play = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                reroll = kb.rKey.wasPressedThisFrame;
            }
            foreach (Gamepad pad in Gamepad.all)
            {
                // Not Start: it opens the pause.
                play |= pad.buttonSouth.wasPressedThisFrame;
                reroll |= pad.buttonNorth.wasPressedThisFrame;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null) play |= mouse.leftButton.wasPressedThisFrame;
        }

        private void Play()
        {
            phase = Phase.Closed;
            root.style.display = DisplayStyle.None;
            PoolMatchRules.ExternalMenu = externalMenuBefore;
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null)
            {
                Debug.LogWarning("[BoogieNight] Pas de PoolMatchRules dans le niveau : la partie ne peut pas démarrer.");
                return;
            }
            Debug.Log($"[BoogieNight] Partie {BoogieNight.Played + 1} / {BoogieNight.Rounds} : {drawn.Describe()}.");
            if (rules.MatchStarted) rules.RequestRestart(drawn);
            else rules.RequestStart(drawn);
        }

        // A text from the settings with its {0} {1}; a typo must not throw.
        private static string Format(string format, params object[] values)
        {
            try { return string.Format(format ?? "", values); }
            catch (System.FormatException) { return format; }
        }
    }
}
