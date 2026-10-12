using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // R&D stamina of the one who strikes (CombatSettings > Endurance),
    // boxing punches only: every punch and kick costs some, a Spartan kick
    // a bit more as it leaves; without enough, the blow doesn't start
    // (CanStart). Below Tired Below, punches go out slower and land weaker.
    // It comes back Stamina Regen Delay after the last blow. Keeps players
    // from spamming: a burst of about six punches, then a breather.
    public partial class LocalUnarmedMelee
    {
        private float stamina = 1f;
        private float lastStaminaUse = -10f;
        private float nextTiredLog;
        private bool tiredPunch;         // the current punch was thrown tired

        public float Stamina => stamina;
        private bool Tired => tiredPunch;
        private bool StaminaOn => Boxing && CombatSettings.Instance.staminaEnabled;

        private static float StaminaCost(Move m, CombatSettings cfg) => m == Move.Punch ? cfg.punchStamina : cfg.kickStamina;

        // Enough for this blow? (Logged at most twice a second when not.)
        private bool HasStamina(Move m)
        {
            if (!StaminaOn) return true;
            float cost = StaminaCost(m, CombatSettings.Instance);
            if (stamina >= cost - 1e-4f) return true;
            if (debugLogs && Time.time >= nextTiredLog)
            {
                nextTiredLog = Time.time + 0.5f;
                Debug.Log($"[UnarmedMelee] {name}: too tired for a {(m == Move.Punch ? "punch" : "kick")} (stamina {stamina:P0}, needs {cost:P0})", this);
            }
            return false;
        }

        // A blow starts: pay for it; a punch thrown with little left is tired.
        private void SpendStamina(Move m)
        {
            tiredPunch = false;
            if (!StaminaOn) return;
            CombatSettings cfg = CombatSettings.Instance;
            stamina = Mathf.Max(0f, stamina - StaminaCost(m, cfg));
            lastStaminaUse = Time.time;
            tiredPunch = m == Move.Punch && stamina < cfg.tiredBelow;
            if (tiredPunch && debugLogs) Debug.Log($"[UnarmedMelee] {name}: tired punch (stamina {stamina:P0})", this);
        }

        // A charged kick released as a Spartan kick costs extra.
        private void SpendSpartanStamina()
        {
            if (!StaminaOn) return;
            stamina = Mathf.Max(0f, stamina - CombatSettings.Instance.spartanStamina);
            lastStaminaUse = Time.time;
        }

        private void UpdateStamina(float dt)
        {
            CombatSettings cfg = CombatSettings.Instance;
            if (!StaminaOn) { stamina = 1f; return; }
            if (Time.time - lastStaminaUse >= cfg.staminaRegenDelay)
                stamina = Mathf.Min(1f, stamina + cfg.staminaRegenRate * dt);
        }

        // Provisional: the stamina bar under the crosshair, while not full.
        private void DrawStamina()
        {
            if (!StaminaOn || stamina >= 1f) return;
            DrawBar(0.25f, 120f, 5f, stamina,
                stamina < CombatSettings.Instance.tiredBelow ? new Color(1f, .55f, .15f) : new Color(.45f, .9f, .4f));
        }
    }
}
