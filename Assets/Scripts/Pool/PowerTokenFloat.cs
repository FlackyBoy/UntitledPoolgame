using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // The idle animation of a power token on the table (the generated
    // Token_* prefabs, see PowerTokenGenerator): it spins, tilted on a
    // corner like an item box, and bobs gently. Only its pivot child moves —
    // the root stays where PoolPowerCrate put it, and the pickup (a distance
    // check on the crate) doesn't depend on any of this. Each token starts
    // at a random point of the cycle so several on the table don't move in
    // step.
    public class PowerTokenFloat : MonoBehaviour
    {
        [Tooltip("Ce qui tourne et flotte (vide = le premier enfant).")]
        [SerializeField] private Transform pivot;
        [Tooltip("Vitesse de rotation, en degrés par seconde.")]
        [SerializeField] private float spinSpeed = 70f;
        [Tooltip("Inclinaison (degrés) : le jeton tourne posé sur un coin, comme une boîte à objets.")]
        [SerializeField] private float tilt = 18f;
        [Tooltip("Amplitude du flottement, en mètres.")]
        [SerializeField] private float bobHeight = 0.012f;
        [Tooltip("Durée d'un aller-retour du flottement, en secondes.")]
        [SerializeField] private float bobPeriod = 1.8f;

        private Vector3 basePosition;
        private float phase;

        private void Awake()
        {
            if (pivot == null && transform.childCount > 0) pivot = transform.GetChild(0);
            if (pivot != null) basePosition = pivot.localPosition;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            if (pivot == null) return;
            float t = Time.time + phase;
            pivot.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(0.1f, bobPeriod)) * bobHeight);
            pivot.localRotation = Quaternion.Euler(0f, Mathf.Repeat(t * spinSpeed, 360f), 0f) * Quaternion.Euler(tilt, 0f, tilt);
        }
    }
}
