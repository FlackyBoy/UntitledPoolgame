using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Fusil à pompe": a fan of shots at once, and a recoil strong enough to
    // send the shooter into a pocket.
    [CreateAssetMenu(fileName = "ShooterShotgunPower", menuName = "Pool/Powers/Shooter/Shotgun")]
    public class ShooterShotgunPower : ShooterPower
    {
        [Tooltip("Nombre de tirs de l'éventail.")]
        [Min(2)] [SerializeField] private int pellets = 5;
        [Tooltip("Nombre de tirs au niveau 2 (pouvoir ramassé deux fois). 16 tirs au plus peuvent être en l'air par joueur.")]
        [Min(2)] [SerializeField] private int level2Pellets = 8;
        [Tooltip("Ouverture de l'éventail de chaque côté du viseur, en degrés.")]
        [Range(0f, 45f)] [SerializeField] private float spread = 12f;
        [Tooltip("Vitesse du recul, en m/s. Plus haut = le tireur part plus loin en arrière.")]
        [Min(0f)] [SerializeField] private float recoilSpeed = 1.4f;
        [Tooltip("Durée du recul, en secondes.")]
        [Min(0.01f)] [SerializeField] private float recoilTime = 0.45f;

        public override PowerType Type => PowerType.Attack;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.WeaponOf(player)?.FireSpread(level2 ? level2Pellets : pellets, spread, recoilSpeed, recoilTime);
        }
    }
}
