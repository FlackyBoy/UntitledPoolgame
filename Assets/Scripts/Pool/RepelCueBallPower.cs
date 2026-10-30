using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Pocket Size, tiny player: each of the giant's cue balls is sent
    // rolling to the nearest open pocket (in, it waits out the Speed Pool
    // respawn delay).
    [CreateAssetMenu(fileName = "RepelCueBallPower", menuName = "Pool/Powers/Pocket Size/Repel Cue Ball")]
    public class RepelCueBallPower : PocketSizePower
    {
        [Tooltip("Vitesse donnée à la blanche vers la poche, en m/s.")]
        [SerializeField, Min(0.1f)] private float speed = 2.5f;

        public override PocketSizeRole Role => PocketSizeRole.Tiny;
        public override PowerType Type => PowerType.Defense;

        protected override void Apply(PocketSizeRuleSet rules, int player) => rules.SendCueBallsToPockets(speed);
    }
}
