using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Aimant": the activating player picks a pocket; on their next shot,
    // the first of their own balls (PoolMatchRules.IsOwnBall) to roll
    // toward that pocket within Capture Radius gets caught and pulled in —
    // the nearest one if several qualify the same step. Armed like Ball
    // Blast (PoolMatchRules.ArmMagnet) and consumed by that next shot,
    // whatever happens. Only ever one ball per shot: catching a ball still
    // takes a decent shot, the magnet just finishes it.
    [CreateAssetMenu(fileName = "MagnetPower", menuName = "Pool/Powers/Magnet")]
    public class MagnetPower : PocketTargetPower
    {
        [Tooltip("Distance (mètres, à plat) du trou en dessous de laquelle une bille du joueur qui roule vers lui est captée. Une poche fait ~6 cm de rayon, une bille ~3 cm.")]
        [SerializeField] private float captureRadius = 0.4f;
        [Tooltip("Vitesse minimale (m/s) pour qu'une bille soit captée : une bille immobile près du trou ne l'est pas, il faut l'y envoyer.")]
        [SerializeField] private float minCaptureSpeed = 0.05f;
        [Tooltip("Accélération (m/s²) qui tire la bille captée vers le trou. 1 = discret, 3 = franc.")]
        [SerializeField] private float pullAcceleration = 1.5f;
        [Tooltip("Correction de trajectoire (par seconde) : à quelle vitesse la bille captée perd ce qui l'écarte du trou. 0 = aucune, 5 = presque rail.")]
        [SerializeField] private float steering = 4f;
        [Tooltip("Durée maximale (secondes) d'attraction : au-delà la bille est lâchée, pour qu'une bille coincée contre une bande ne bloque pas le coup.")]
        [SerializeField] private float maxPullDuration = 4f;

        [Header("VFX dans la poche")]
        [Tooltip("Effet en boucle joué dans la poche choisie tant que l'aimant est actif : de la validation du trou jusqu'à la fin du tir (ou l'empochage de la bille captée). Vide = aucun. Par défaut : Cartoon FX Remaster > CFXR Electrified 3.")]
        [SerializeField] private GameObject pocketVfxPrefab;
        [Tooltip("Échelle de l'effet par rapport à son prefab : les effets sont faits pour des objets de quelques mètres, une poche fait ~12 cm.")]
        [SerializeField] private float pocketVfxScale = 0.2f;
        [Tooltip("Hauteur (mètres) de l'effet au-dessus du centre de la poche.")]
        [SerializeField] private float pocketVfxHeight = 0.02f;
        [Tooltip("Durée (secondes) laissée aux particules déjà lancées pour finir, une fois l'aimant terminé, avant de supprimer l'effet.")]
        [SerializeField] private float pocketVfxFadeOut = 1.5f;

        public override PowerType Type => PowerType.Effect;
        public float MaxPullDuration => maxPullDuration;

        public override void Activate(PoolMatchRules match, int activatingPlayer, PoolPocket target)
        {
            match.ArmMagnet(activatingPlayer, this, target);
        }

        // The looping effect in the picked pocket while the magnet is active
        // (PoolMatchRules keeps the instance and stops it). Null without a
        // prefab.
        public GameObject SpawnPocketVfx(PoolPocket pocket)
        {
            if (pocketVfxPrefab == null || pocket == null) return null;
            Vector3 position = pocket.transform.position + Vector3.up * pocketVfxHeight;
            GameObject vfx = Instantiate(pocketVfxPrefab, position, pocketVfxPrefab.transform.rotation);
            vfx.transform.localScale = pocketVfxPrefab.transform.localScale * pocketVfxScale;
            return vfx;
        }

        // Stops emitting and lets the particles already in flight finish,
        // rather than popping the effect out mid-flight.
        public void StopPocketVfx(GameObject vfx)
        {
            if (vfx == null) return;
            foreach (ParticleSystem system in vfx.GetComponentsInChildren<ParticleSystem>())
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(vfx, pocketVfxFadeOut);
        }

        // The ball to catch this physics step, or null: the nearest of the
        // shooter's own balls rolling toward the pocket within captureRadius.
        public PoolBall FindCapture(PoolMatchRules match, int player, PoolPocket pocket)
        {
            Vector3 pocketPosition = pocket.transform.position;
            float minSpeedSqr = minCaptureSpeed * minCaptureSpeed;
            PoolBall best = null;
            float bestDistanceSqr = captureRadius * captureRadius;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.Rigidbody.isKinematic || !match.IsOwnBall(player, ball)) continue;

                Vector3 toPocket = pocketPosition - ball.transform.position;
                toPocket.y = 0f;
                float distanceSqr = toPocket.sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr) continue;

                Vector3 velocity = ball.Rigidbody.linearVelocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude < minSpeedSqr || Vector3.Dot(velocity, toPocket) <= 0f) continue;

                best = ball;
                bestDistanceSqr = distanceSqr;
            }
            return best;
        }

        // One physics step of pull on the caught ball: accelerate it toward
        // the pocket and bleed off the sideways part of its velocity so it
        // curves in instead of orbiting around the pocket.
        public void Pull(PoolBall ball, PoolPocket pocket)
        {
            Vector3 toPocket = pocket.transform.position - ball.transform.position;
            toPocket.y = 0f;
            float distance = toPocket.magnitude;
            if (distance < 1e-4f) return;
            Vector3 direction = toPocket / distance;

            Rigidbody rb = ball.Rigidbody;
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;
            Vector3 sideways = velocity - direction * Vector3.Dot(velocity, direction);
            float bleed = Mathf.Clamp01(steering * Time.fixedDeltaTime);

            rb.AddForce(direction * pullAcceleration, ForceMode.Acceleration);
            rb.AddForce(-sideways * bleed, ForceMode.VelocityChange);
        }
    }
}
