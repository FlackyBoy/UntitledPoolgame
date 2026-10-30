using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere, crate object: the shortcut pipe. Two parts, placed one
    // after the other (IPlayAnywhereTwoPart): a hole in the floor (Entry),
    // then the pipe end it comes out of (Exit), within Pipe Max Length. A
    // ball that rolls over the hole drops in and comes out of the exit's
    // mouth, along the exit's Z axis, at its speed (Pipe Min Exit Speed at
    // least). No penalty: it's a shortcut. Dropped straight in the scene,
    // both parts work where they stand.
    [AddComponentMenu("Pool/Play Anywhere/Tuyau de raccourci")]
    public class PlayAnywherePipe : PlayAnywhereGadget, IPlayAnywhereTwoPart
    {
        [Tooltip("Le trou d'entrée, posé en premier.")]
        [SerializeField] private Transform entry;
        [Tooltip("Le bout de tuyau d'où la bille ressort, posé ensuite ; elle sort selon son axe Z.")]
        [SerializeField] private Transform exit;
        [Tooltip("Rayon (mètres) du trou : une bille dont le centre passe à moins de cette distance tombe dedans.")]
        [SerializeField] private float entryRadius = 0.13f;
        [Tooltip("Hauteur (mètres) au-dessus du trou jusqu'où la bille est prise (une bille qui saute par-dessus ne tombe pas).")]
        [SerializeField] private float entryHeight = 0.1f;
        [Tooltip("La bille ressort à cette distance (mètres) devant la sortie.")]
        [SerializeField] private float exitDistance = 0.12f;

        public Transform FirstPart => entry;
        public Transform SecondPart => exit;
        public float MaxPartDistance => PlayAnywhereRuleSet.Current != null ? PlayAnywhereRuleSet.Current.Settings.pipeMaxLength : 6f;

        protected override void Act(PlayAnywhereRuleSet rules, int player, PoolBall ball, Vector3 from, Vector3 to)
        {
            if (entry == null || exit == null) return;
            // The ball's path this step, in the world, against the hole.
            Vector3 a = transform.TransformPoint(from), b = transform.TransformPoint(to);
            Vector3 hole = entry.position;
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            float length2 = ab.sqrMagnitude;
            float t = length2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(new Vector2(hole.x - a.x, hole.z - a.z), ab) / length2) : 0f;
            Vector3 closest = Vector3.Lerp(a, b, t);
            float height = closest.y - ball.Radius - hole.y;
            if (new Vector2(closest.x - hole.x, closest.z - hole.z).sqrMagnitude > entryRadius * entryRadius
                || height > entryHeight || height < -0.05f)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(exit.forward, Vector3.up);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 spot = exit.position + forward * exitDistance + Vector3.up * ball.Radius;
            float speed = Mathf.Max(ball.HorizontalSpeed, rules.Settings.pipeMinExitSpeed);
            ball.PlaceAt(spot);
            Launch(ball, forward * speed);
            MarkMoved(player, ball);
            Cooldown(player, 0.3f);
            Log(rules, $"J{player + 1} tombe dans le tuyau ({name}) : ressort à {spot}, {speed:0.00} m/s.", this);
        }

        protected override void DrawZones()
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(0.2f, 1f, 0.3f);
            if (entry != null) Gizmos.DrawWireSphere(entry.position, entryRadius);
            if (exit != null) Gizmos.DrawLine(exit.position + Vector3.up * 0.05f, exit.position + Vector3.up * 0.05f + exit.forward * (exitDistance + 0.3f));
        }
    }
}
