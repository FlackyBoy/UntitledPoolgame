using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Shouts, power cards and the little animations behind them.
    public partial class MatchHud
    {
        // ---------- Effects ----------

        // From a Feel sequence (MMF_PoolHudText): a big word on that player's
        // half (-1 = both). color null = yellow.
        public static void ShowShout(int player, string markup, Color? color = null, bool burst = false)
        {
            MatchHud hud = FindAnyObjectByType<MatchHud>();
            if (hud == null) return;
            Color c = color ?? hud.Yellow;
            if (player >= 0) { hud.Shout(player, markup, c, burst); return; }
            foreach (Half h in hud.halves)
                if (h != null && h.input != null) hud.Shout(h.player, markup, c, burst);
        }

        public static float ShoutDuration =>
            PoolSettingsLoader.LoadOrDefault<HudSettings>("HudSettings").shoutDuration;

        private void Shout(int player, string text, Color color, bool burst)
        {
            Half h = HalfOf(player);
            // An emptied text in the settings turns that interjection off.
            if (h == null || string.IsNullOrEmpty(text)) return;
            Enqueue(h, h.shouts, () => ShowShoutNow(h, text, color, burst));
        }

        private Fx ShowShoutNow(Half h, string text, Color color, bool burst)
        {
            // Big outlined word that pops in tilted, then floats away.
            VisualElement holder = Box(h.fx, "shout");
            if (burst)
            {
                VisualElement rays = Box(h.fx, "burst");
                rays.PlaceBehind(holder);
                rays.style.position = Position.Absolute;
                rays.style.width = rays.style.height = 460;
                rays.style.left = Length.Percent(50);
                rays.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                rays.generateVisualContent += ctx => DrawRays(ctx, new Color(1f, 0.82f, 0.25f, 0.55f));
                AddFx(rays, Mathf.Max(0.05f, cfg.burstDuration), (fx, u) =>
                {
                    float s = Mathf.Lerp(0.2f, 1.3f, u);
                    fx.element.style.scale = new Scale(new Vector3(s, s, 1f));
                    fx.element.style.rotate = new Rotate(new Angle(60f * u, AngleUnit.Degree));
                    fx.element.style.opacity = 1f - u;
                    fx.element.style.top = ShoutCenter(h, holder);
                });
            }

            holder.style.position = Position.Absolute;
            holder.style.left = Length.Percent(50);
            holder.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            Label label = Text(holder, text, titleFont, cfg.shoutFontSize, color);
            TextFx.Reveal(label, text);   // letters pop in one after the other
            label.style.unityTextOutlineWidth = 3f;
            label.style.unityTextOutlineColor = Ink;
            InkShadow(label, 7);
            float life = Mathf.Max(0.05f, cfg.shoutDuration);
            Fx shout = AddFx(holder, life, (fx, u) =>
            {
                float s, rot = -6f, op = 1f, rise = 0f;
                if (u < 0.22f) { float k = u / 0.22f; s = Mathf.Lerp(0.2f, 1.12f, k); rot = Mathf.Lerp(-18f, -6f, k); op = k; }
                else if (u < 0.35f) s = Mathf.Lerp(1.12f, 1f, (u - 0.22f) / 0.13f);
                else s = 1f;
                if (u > 0.8f) { float k = (u - 0.8f) / 0.2f; op = 1f - k; rise = 0.07f * k; }
                fx.element.style.scale = new Scale(new Vector3(s, s, 1f));
                fx.element.style.rotate = new Rotate(new Angle(rot, AngleUnit.Degree));
                fx.element.style.opacity = op;
                fx.element.style.top = ShoutCenter(h, holder) - rise * ContentHeight(h);
            });
            shout.exitAt = 0.8f * life;
            return shout;
        }

        // 40 % down the half, or just under the incoming card when there is
        // one (stacked under the power slot, it reaches that low).
        private float ShoutCenter(Half h, VisualElement shout)
        {
            float center = 0.4f * ContentHeight(h);
            VisualElement card = h.incoming.current?.element;
            if (card == null || card.panel == null) return center;
            float cardBottom = card.layout.yMax, half = shout.layout.height * 0.5f;
            if (float.IsNaN(cardBottom) || float.IsNaN(half)) return center;
            return Mathf.Max(center, cardBottom + 8f + half);
        }

        private static float ContentHeight(Half h)
        {
            float height = h.content.layout.height;
            return float.IsNaN(height) || height < 1f ? 1080f : height;
        }

        // Top right, one under the other: power slot, refusal hint, incoming
        // card (the slot's size varies, so no fixed offsets).
        private void StackUnderSlot(Half h)
        {
            Rect slot = h.slot.layout;
            if (float.IsNaN(slot.height)) return;
            float y = slot.y + slot.height * Mathf.Max(0.1f, cfg.powerSlotScale) + 6f;
            if (h.refusedHint != null && h.refusedHint.panel != null)
            {
                h.refusedHint.style.top = y;
                float hintHeight = h.refusedHint.layout.height;
                if (!float.IsNaN(hintHeight)) y += hintHeight + 6f;
            }
            VisualElement card = h.incoming.current?.element;
            if (card != null && card.panel != null) card.style.top = Mathf.Max(y + 4f, 0.24f * ContentHeight(h));
        }

        private void Enqueue(Half h, Lane lane, Func<Fx> show)
        {
            while (lane.waiting.Count >= MaxWaiting) lane.waiting.Dequeue();
            lane.waiting.Enqueue(show);
            PumpLanes(h);
        }

        private void PumpLanes(Half h)
        {
            Pump(h.shouts);
            Pump(h.sponsors);
            Pump(h.incoming);
            if (h.input != null) StackUnderSlot(h);
        }

        private void Pump(Lane lane)
        {
            if (lane.current != null && lane.current.done) lane.current = null;
            if (lane.waiting.Count == 0) return;
            if (lane.current == null)
            {
                lane.current = lane.waiting.Dequeue()();
                return;
            }
            // Readable long enough: its exit starts now, the next one follows.
            Fx fx = lane.current;
            float minShown = lane.card ? cfg.minCardTime : cfg.minShoutTime;
            if (fx.time >= minShown && fx.time < fx.exitAt) fx.time = fx.exitAt;
        }

        // "CE TOUR VOUS EST OFFERT PAR …": the power picked up, as a sponsor.
        private void Sponsor(int player, PoolPower power)
        {
            Half h = HalfOf(player);
            if (h == null) return;
            Enqueue(h, h.sponsors, () =>
            {
                string kind = power.Type switch { PowerType.Attack => cfg.attackLabel, PowerType.Defense => cfg.defenseLabel, _ => cfg.effectLabel };
                VisualElement card = PowerCard(h, "sponsor", power, cfg.sponsorHeader, power.PowerName + cfg.sponsorNameSuffix, 40, false, Format(cfg.sponsorLineFormat, kind));
                card.style.left = Length.Percent(4);
                // Clear of the hint banner (bottom 15 %) when its text is long.
                card.style.bottom = Length.Percent(23);
                card.style.width = Length.Percent(60);
                return SlideCard(card, Mathf.Max(1f, cfg.sponsorDuration), -1f, 0.45f);
            });
        }

        // "J1 T'ENVOIE ...": the target's card when a power is launched, from
        // the right (the sponsor card comes from the left), under the slot.
        private void Incoming(int player, PoolPower power, string header, string line)
        {
            Half h = HalfOf(player);
            if (h == null) return;
            Enqueue(h, h.incoming, () =>
            {
                VisualElement card = PowerCard(h, "incoming", power, header, power.PowerName, 36, true, line);
                card.style.right = Length.Percent(4);
                card.style.top = Length.Percent(24);
                card.style.width = Length.Percent(52);
                return SlideCard(card, Mathf.Max(1f, cfg.incomingDuration), 1f, 0.4f);
            });
        }

        // A power card: a strip in the power's colour with the header, the
        // big name, a smaller line under it.
        private VisualElement PowerCard(Half h, string name, PoolPower power, string header, string title, float titleSize, bool revealTitle, string line)
        {
            VisualElement card = PopCard(h.fx, name);
            card.style.position = Position.Absolute;
            card.style.overflow = Overflow.Hidden;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = 0;
            card.style.paddingBottom = 10;
            VisualElement head = Box(card, "head");
            head.style.backgroundColor = powerSettings != null ? powerSettings.GetColor(power.Type) : Red;
            head.style.paddingLeft = head.style.paddingRight = 16;
            head.style.paddingTop = head.style.paddingBottom = 5;
            head.style.borderBottomWidth = 3;
            head.style.borderBottomColor = Ink;
            Text(head, header, bodyFont, 16, Color.white).style.unityTextAlign = TextAnchor.MiddleLeft;
            Label titleLabel = Text(card, title, titleFont, titleSize, Ink);
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.marginLeft = 16;
            titleLabel.style.marginTop = 6;
            if (revealTitle) TextFx.Reveal(titleLabel, title);
            if (!string.IsNullOrEmpty(line))
            {
                Label small = Text(card, line, bodyFont, 20, new Color(Ink.r, Ink.g, Ink.b, 0.75f));
                small.style.unityTextAlign = TextAnchor.MiddleLeft;
                small.style.marginLeft = 16;
            }
            return card;
        }

        // Slides in tilted from its side (-1 left, 1 right), stays, slides
        // back out over the last 0.3 s (where a Lane cuts it short).
        private Fx SlideCard(VisualElement card, float life, float side, float inTime)
        {
            Fx fx = AddFx(card, life, (f, u) =>
            {
                float t = u * life, x;
                if (t < inTime) x = 120f * side * (1f - BackOut(t / inTime));
                else if (t > life - 0.3f) x = 130f * side * ((t - (life - 0.3f)) / 0.3f);
                else x = 0f;
                f.element.style.translate = new Translate(Length.Percent(x), 0);
                f.element.style.rotate = new Rotate(new Angle(t < inTime ? 6f * side * (1f - t / inTime) : 0f, AngleUnit.Degree));
            });
            fx.exitAt = life - 0.3f;
            return fx;
        }

        // A quick "no" shake, sideways: leaves scale/rotate to Pop.
        private void Wiggle(VisualElement e, float duration)
        {
            AddFx(e, duration, (fx, u) =>
            {
                float x = u < 1f ? Mathf.Sin(u * Mathf.PI * 6f) * 10f * (1f - u) : 0f;
                fx.element.style.translate = new Translate(x, 0);
            }, removeAtEnd: false);
        }

        private void Pop(VisualElement e, float duration)
        {
            AddFx(e, duration, (fx, u) =>
            {
                float s = u < 0.7f ? Mathf.Lerp(0.2f, 1.25f, u / 0.7f) : Mathf.Lerp(1.25f, 1f, (u - 0.7f) / 0.3f);
                fx.element.style.scale = new Scale(new Vector3(s, s, 1f));
                fx.element.style.rotate = new Rotate(new Angle(u < 0.7f ? Mathf.Lerp(-180f, 10f, u / 0.7f) : Mathf.Lerp(10f, 0f, (u - 0.7f) / 0.3f), AngleUnit.Degree));
            }, removeAtEnd: false);
        }

        private Fx AddFx(VisualElement e, float life, Action<Fx, float> animate, bool removeAtEnd = true)
        {
            var fx = new Fx { element = e, life = life, exitAt = life, animate = animate, keep = !removeAtEnd };
            effects.Add(fx);
            animate(fx, 0f);
            return fx;
        }

        private void UpdateEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Fx fx = effects[i];
                fx.time += dt;
                float u = Mathf.Clamp01(fx.time / fx.life);
                fx.animate(fx, u);
                if (u < 1f) continue;
                effects.RemoveAt(i);
                fx.done = true;
                if (fx.keep)
                {
                    fx.element.style.scale = new Scale(Vector3.one);
                    fx.element.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
                }
                else fx.element.RemoveFromHierarchy();
            }
        }

        private static float BackOut(float k)
        {
            const float c = 1.70158f;
            float x = k - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
