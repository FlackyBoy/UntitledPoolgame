using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Marker for a numbered ball that also grants a power when legally
    // pocketed — same pattern as Cue.cs (a plain marker component; base rules
    // never need to know about it). PoolMatchRules listens for this via the
    // existing PoolBall.Pocketed hook, independently of group/order rules —
    // pocketing a power ball still counts normally for 8-ball/9-ball/14.1.
    public class PowerBall : MonoBehaviour
    {
        [SerializeField] private PoolPower power;
        public PoolPower Power => power;
    }
}
