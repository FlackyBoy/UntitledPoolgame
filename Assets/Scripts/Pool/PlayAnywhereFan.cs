using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere, crate object: the fan, blowing along its Z axis. A ball
    // in the zone in front of it is pushed that way (Fan Acceleration right
    // in front, down to nothing at the far end of the zone), still or not.
    // A ball pushed less than its ground's friction stops: the shot ends.
    [AddComponentMenu("Pool/Play Anywhere/Ventilateur")]
    public class PlayAnywhereFan : PlayAnywhereGadget
    {
        [Tooltip("Zone soufflée devant le ventilateur (espace de l'objet). La poussée est la plus forte du côté du ventilateur (Z le plus petit).")]
        [SerializeField] private Vector3 blowCenter = new Vector3(0f, 0.25f, 1.075f);
        [SerializeField] private Vector3 blowSize = new Vector3(0.6f, 0.5f, 2f);

        protected override void Act(PlayAnywhereRuleSet rules, int player, PoolBall ball, Vector3 from, Vector3 to)
        {
            if (!Inside(to, blowCenter, blowSize)) return;
            float near = blowCenter.z - blowSize.z * 0.5f;
            float t = Mathf.InverseLerp(near, near + blowSize.z, to.z);
            float push = rules.Settings.fanAcceleration * (1f - t);
            ball.Rigidbody.linearVelocity += FlatDirection(Vector3.forward) * (push * Time.fixedDeltaTime);
        }

        protected override void DrawZones() => DrawZone(blowCenter, blowSize, new Color(0.6f, 0.9f, 1f));
    }
}
