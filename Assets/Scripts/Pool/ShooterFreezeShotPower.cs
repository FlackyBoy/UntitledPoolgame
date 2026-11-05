using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Tir de glace": one shot. A player it hits can't do anything for a while;
    // a ball it hits turns into a fixed wall.
    [CreateAssetMenu(fileName = "ShooterFreezeShotPower", menuName = "Pool/Powers/Shooter/Freeze Shot")]
    public class ShooterFreezeShotPower : ShooterPower
    {
        [Tooltip("Durée du gel d'un joueur touché, en secondes : il ne peut plus rien faire.")]
        [Min(0.1f)] [SerializeField] private float playerTime = 2f;
        [Tooltip("Durée du gel d'un joueur au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.1f)] [SerializeField] private float level2PlayerTime = 3.5f;
        [Tooltip("Durée du gel d'une bille touchée, en secondes : elle ne bouge plus.")]
        [Min(0.1f)] [SerializeField] private float ballTime = 8f;

        public override PowerType Type => PowerType.Attack;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.WeaponOf(player)?.FireFreezeShot(level2 ? level2PlayerTime : playerTime, ballTime);
        }
    }
}
