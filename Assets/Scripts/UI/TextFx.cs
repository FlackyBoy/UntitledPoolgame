using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace UntitledPoolGame.Core
{
    // Letter-by-letter animation of UI Toolkit labels, our own take on
    // Febucci's Text Animator: effects are written as tags right in the texts
    // of the settings assets (MenuSettings, HudSettings,
    // LoadingScreenSettings…), so the designer animates a word without code.
    //
    //   persistent, while the text is shown: <wave> <shake> <wiggle> <bounce>
    //     <pulse> <swing> <rainbow>, closed by </wave>…; a= and s= scale the
    //     force and the speed: <wave a=2 s=0.5>
    //   entrance of the letters inside: <pop> <drop> <fade> <slide> <grow>
    //   typewriter only: <pause=0.4> waits, <speed=2> types faster
    //
    // Other tags (<b>, <color=…>, <size=…>) are Unity's rich text and pass
    // through untouched. Each letter is moved, scaled, turned and tinted on
    // its four vertices after Unity lays the text out
    // (TextElement.PostProcessTextVertices, Unity 6), so a label stays a
    // single element whatever the length. Strengths and timings:
    // TextFxSettings (Resources).
    //
    // Set: the text, with its effects, shown at once (cheap when the text has
    // no tag; calling it every frame with the same text does nothing).
    // Reveal: letters pop in one after the other. Type: typewriter. Hide:
    // letters leave.
    public static class TextFx
    {
        private enum Kind { Wave, Shake, Wiggle, Bounce, Pulse, Swing, Rainbow }

        private struct Effect
        {
            public Kind kind;
            public int start, end;     // letters [start, end) of the parsed text
            public float amplitude, speed;
        }

        private struct EntranceSpan
        {
            public TextFxEntrance entrance;
            public int start, end;
        }

        private class State
        {
            public string markup;      // what was asked
            public string shown;       // what the label got (our tags removed)
            public int letters;        // length of the parsed text
            public string plain;       // the parsed text, for punctuation and callbacks
            public readonly List<Effect> effects = new List<Effect>();
            public readonly List<EntranceSpan> entrances = new List<EntranceSpan>();
            public float[] appearAt;   // per letter, seconds after start; null = all visible
            public TextFxEntrance defaultEntrance = TextFxEntrance.None;
            public float start, hideAt = -1f, done;
            public bool shownFired, hiddenFired, broken;
            public int lettersFired;
            public Action onShown, onHidden;
            public Action<char> onLetter;
            public IVisualElementScheduledItem tick;
        }

        private static readonly ConditionalWeakTable<Label, State> states = new ConditionalWeakTable<Label, State>();
        private static bool warned;

        // ---------- API ----------

        public static void Set(Label label, string markup)
        {
            if (label == null) return;
            states.TryGetValue(label, out State old);
            if (old != null && old.markup == markup && old.hideAt < 0f && label.text == old.shown) return;
            if (string.IsNullOrEmpty(markup) || markup.IndexOf('<') < 0)
            {
                Detach(label);
                label.text = markup ?? "";
                return;
            }
            State s = Parse(markup, false);
            // Letters inside an entrance tag still come in, one after the
            // other; the others are there at once.
            if (s.entrances.Count > 0)
            {
                s.appearAt = Stagger(s, TextFxSettings.Instance.revealStagger);
                for (int i = 0; i < s.letters; i++)
                    if (EntranceOf(s, i) == TextFxEntrance.None) s.appearAt[i] = 0f;
            }
            Attach(label, s);
        }

        public static void Reveal(Label label, string markup, Action onShown = null)
        {
            if (label == null) return;
            State s = Parse(markup ?? "", false);
            s.defaultEntrance = TextFxSettings.Instance.defaultEntrance;
            s.appearAt = Stagger(s, TextFxSettings.Instance.revealStagger);
            s.onShown = onShown;
            Attach(label, s);
        }

        public static void Type(Label label, string markup, Action onShown = null, Action<char> onLetter = null)
        {
            if (label == null) return;
            State s = Parse(markup ?? "", true);
            s.defaultEntrance = TextFxSettings.Instance.defaultEntrance;
            s.onShown = onShown;
            s.onLetter = onLetter;
            Attach(label, s);
        }

        // Letters leave one after the other; the text stays (invisible)
        // until something else is set.
        public static void Hide(Label label, Action onHidden = null)
        {
            if (label == null) return;
            if (!states.TryGetValue(label, out State s))
            {
                s = Parse(label.text ?? "", false);
                Attach(label, s);
            }
            s.hideAt = Now(s);
            s.hiddenFired = false;
            s.onHidden = onHidden;
            Wake(label, s);
        }

        // Typewriter or reveal finished at once.
        public static void Skip(Label label)
        {
            if (label == null || !states.TryGetValue(label, out State s) || s.appearAt == null) return;
            float now = Now(s);
            for (int i = 0; i < s.appearAt.Length; i++) s.appearAt[i] = Mathf.Min(s.appearAt[i], now);
            s.done = now;
        }

        public static bool IsAnimating(Label label) =>
            label != null && states.TryGetValue(label, out State s) && (!s.shownFired || (s.hideAt >= 0f && !s.hiddenFired));

        // The text as it reads, without our tags (Unity's rich text kept).
        public static string Strip(string markup) => markup == null ? "" : Parse(markup, false).shown;

        public static void Detach(Label label)
        {
            if (label == null || !states.TryGetValue(label, out State s)) return;
            s.tick?.Pause();
            label.PostProcessTextVertices = null;
            states.Remove(label);
            label.MarkDirtyRepaint();
        }

        // ---------- Parsing ----------

        private static State Parse(string markup, bool typewriter)
        {
            var s = new State { markup = markup };
            var shown = new StringBuilder(markup.Length);
            var plain = new StringBuilder(markup.Length);
            var open = new List<Effect>();
            var openEntrances = new List<EntranceSpan>();
            var times = typewriter ? new List<float>() : null;
            TextFxSettings cfg = TextFxSettings.Instance;
            float clock = 0f, speed = 1f, pendingPause = 0f;

            int i = 0;
            while (i < markup.Length)
            {
                char c = markup[i];
                if (c == '<')
                {
                    int close = markup.IndexOf('>', i + 1);
                    if (close > i)
                    {
                        string inner = markup.Substring(i + 1, close - i - 1).Trim();
                        if (TryOurTag(inner, plain.Length, open, openEntrances, s, ref pendingPause, ref speed))
                        {
                            i = close + 1;
                            continue;
                        }
                        // Unity's rich text: kept, not a letter.
                        shown.Append(markup, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }

                shown.Append(c);
                plain.Append(c);
                if (times != null)
                {
                    clock += pendingPause;
                    pendingPause = 0f;
                    times.Add(clock);
                    float step = 1f / (cfg.lettersPerSecond * Mathf.Max(0.05f, speed));
                    if (c == '.' || c == '!' || c == '?' || c == '…') step += cfg.sentencePause;
                    else if (c == ',' || c == ';' || c == ':') step += cfg.commaPause;
                    clock += step;
                }
                i++;
            }

            int n = plain.Length;
            foreach (Effect e in open) s.effects.Add(new Effect { kind = e.kind, start = e.start, end = n, amplitude = e.amplitude, speed = e.speed });
            foreach (EntranceSpan e in openEntrances) s.entrances.Add(new EntranceSpan { entrance = e.entrance, start = e.start, end = n });
            s.shown = shown.ToString();
            s.plain = plain.ToString();
            s.letters = n;
            if (times != null) s.appearAt = times.ToArray();
            return s;
        }

        private static bool TryOurTag(string inner, int at, List<Effect> open, List<EntranceSpan> openEntrances, State s,
            ref float pendingPause, ref float speed)
        {
            if (inner.Length == 0) return false;
            bool closing = inner[0] == '/';
            string body = closing ? inner.Substring(1).Trim() : inner;
            string[] parts = body.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            string head = parts[0].ToLowerInvariant();
            string name = head, value = null;
            int eq = head.IndexOf('=');
            if (eq > 0) { name = head.Substring(0, eq); value = head.Substring(eq + 1); }

            if (!closing && name == "pause") { pendingPause += ParseFloat(value, 0.3f); return true; }
            if (name == "speed") { speed = closing ? 1f : ParseFloat(value, 1f); return true; }

            if (TryKind(name, out Kind kind))
            {
                if (closing)
                {
                    int k = open.FindLastIndex(e => e.kind == kind);
                    if (k >= 0)
                    {
                        Effect e = open[k];
                        e.end = at;
                        s.effects.Add(e);
                        open.RemoveAt(k);
                    }
                    return true;
                }
                float a = 1f, sp = 1f;
                for (int p = 1; p < parts.Length; p++)
                {
                    string part = parts[p].ToLowerInvariant();
                    if (part.StartsWith("a=")) a = ParseFloat(part.Substring(2), 1f);
                    else if (part.StartsWith("s=")) sp = ParseFloat(part.Substring(2), 1f);
                }
                open.Add(new Effect { kind = kind, start = at, amplitude = a, speed = sp });
                return true;
            }

            if (TryEntrance(name, out TextFxEntrance entrance))
            {
                if (closing)
                {
                    int k = openEntrances.FindLastIndex(e => e.entrance == entrance);
                    if (k >= 0)
                    {
                        EntranceSpan e = openEntrances[k];
                        e.end = at;
                        s.entrances.Add(e);
                        openEntrances.RemoveAt(k);
                    }
                    return true;
                }
                openEntrances.Add(new EntranceSpan { entrance = entrance, start = at });
                return true;
            }
            return false;
        }

        private static bool TryKind(string name, out Kind kind)
        {
            switch (name)
            {
                case "wave": kind = Kind.Wave; return true;
                case "shake": kind = Kind.Shake; return true;
                case "wiggle": kind = Kind.Wiggle; return true;
                case "bounce": kind = Kind.Bounce; return true;
                case "pulse": kind = Kind.Pulse; return true;
                case "swing": kind = Kind.Swing; return true;
                case "rainbow": kind = Kind.Rainbow; return true;
                default: kind = Kind.Wave; return false;
            }
        }

        private static bool TryEntrance(string name, out TextFxEntrance entrance)
        {
            switch (name)
            {
                case "pop": entrance = TextFxEntrance.Pop; return true;
                case "drop": entrance = TextFxEntrance.Drop; return true;
                case "fade": entrance = TextFxEntrance.Fade; return true;
                case "slide": entrance = TextFxEntrance.Slide; return true;
                case "grow": entrance = TextFxEntrance.Grow; return true;
                default: entrance = TextFxEntrance.None; return false;
            }
        }

        private static float ParseFloat(string text, float fallback) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        // Letters one after the other (spaces don't take a turn).
        private static float[] Stagger(State s, float step)
        {
            var times = new float[s.letters];
            float t = 0f;
            for (int i = 0; i < s.letters; i++)
            {
                times[i] = t;
                if (!char.IsWhiteSpace(s.plain[i])) t += step;
            }
            return times;
        }

        // ---------- Running ----------

        private static void Attach(Label label, State s)
        {
            if (states.TryGetValue(label, out State old))
            {
                old.tick?.Pause();
                states.Remove(label);
            }
            s.start = Clock;
            float last = 0f;
            if (s.appearAt != null) foreach (float t in s.appearAt) last = Mathf.Max(last, t);
            s.done = last;
            states.Add(label, s);
            label.text = s.shown;
            label.PostProcessTextVertices = glyphs => Post(label, glyphs);
            Wake(label, s);
        }

        private static void Wake(Label label, State s)
        {
            if (s.tick == null) s.tick = label.schedule.Execute(() => Tick(label)).Every(16);
            else s.tick.Resume();
            label.MarkDirtyRepaint();
        }

        // Real time: runs through the pause (time scale 0) and in the editor
        // outside Play mode (Text FX Studio preview).
        private static float Clock => Time.realtimeSinceStartup;

        private static float Now(State s) => Clock - s.start;

        // Each frame while something moves: redraw, callbacks, then sleep
        // once the text is still (no persistent effect, nothing arriving).
        private static void Tick(Label label)
        {
            if (!states.TryGetValue(label, out State s)) return;
            TextFxSettings cfg = TextFxSettings.Instance;
            float now = Now(s);
            label.MarkDirtyRepaint();

            if (s.onLetter != null && s.appearAt != null)
                while (s.lettersFired < s.appearAt.Length && s.appearAt[s.lettersFired] <= now)
                {
                    char c = s.plain[s.lettersFired++];
                    if (!char.IsWhiteSpace(c)) s.onLetter(c);
                }

            bool arriving = s.appearAt != null && now < s.done + cfg.entranceDuration;
            if (!arriving && !s.shownFired) { s.shownFired = true; s.onShown?.Invoke(); }

            bool leaving = false;
            if (s.hideAt >= 0f)
            {
                leaving = now < s.hideAt + cfg.exitDuration + cfg.exitStagger * s.letters;
                if (!leaving && !s.hiddenFired) { s.hiddenFired = true; s.onHidden?.Invoke(); }
            }

            if (!arriving && !leaving && s.effects.Count == 0) s.tick?.Pause();
        }

        // After Unity has laid the text out: each letter's quad is moved,
        // scaled, turned and tinted around its own center.
        private static void Post(Label label, TextElement.GlyphsEnumerable glyphs)
        {
            if (!states.TryGetValue(label, out State s) || s.broken) return;
            if (label.text != s.shown) return;   // text replaced without TextFx
            try
            {
                TextFxSettings cfg = TextFxSettings.Instance;
                float now = Now(s);
                foreach (TextElement.Glyph glyph in glyphs)
                {
                    int i = glyph.textRange.start;
                    if (i < 0 || i >= s.letters) continue;
                    NativeSlice<Vertex> v = glyph.vertices;
                    if (v.Length < 4) continue;
                    Animate(s, cfg, i, now, v);
                }
            }
            catch (Exception e)
            {
                // A bug was reported in this Unity callback (6.5): the text
                // then stays still rather than breaking.
                s.broken = true;
                if (!warned) { warned = true; Debug.LogWarning("[TextFx] Animation de texte coupée : " + e.Message); }
            }
        }

        private static void Animate(State s, TextFxSettings cfg, int i, float now, NativeSlice<Vertex> v)
        {
            Vector3 center = (v[0].position + v[1].position + v[2].position + v[3].position) * 0.25f;
            float h = Mathf.Max(4f, Mathf.Abs(v[1].position.y - v[0].position.y));
            Vector2 offset = Vector2.zero;   // UI Toolkit: y goes down
            float scale = 1f, angle = 0f, alpha = 1f;
            Color? tint = null;

            // Entrance
            if (s.appearAt != null)
            {
                float local = now - s.appearAt[i];
                if (local < 0f) alpha = 0f;
                else if (local < cfg.entranceDuration)
                {
                    float k = local / cfg.entranceDuration;
                    switch (EntranceOf(s, i))
                    {
                        case TextFxEntrance.Pop: scale = BackOut(k); break;
                        case TextFxEntrance.Drop: offset.y -= (1f - BounceOut(k)) * h * 1.2f; alpha = Mathf.Clamp01(k * 4f); break;
                        case TextFxEntrance.Fade: alpha = k; break;
                        case TextFxEntrance.Slide: offset.x += (1f - CubicOut(k)) * h * 1.5f; alpha = k; break;
                        case TextFxEntrance.Grow: scale = CubicOut(k); offset.y += (1f - CubicOut(k)) * h * 0.4f; break;
                    }
                }
            }

            // Persistent effects
            foreach (Effect e in s.effects)
            {
                if (i < e.start || i >= e.end) continue;
                TextFxTuning t = Tuning(cfg, e.kind);
                float a = e.amplitude * t.amplitude, sp = e.speed * t.speed;
                switch (e.kind)
                {
                    case Kind.Wave: offset.y -= Mathf.Sin(now * 6f * sp + i * 0.55f) * h * 0.16f * a; break;
                    case Kind.Bounce: offset.y -= Mathf.Abs(Mathf.Sin(now * 5f * sp + i * 0.45f)) * h * 0.25f * a; break;
                    case Kind.Shake:
                        int frame = Mathf.FloorToInt(now * 24f * sp);
                        offset.x += (Hash(frame * 7 + i * 131) - 0.5f) * h * 0.14f * a;
                        offset.y += (Hash(frame * 13 + i * 71 + 5) - 0.5f) * h * 0.14f * a;
                        break;
                    case Kind.Wiggle:
                        offset.x += Mathf.Sin(now * 8f * sp + i * 1.7f) * h * 0.05f * a;
                        offset.y += Mathf.Cos(now * 7f * sp + i * 2.3f) * h * 0.05f * a;
                        break;
                    case Kind.Pulse: scale *= 1f + Mathf.Sin(now * 6f * sp + i * 0.3f) * 0.12f * a; break;
                    case Kind.Swing: angle += Mathf.Sin(now * 5f * sp + i * 0.6f) * 12f * a; break;
                    case Kind.Rainbow: tint = Color.HSVToRGB(Mathf.Repeat(now * 0.25f * sp + i * 0.07f, 1f), Mathf.Clamp01(0.75f * a), 1f); break;
                }
            }

            // Exit
            if (s.hideAt >= 0f)
            {
                float k = Mathf.Clamp01((now - s.hideAt - i * cfg.exitStagger) / cfg.exitDuration);
                scale *= 1f - k;
                alpha *= 1f - k;
                offset.y += k * h * 0.3f;
            }

            if (offset == Vector2.zero && scale == 1f && angle == 0f && alpha >= 1f && tint == null) return;

            float rad = angle * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            for (int j = 0; j < 4; j++)
            {
                Vertex vx = v[j];
                Vector3 d = vx.position - center;
                d = new Vector3(d.x * cos - d.y * sin, d.x * sin + d.y * cos, d.z) * scale;
                vx.position = center + d + (Vector3)offset;
                Color32 c = vx.tint;
                if (tint.HasValue) { Color32 r = tint.Value; c.r = r.r; c.g = r.g; c.b = r.b; }
                c.a = (byte)(c.a * Mathf.Clamp01(alpha));
                vx.tint = c;
                v[j] = vx;
            }
        }

        private static TextFxEntrance EntranceOf(State s, int i)
        {
            for (int k = s.entrances.Count - 1; k >= 0; k--)
                if (i >= s.entrances[k].start && i < s.entrances[k].end) return s.entrances[k].entrance;
            return s.defaultEntrance;
        }

        private static TextFxTuning Tuning(TextFxSettings cfg, Kind kind) => kind switch
        {
            Kind.Wave => cfg.wave,
            Kind.Shake => cfg.shake,
            Kind.Wiggle => cfg.wiggle,
            Kind.Bounce => cfg.bounce,
            Kind.Pulse => cfg.pulse,
            Kind.Swing => cfg.swing,
            _ => cfg.rainbow,
        };

        private static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.5453f, 1f);

        private static float CubicOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        private static float BackOut(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private static float BounceOut(float k)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (k < 1f / d1) return n1 * k * k;
            if (k < 2f / d1) { k -= 1.5f / d1; return n1 * k * k + 0.75f; }
            if (k < 2.5f / d1) { k -= 2.25f / d1; return n1 * k * k + 0.9375f; }
            k -= 2.625f / d1;
            return n1 * k * k + 0.984375f;
        }
    }
}
