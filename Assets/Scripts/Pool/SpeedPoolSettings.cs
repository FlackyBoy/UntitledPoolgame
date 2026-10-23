using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Speed Pool (SpeedPoolRuleSet): the two cue balls and what happens when
    // one goes in.
    [CreateAssetMenu(fileName = "SpeedPoolSettings", menuName = "Pool/Speed Pool Settings")]
    public class SpeedPoolSettings : ScriptableObject
    {
        [Header("Cue balls")]
        [Tooltip("Teinte de la blanche de J1 (multipliée par la texture de la blanche).")]
        public Color player1CueColor = new Color(0.2f, 0.45f, 1f);
        [Tooltip("Teinte de la blanche de J2.")]
        public Color player2CueColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Écart de chaque blanche par rapport au point de départ habituel, en fraction de la demi-largeur du tapis (0 = au même endroit, 1 = contre la bande). J1 d'un côté, J2 de l'autre.")]
        [Range(0.1f, 0.9f)] public float cueSpread = 0.5f;

        [Header("Scratch (cue ball potted)")]
        [Tooltip("Temps (secondes, temps réel) avant qu'une blanche rentrée revienne sur la table, à son point de départ ou au plus près s'il est occupé. Le joueur ne peut pas tirer pendant ce temps.")]
        [Min(0f)] public float cueRespawnDelay = 5f;
        [Tooltip("Blanche rentrée : la dernière bille de son groupe que le joueur a rentrée ressort sur la table (à sa place du rack, ou au plus près). Rien ne ressort s'il n'en a encore rentré aucune.")]
        public bool respotOwnBallOnScratch = true;
    }
}
