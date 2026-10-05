using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Bille destructrice": the activating player's next shot destroys the
    // first ball the cue ball touches — an explosion, and the ball leaves the
    // table as if pocketed by the shooter (PoolMatchRules.ArmBallBlast /
    // HandleFirstContact). The rule sets then judge the shot as usual: one of
    // their own balls keeps the turn; a wrong ball is still destroyed, but
    // the first contact was illegal so it's a foul (ball in hand for the
    // other player) and the ball is gone in the other player's favour.
    // Consumed by that next shot, whatever it touches (or misses).
    [CreateAssetMenu(fileName = "BallBlastPower", menuName = "Pool/Powers/Ball Blast")]
    public class BallBlastPower : PoolPower
    {
        [Tooltip("Effet joué à l'endroit de la bille détruite (Cartoon FX Remaster > CFXR2 WW Explosion, choisi par l'utilisateur).")]
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Échelle de l'effet par rapport à son prefab : les effets sont faits pour des objets de quelques mètres, une bille fait 6 cm.")]
        [SerializeField] private float explosionScale = 0.15f;
        [Tooltip("Durée (secondes) avant de supprimer l'effet, s'il ne se supprime pas tout seul.")]
        [SerializeField] private float explosionLifetime = 4f;
        [Tooltip("La bille 8 n'explose pas : la détruire ferait perdre la partie sur-le-champ (8 rentrée sans être annoncée). Le pouvoir est alors perdu, le coup se joue normalement.")]
        [SerializeField] private bool spareEightBall = true;
        // A velocity change rather than an impulse: the same push whatever the
        // balls' mass. (Was Blast Impulse, 0 by default; renamed so the light
        // blast asked for applies to an asset already created.)
        [Tooltip("Souffle : vitesse (m/s) donnée aux billes collées à l'explosion, diminuant jusqu'à 0 au bord du rayon. 0 = aucun. 0,5 = léger, 1,5 = fort.")]
        [SerializeField] private float blastSpeed = 0.5f;
        [Tooltip("Rayon (mètres) du souffle.")]
        [SerializeField] private float blastRadius = 0.3f;

        public override PowerType Type => PowerType.Effect;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            match.ArmBallBlast(activatingPlayer, this);
        }

        public bool CanBlast(PoolBall ball) =>
            ball != null && !ball.IsCueBall && !(spareEightBall && ball.Group == BallGroup.Eight);

        // The explosion and its blast; the ball itself is removed by
        // PoolMatchRules right after (OnPocketed).
        public void PlayEffects(PoolBall target)
        {
            Vector3 position = target.transform.position;
            if (explosionPrefab != null)
            {
                GameObject vfx = Instantiate(explosionPrefab, position, explosionPrefab.transform.rotation);
                vfx.transform.localScale = explosionPrefab.transform.localScale * explosionScale;
                if (explosionLifetime > 0f) Destroy(vfx, explosionLifetime);
            }

            if (blastSpeed <= 0f || blastRadius <= 0f) return;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball == target || ball.Rigidbody == null || ball.Rigidbody.isKinematic) continue;
                Vector3 away = ball.transform.position - position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance > blastRadius || distance < 1e-4f) continue;
                float falloff = 1f - distance / blastRadius;
                ball.Rigidbody.AddForce(away / distance * (blastSpeed * falloff), ForceMode.VelocityChange);
            }
        }
    }
}
