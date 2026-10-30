using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere, crate object: the spring, facing its Z axis. A ball
    // that touches its pad is sent straight out in front of it, like a
    // pinball bumper, faster than a bounce: Spring Launch Speed, or its own
    // speed times Spring Speed Gain when that's more. Turned at placing to
    // aim it. The pad pops out when it fires.
    [AddComponentMenu("Pool/Play Anywhere/Ressort")]
    public class PlayAnywhereSpring : PlayAnywhereGadget
    {
        [Tooltip("Zone juste devant la plaque (espace de l'objet) : une bille qui y entre est renvoyée.")]
        [SerializeField] private Vector3 padZoneCenter = new Vector3(0f, 0.1f, 0.22f);
        [SerializeField] private Vector3 padZoneSize = new Vector3(0.4f, 0.2f, 0.16f);
        [Tooltip("La plaque, qui sort d'un coup quand le ressort se déclenche (facultatif).")]
        [SerializeField] private Transform pad;
        [Tooltip("Course (mètres) et durée (secondes) de la plaque qui sort.")]
        [SerializeField] private float padPop = 0.05f;
        [SerializeField] private float padPopTime = 0.15f;

        private Vector3 padRest;
        private float poppedAt = -1f;

        private void Awake()
        {
            if (pad != null) padRest = pad.localPosition;
        }

        protected override void Act(PlayAnywhereRuleSet rules, int player, PoolBall ball, Vector3 from, Vector3 to)
        {
            if (!Crosses(from, to, padZoneCenter, padZoneSize)) return;
            PlayAnywhereSettings s = rules.Settings;
            float speed = Mathf.Max(s.springLaunchSpeed, ball.HorizontalSpeed * s.springSpeedGain);
            Launch(ball, FlatDirection(Vector3.forward) * speed);
            Cooldown(player, 0.3f);
            poppedAt = Time.time;
            Log(rules, $"J{player + 1} touche le ressort ({name}) : renvoyée à {speed:0.00} m/s.", this);
        }

        private void Update()
        {
            if (pad == null || poppedAt < 0f) return;
            float k = (Time.time - poppedAt) / Mathf.Max(0.01f, padPopTime);
            if (k >= 1f)
            {
                pad.localPosition = padRest;
                poppedAt = -1f;
                return;
            }
            pad.localPosition = padRest + Vector3.forward * (padPop * Mathf.Sin(k * Mathf.PI));
        }

        protected override void DrawZones() => DrawZone(padZoneCenter, padZoneSize, new Color(1f, 0.3f, 0.3f));
    }
}
