using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // The plate: the player's goal in each mode, and Power Wash's clock.
    public partial class MatchHud
    {
        // Rebuilt only when what it shows changes.
        private void UpdatePlate(Half h, PoolMatchRules rules)
        {
            int p = h.player;
            BallGroup group = rules.GetGroup(p);
            bool hasScore = rules.TryGetScore(p, out int score, out int target);
            // Play Anywhere: strokes, par and the round's step.
            PlayAnywhereRuleSet golf = rules.Mode == PoolGameMode.PlayAnywhere ? PlayAnywhereRuleSet.Current : null;
            int nextNine = rules.Mode == PoolGameMode.NineBall ? LowestBallOnTable(9) : 0;
            // Power Wash Pool: felt washed and the clock (-1 = overtime).
            PowerWashDirt wash = rules.Mode == PoolGameMode.PowerWashPool ? PowerWashDirt.Instance : null;
            // Pocket Size: the role, the round, the clock and the touches.
            PocketSizeRuleSet pocketSize = rules.Mode == PoolGameMode.PocketSize ? PocketSizeRuleSet.Current : null;
            // Shooter miniature: health and eliminations. Shooter Billard: group and balls left.
            ShooterMiniRuleSet shooter = ShooterMiniRuleSet.Current;
            // Bowling Pool: the score, the frame and the throws so far.
            BowlingPoolRuleSet bowling = rules.Mode == PoolGameMode.BowlingPool ? BowlingPoolRuleSet.Current : null;
            // Sumo Pool: the score and the countdown. Sumo Billard: its group and
            // the countdown, then the balls left as in any mode with groups.
            SumoPoolRuleSet sumo = SumoPoolRuleSet.Current != null && SumoPoolRuleSet.Current.ShowsPlate ? SumoPoolRuleSet.Current : null;
            // Hide and seek: the role, the score, the clock and what's left.
            HideAndSeekRuleSet hideAndSeek = rules.Mode == PoolGameMode.HideAndSeek ? HideAndSeekRuleSet.Current : null;
            // Bit n set = ball n still on the table.
            int onTable = 0;
            // 8-ball at three or four (PlayerBallGroups): the player's side's
            // balls, bit n = its ball n (two racks share numbers).
            SpeedPoolRuleSet speed = rules.SpeedPool;
            int side = speed != null && PlayerBallGroups.Players >= 3
                && (speed.IsEightBall || PlayerBallGroups.Split != GroupSplit.SolidsStripes) ? PlayerBallGroups.SideOf(p) : -1;
            bool eliminated = side >= 0 && speed.IsEliminated(p);
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.IsCueBall || ball.Number <= 0 || ball.Number >= 32) continue;
                if (side < 0 || PlayerBallGroups.OwnerOf(ball) == side) onTable |= 1 << ball.Number;
            }

            // Groupe identique: the balls left would tell the groups apart.
            (int kind, int a, int b) key =
                rules.AreGroupsDisguised ? (1, 0, 0)
                : golf != null ? (7, score, golf.RoundStateKey(p))
                : pocketSize != null ? (8, pocketSize.PlateKey(p), 0)
                : shooter != null ? (9, shooter.PlateKey(p), 0)
                : bowling != null ? (10, bowling.PlateKey(p), 0)
                : sumo != null ? (11, sumo.PlateKey(p), 0)
                : hideAndSeek != null ? (12, hideAndSeek.PlateKey(p), 0)
                : hasScore ? (2, score, target)
                : wash != null ? (6, Mathf.RoundToInt(wash.WashedShare(p) * 100f), 0)
                : rules.Mode == PoolGameMode.NineBall ? (3, nextNine, 0)
                : side >= 0 ? (13, side * 2 + (eliminated ? 1 : 0), onTable)
                : group == BallGroup.Solid || group == BallGroup.Stripe ? (4, (int)group, onTable)
                : (5, 0, 0);
            if (key == h.plateKey) return;
            h.plateKey = key;

            h.plateBalls.Clear();
            if (rules.AreGroupsDisguised)
                Text(h.plateBalls, cfg.groupsDisguisedText, bodyFont, 22, Ink);
            else if (golf != null)
            {
                Text(h.plateBalls, $"{score}", titleFont, 34, Ink);
                Text(h.plateBalls, $" coup{(score > 1 ? "s" : "")} (par {target})  ·  {golf.DescribeRound(p)}", bodyFont, 22, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (pocketSize != null)
            {
                Text(h.plateBalls, pocketSize.RoleLabel(p), titleFont, 34, Ink);
                Text(h.plateBalls, $"  ·  {pocketSize.DescribeRound(p)}", bodyFont, 22, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (shooter != null)
            {
                Text(h.plateBalls, shooter.HealthLabel(p), titleFont, 34, Ink);
                Text(h.plateBalls, $"  ·  {shooter.DescribeScore(p)}", bodyFont, 22, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (bowling != null)
            {
                Text(h.plateBalls, $"{bowling.TotalOf(p)}", titleFont, 34, Ink);
                Text(h.plateBalls, $"  ·  {bowling.DescribeProgress(p)}", bodyFont, 22, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (hideAndSeek != null)
            {
                Text(h.plateBalls, hideAndSeek.RoleLabel(p), titleFont, 34, Ink);
                Text(h.plateBalls, $"  ·  {hideAndSeek.DescribeRound(p)}", bodyFont, 22, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (sumo != null)
            {
                Text(h.plateBalls, sumo.DescribeScore(p), titleFont, 34, Ink);
                Text(h.plateBalls, $"  ·  {sumo.DescribeRound()}", bodyFont, 22, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (hasScore)
            {
                Text(h.plateBalls, $"{score}", titleFont, 34, Ink);
                Text(h.plateBalls, $" / {target}", bodyFont, 24, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (wash != null)
            {
                // The balls that wash for this player (they always come back,
                // so none is greyed out), then the score.
                if (group == BallGroup.Solid || group == BallGroup.Stripe)
                {
                    int first = group == BallGroup.Solid ? 1 : 9;
                    for (int n = first; n < first + 7; n++) MiniBall(h.plateBalls, 22, n).style.marginRight = 2;
                    Text(h.plateBalls, "  ", bodyFont, 24, Ink);
                }
                // The score in a badge of the player's colour: on the cream
                // card, next to the balls, plain ink got lost.
                VisualElement badge = Box(h.plateBalls, "wash-score");
                badge.style.backgroundColor = cfg.PlayerColor(p);
                Radius(badge, 12);
                Border(badge, 3, Ink);
                badge.style.paddingLeft = badge.style.paddingRight = 10;
                InkShadow(Text(badge, $"{key.a} %", titleFont, 42, Color.white), 3);
            }
            else if (rules.Mode == PoolGameMode.NineBall)
            {
                Text(h.plateBalls, cfg.nextBallText, bodyFont, 22, Ink);
                if (nextNine > 0) MiniBall(h.plateBalls, 34, nextNine);
            }
            else if (side >= 0) SidePlate(h, speed, side, onTable, eliminated);
            else if (group == BallGroup.Solid || group == BallGroup.Stripe)
            {
                int first = group == BallGroup.Solid ? 1 : 9;
                bool cleared = true;
                for (int n = first; n < first + 7; n++)
                {
                    VisualElement b = MiniBall(h.plateBalls, 28, n);
                    b.style.marginRight = 3;
                    if ((onTable & (1 << n)) == 0)
                    {
                        b.style.opacity = 0.25f;
                        b.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
                    }
                    else cleared = false;
                }
                if (cleared)
                {
                    Text(h.plateBalls, " → ", bodyFont, 22, Ink);
                    MiniBall(h.plateBalls, 34, 8);
                }
            }
            else Text(h.plateBalls, cfg.groupUndecidedText, bodyFont, 22, Ink);
        }

        // 8-ball at three or four (screen A of docs/ui/plaque-billes.html):
        // "TES BILLES · CROIX" over the side's balls ringed with its colour,
        // greyed once potted, then how many are left, or "TA 8 !" once
        // they're all in; the teammate in 2 v 2; "ÉLIMINÉ" after an early 8.
        private void SidePlate(Half h, SpeedPoolRuleSet speed, int side, int onTable, bool eliminated)
        {
            if (eliminated)
            {
                InkShadow(Text(h.plateBalls, cfg.eliminatedText, titleFont, cfg.sidePlateBallSize * 1.1f, Red), 2);
                return;
            }
            float size = cfg.sidePlateBallSize;
            int[] numbers = PlayerBallGroups.Numbers(side);
            bool patterned = speed.TryGetPattern(side, out BallOwnerMarker.Pattern pattern);
            Color ring = cfg.PlayerColor(side);

            VisualElement column = Box(h.plateBalls, "side-balls");
            column.style.flexDirection = FlexDirection.Column;
            string name = patterned && (int)pattern < cfg.ballPatternNames.Length ? cfg.ballPatternNames[(int)pattern]
                : numbers.Length > 0 ? Format(cfg.ballRangeFormat, numbers[0], numbers[numbers.Length - 1]) : "";
            Text(column, Format(cfg.yourBallsFormat, name), bodyFont, size * 0.48f, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            VisualElement row = Box(column, "row");
            row.style.flexDirection = FlexDirection.Row;
            int left = 0;
            foreach (int n in numbers)
            {
                VisualElement b = MiniBall(row, size, n, patterned ? pattern : (BallOwnerMarker.Pattern?)null);
                b.style.marginRight = size * 0.1f;
                Border(b, Mathf.Max(3f, size * 0.1f), ring);
                if ((onTable & (1 << n)) != 0)
                {
                    left++;
                    continue;
                }
                b.style.opacity = 0.25f;
                b.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
            }

            if (left > 0)
            {
                VisualElement count = Box(h.plateBalls, "left");
                count.style.alignItems = Align.Center;
                count.style.marginLeft = 10;
                Text(count, left.ToString(), titleFont, size * 1.1f, Ink);
                Text(count, left > 1 ? cfg.ballsLeftText : cfg.ballLeftText, bodyFont, size * 0.42f, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else
            {
                VisualElement eight = Box(h.plateBalls, "your-eight");
                eight.style.flexDirection = FlexDirection.Row;
                eight.style.alignItems = Align.Center;
                eight.style.marginLeft = 10;
                eight.style.paddingLeft = 4;
                eight.style.paddingRight = 10;
                eight.style.backgroundColor = Yellow;
                Radius(eight, 10);
                Border(eight, 3, Ink);
                MiniBall(eight, size, 8).style.marginRight = 6;
                Text(eight, cfg.yourEightText, titleFont, size * 0.75f, Ink);
            }

            int mate = PlayerBallGroups.TeammateOf(h.player);
            if (mate >= 0)
            {
                VisualElement tag = Box(h.plateBalls, "teammate");
                tag.style.marginLeft = 10;
                tag.style.paddingLeft = tag.style.paddingRight = 8;
                tag.style.backgroundColor = cfg.PlayerColor(mate);
                Radius(tag, 8);
                Border(tag, 3, Ink);
                InkShadow(Text(tag, Format(cfg.teammateFormat, mate + 1), titleFont, size * 0.55f, Color.white), 2);
            }
        }

        // Power Wash Pool's clock, or Play Anywhere's in Speed Pool (the player
        // left after the other holed out), top centre of each half. The last Clock
        // Final Seconds: red, a bit bigger each second, and a bump on each
        // tick. Text and colour only change when the second does.
        private void UpdateWashClock(Half h, PoolMatchRules rules, float dt)
        {
            PowerWashDirt wash = rules.Mode == PoolGameMode.PowerWashPool && !rules.GameOver ? PowerWashDirt.Instance : null;
            PlayAnywhereRuleSet golf = !rules.GameOver ? PlayAnywhereRuleSet.Current : null;
            float golfLeft = golf != null ? golf.FinishClockLeft : -1f;
            if (wash == null && golfLeft < 0f)
            {
                if (h.shownClock == int.MinValue) return;
                h.shownClock = int.MinValue;
                h.clock.style.display = DisplayStyle.None;
                return;
            }

            int seconds = wash == null ? Mathf.CeilToInt(golfLeft) : wash.Overtime ? -1 : Mathf.CeilToInt(wash.TimeLeft);
            bool final = seconds >= 0 && seconds <= cfg.clockFinalSeconds;
            if (seconds != h.shownClock)
            {
                if (h.shownClock == int.MinValue) h.clock.style.display = DisplayStyle.Flex;
                h.shownClock = seconds;
                // TextFx: the overtime text may carry tags, like the HUD's other texts.
                TextFx.Set(h.clock, seconds < 0 ? cfg.overtimeText : $"{seconds / 60}:{seconds % 60:00}");
                h.clock.style.color = final || seconds < 0 ? Red : Color.white;
                if (final) h.clockBump = 1f;
            }

            // From 1 at Clock Final Seconds to Clock Final Scale at 0.
            float grow = final ? Mathf.Lerp(cfg.clockFinalScale, 1f, seconds / (float)Mathf.Max(1, cfg.clockFinalSeconds)) : 1f;
            h.clockBump = Mathf.Max(0f, h.clockBump - dt / Mathf.Max(0.01f, cfg.clockBumpTime));
            float scale = grow * (1f + cfg.clockBump * h.clockBump * h.clockBump);
            h.clock.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        private static int LowestBallOnTable(int max)
        {
            int lowest = 0;
            foreach (PoolBall ball in PoolBall.Active)
                if (!ball.IsCueBall && ball.Number <= max && (lowest == 0 || ball.Number < lowest)) lowest = ball.Number;
            return lowest;
        }
    }
}
