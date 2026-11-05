using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A power of the Shooter modes: dealt by the crates, activated with the
    // power button like any other, and it goes off with the holder's next shot
    // (ShooterMiniRuleSet.FireArmedPower). Which of the modes is set on each
    // asset (Only In Modes). Outside them there's no weapon to fire it with: it
    // does nothing.
    public abstract class ShooterPower : PoolPower
    {
        public sealed override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            ShooterMiniRuleSet rules = ShooterMiniRuleSet.Current;
            if (rules == null)
            {
                Debug.Log($"[ShooterMini] {PowerName} : sans effet hors des modes Shooter.", this);
                return;
            }
            rules.ArmPower(activatingPlayer, this, match.ActivatingPowerLevel >= 2);
        }

        public void Fire(ShooterMiniRuleSet rules, int player, bool level2) => Apply(rules, player, level2);

        // False: pulling the trigger now would waste it, it keeps waiting.
        public virtual bool ReadyToFire(ShooterMiniRuleSet rules, int player, bool level2) => true;

        // level2: picked up twice.
        protected abstract void Apply(ShooterMiniRuleSet rules, int player, bool level2);
    }
}
