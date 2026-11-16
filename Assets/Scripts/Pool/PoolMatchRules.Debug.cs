using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Test-only entry points: the cheats and the DebugMenu.
    public partial class PoolMatchRules
    {
        // Test (C+W cheat): the current player's group counts as cleared,
        // next legal shot is the 8.
        public void DebugForceEightBallEndgame()
        {
            if (!MatchStarted || GameOver)
            {
                Debug.LogWarning("[EightBallEndgameCheat] No match in progress.");
                return;
            }

            if (!(ruleSet is EightBallRuleSet eightBall))
            {
                Debug.LogWarning("[EightBallEndgameCheat] Current mode isn't 8-ball.");
                return;
            }

            // Copy: deactivating a ball removes it from Active.
            foreach (PoolBall ball in new List<PoolBall>(PoolBall.Active))
            {
                if (ball.Group == BallGroup.Solid || ball.Group == BallGroup.Stripe)
                    ball.gameObject.SetActive(false);
            }

            eightBall.DebugAssignGroup(CurrentPlayer, BallGroup.Solid);
            Debug.Log($"[EightBallEndgameCheat] Joueur {CurrentPlayer + 1} peut maintenant tirer la bille 8.");
        }

        // ---------- Test-only entry points (DebugMenu) ----------
        // Same paths as real play, so they behave like the real thing.

        // A match is on and the table is still: the only time a fake shot
        // or a turn change makes sense.
        public bool DebugCanAct => MatchStarted && !GameOver && !shotInProgress && !PoolBall.AnyMoving();

        // Sets a power at that level: its own slot if already in stock, else a
        // free slot, else replaces the selected one. Null empties both.
        public void DebugSetHeldPower(int player, PoolPower power, int level = 1)
        {
            if (power == null)
            {
                for (int s = 0; s < PowerSlots; s++) { heldPowers[player, s] = null; heldLevels[player, s] = 1; }
                SelectSlot(player, 0);
                return;
            }
            int slot = -1;
            for (int s = 0; s < PowerSlots && slot < 0; s++) if (heldPowers[player, s] == power) slot = s;
            for (int s = 0; s < PowerSlots && slot < 0; s++) if (heldPowers[player, s] == null) slot = s;
            if (slot < 0) slot = selectedSlot[player];
            bool added = heldPowers[player, slot] != power;
            heldPowers[player, slot] = power;
            heldLevels[player, slot] = Mathf.Clamp(level, 1, MaxPowerLevel);
            SelectSlot(player, slot);
            PowerGranted?.Invoke(player);
            PowerPickedUp?.Invoke(player, power, added ? PowerPickup.Added : PowerPickup.LevelUp, heldLevels[player, slot]);
        }

        public void DebugSwitchTurn()
        {
            if (DebugCanAct) SwitchTurn();
        }

        // A foul committed by player: the turn passes to the other one, with
        // ball in hand — as after a scratch.
        public void DebugFoul(int player)
        {
            if (!DebugCanAct) return;
            if (CurrentPlayer != player) SwitchTurn();
            RegisterFoul();
            SwitchTurn();
        }

        // A shot judged by the real rule set: pocketed (may be null) leaves the
        // table, firstContact is what the cue ball touched (null = nothing).
        public void DebugResolveShot(PoolBall pocketed, PoolBall firstContact)
        {
            if (!DebugCanAct) return;
            pocketedThisShot.Clear();
            pocketByBallThisShot.Clear();
            cueBallPocketedThisShot = false;
            // Through OnPocketed: HandleBallPocketed records it (and plays the
            // pot feedback) exactly as for a real pot.
            if (pocketed != null) pocketed.OnPocketed();
            ruleSet.ResolveShot(this, pocketedThisShot, cueBallPocketedThisShot, firstContact);
        }
    }
}
