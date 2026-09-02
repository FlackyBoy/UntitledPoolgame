using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Attack power: inverts the opponent's look controls for the entirety
    // of their next turn — meant to be activated right before their turn to
    // throw off their aim. Same turn-bound lifecycle as VisionImpairPower
    // (starts the moment they enter aim mode, ends when they leave it or
    // their turn ends) — see PoolMatchRules.QueueInvertedControls. Look
    // only, not movement: inverting WASD as well would risk the affected
    // player wandering off the table/into hazards rather than just fumbling
    // their aim, which felt like a step too far for a first pass.
    //
    // On top of the inversion itself (retour utilisateur: "trop facile"):
    // look sensitivity is also boosted while it's active, making small
    // corrections much harder to land on top of having to think backwards,
    // and the trajectory preview is hidden entirely — no more aiming by
    // watching where the line goes and correcting for the flip.
    [CreateAssetMenu(fileName = "InvertedControlsPower", menuName = "Pool/Powers/Inverted Controls (Attack)")]
    public class InvertedControlsPower : PoolPower
    {
        [SerializeField] private float sensitivityMultiplier = 1.8f;

        public override PowerType Type => PowerType.Attack;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            int opponent = 1 - activatingPlayer;
            match.QueueInvertedControls(opponent, sensitivityMultiplier);
        }
    }
}
