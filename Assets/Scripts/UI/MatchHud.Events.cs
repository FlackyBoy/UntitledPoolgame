using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // What the match tells the HUD: pots, fouls, turns, powers, shots.
    public partial class MatchHud
    {
        // ---------- Events ----------

        private void OnPocketed(PoolBall ball, PoolPocket pocket)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.MatchStarted || rules.GameOver) return;
            // Destroyed by Ball Blast: "BOUM !" already said it.
            if (ball == lastBlasted) { lastBlasted = null; return; }
            // Speed Pool: the owner of the cue ball or of the group, not the turn.
            int player = rules.PotOwner(ball);
            if (ball.IsCueBall) { Shout(player, cfg.cueBallPottedShout, Red, false); return; }
            if (!cfg.showPotShouts) return;
            // Cheered only when it's one of the shooter's balls: an opponent's
            // ball gets its own (rueful) shout, the early 8 none.
            switch (rules.MeaningOfPot(ball))
            {
                case PoolMatchRules.PotMeaning.Good:
                    ShoutOneOf(player, cfg.potShouts, Yellow, true);
                    break;
                case PoolMatchRules.PotMeaning.Opponent:
                    ShoutOneOf(player, cfg.opponentBallPotShouts, Red, false);
                    break;
            }
        }

        // One of the texts at random; nothing when the list is empty.
        private void ShoutOneOf(int player, string[] texts, Color color, bool burst)
        {
            if (texts == null || texts.Length == 0) return;
            Shout(player, texts[UnityEngine.Random.Range(0, texts.Length)], color, burst);
        }

        private PoolBall lastBlasted;

        private void OnBallBlasted(PoolBall ball, int shooter)
        {
            lastBlasted = ball;
            Half h = HalfOf(shooter);
            if (h != null) h.shake = cfg.shakeDuration;
            Shout(shooter, cfg.blastShout, cfg.blastColor, true);
        }

        // Fired before the turn passes: CurrentPlayer is the one who fouled.
        private void OnFouled()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null) return;
            Half h = HalfOf(rules.CurrentPlayer);
            if (h != null) h.shake = cfg.shakeDuration;
            Shout(rules.CurrentPlayer, cfg.foulShout, Red, false);
            delayed.Add((Time.unscaledTime + cfg.foulToBallInHandDelay, () =>
            {
                PoolMatchRules r = PoolMatchRules.Instance;
                if (r != null && !r.GameOver) Shout(r.CurrentPlayer, cfg.ballInHandShout, Color.white, false);
            }));
        }

        private void OnTurnChanged(int player)
        {
            Half h = HalfOf(player);
            if (h != null) h.tagBump = 1f;
        }

        // A new power: the sponsor card. The same one again: its level goes
        // up. Lost (stock full, or already at the top level): a word.
        private void OnPowerPickedUp(int player, PoolPower power, PoolMatchRules.PowerPickup outcome, int level)
        {
            if (power == null) return;
            switch (outcome)
            {
                case PoolMatchRules.PowerPickup.Added:
                    if (cfg.showSponsorCard) Sponsor(player, power);
                    break;
                case PoolMatchRules.PowerPickup.LevelUp:
                    Shout(player, Format(cfg.powerLevelUpShoutFormat, level, power.PowerName), Yellow, true);
                    break;
                case PoolMatchRules.PowerPickup.Lost:
                    Shout(player, cfg.powerLostShout, Red, false);
                    break;
            }
        }

        // The launcher hears it went, the other player gets a card saying
        // what's coming and when.
        private void OnPowerActivated(int player, PoolPower power, bool waitsForOpponentTurn)
        {
            if (power == null) return;
            Shout(player, power.TargetsOpponent ? cfg.powerUsedShout : cfg.powerActivatedShout, Yellow, false);
            if (!cfg.showIncomingCard || (!power.TargetsOpponent && !cfg.showOpponentBuffCard)) return;
            string line = !power.TargetsOpponent ? cfg.incomingBuffLine
                : waitsForOpponentTurn ? cfg.incomingNextTurnLine : cfg.incomingNowLine;
            string header = Format(power.TargetsOpponent ? cfg.incomingAttackHeaderFormat : cfg.incomingBuffHeaderFormat, player + 1);
            PoolMatchRules rules = PoolMatchRules.Instance;
            Incoming(rules != null ? rules.TargetOf(player) : 1 - player, power, header, line);
        }

        // The slot shakes, with a word on why (an emptied text = no word).
        private void OnPowerRefused(int player, PowerRefusal reason)
        {
            Half h = HalfOf(player);
            if (h == null) return;
            Wiggle(h.ring, 0.35f);
            string text = reason switch
            {
                PowerRefusal.OwnTurnOnly => cfg.refusedOwnTurnOnlyText,
                PowerRefusal.OpponentTurnOnly => cfg.refusedOpponentTurnOnlyText,
                PowerRefusal.BallsMoving => cfg.refusedBallsMovingText,
                PowerRefusal.PointerBusy => cfg.refusedPointerBusyText,
                PowerRefusal.CannotAct => cfg.refusedCannotActText,
                _ => cfg.refusedNoPowerText,
            };
            if (string.IsNullOrEmpty(text)) return;
            // One at a time: a mashed button doesn't stack the hints.
            h.refusedHint?.RemoveFromHierarchy();
            Label hint = Text(h.fx, text, bodyFont, 18, Ink);
            h.refusedHint = hint;
            hint.style.position = Position.Absolute;
            hint.style.right = Length.Percent(3);
            hint.style.backgroundColor = Color.white;
            hint.style.paddingLeft = hint.style.paddingRight = 10;
            hint.style.paddingTop = hint.style.paddingBottom = 4;
            Radius(hint, 8);
            Border(hint, 3, Red);
            StackUnderSlot(h);
            AddFx(hint, Mathf.Max(0.3f, cfg.refusedDuration), (fx, u) =>
            {
                float k = Mathf.Clamp01(u / 0.12f);
                fx.element.style.scale = new Scale(new Vector3(Mathf.Lerp(0.6f, 1f, BackOut(k)), Mathf.Lerp(0.6f, 1f, BackOut(k)), 1f));
                fx.element.style.opacity = u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f;
            });
        }

        private void OnShotTaken(int inputIndex, float charge)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || charge < cfg.fullPowerThreshold) return;
            int player = rules.GetEffectivePlayerIndex(inputIndex);
            Half h = HalfOf(player);
            if (h != null) h.shake = cfg.shakeDuration;
            Shout(player, cfg.fullPowerShout, Yellow, true);
        }

        // The half showing that player; in solo, the only one.
        private Half HalfOf(int player)
        {
            Half only = null;
            int active = 0;
            foreach (Half h in halves)
            {
                if (h.input == null) continue;
                active++;
                only = h;
                if (h.player == player) return h;
            }
            return active == 1 ? only : null;
        }
    }
}
