using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Bowling Pool: frames, the pins' size and physics, texts
    // (BowlingPoolRuleSet). Sizes are relative to the table's ball, so they
    // follow the table's scale. Loaded from Resources.
    [CreateAssetMenu(fileName = "BowlingPoolSettings", menuName = "Pool/Bowling Pool Settings")]
    public class BowlingPoolSettings : ScriptableObject
    {
        [Header("Partie")]
        [Tooltip("Frames par joueur. Deux lancers par frame, et jusqu'à trois dans la dernière après un strike ou un spare.")]
        [Min(1)] public int frames = 5;
        [Tooltip("Temps pendant lequel on regarde le résultat avant que les quilles tombées disparaissent et que la blanche revienne, en secondes.")]
        [Min(0f)] public float sweepDelay = 1.5f;

        [Header("Triangle de quilles")]
        [Tooltip("Place de la quille de tête, de 0 (centre de la table) à 1 (bande du fond). Les autres rangées sont derrière elle : trop haut, elles passent dans la bande.")]
        [Range(0f, 0.9f)] public float rackPosition = 0.5f;
        [Tooltip("Hauteur d'une quille, en diamètres de bille (au bowling : 1,76).")]
        [Min(0.2f)] public float pinHeight = 1.76f;
        [Tooltip("Largeur d'une quille à son ventre, en diamètres de bille (au bowling : 0,56).")]
        [Min(0.1f)] public float pinWidth = 0.56f;
        [Tooltip("Distance entre deux quilles voisines, en diamètres de bille (au bowling : 1,41). Plus grand : plus de strikes difficiles.")]
        [Min(0.3f)] public float pinSpacing = 1.41f;
        public Color pinColor = new Color(0.96f, 0.94f, 0.88f);
        public Color collarColor = new Color(0.8f, 0.12f, 0.12f);

        [Header("Physique des quilles")]
        [Tooltip("Masse d'une quille, en part de celle de la blanche (au bowling : 0,22). Plus bas : elles volent plus loin et freinent moins la blanche.")]
        [Min(0.01f)] public float pinMass = 0.35f;
        [Tooltip("Hauteur du centre de masse, en part de la hauteur de la quille. Plus bas : elle revient plus facilement debout.")]
        [Range(0.1f, 0.6f)] public float pinCenterOfMass = 0.35f;
        [Tooltip("Une quille penchée de plus que cet angle compte comme tombée, en degrés.")]
        [Range(5f, 80f)] public float tiltToFall = 30f;
        [Tooltip("Frottement des quilles (tapis, bandes, autres quilles).")]
        [Range(0f, 1f)] public float pinFriction = 0.4f;
        [Tooltip("Rebond des quilles.")]
        [Range(0f, 1f)] public float pinBounciness = 0.25f;
        [Tooltip("Freinage de leur déplacement.")]
        [Min(0f)] public float pinLinearDamping = 0.05f;
        [Tooltip("Freinage de leur rotation : plus haut, une quille couchée roule moins longtemps.")]
        [Min(0f)] public float pinAngularDamping = 0.4f;

        [Header("Fin du lancer")]
        [Tooltip("Une quille est arrêtée sous cette vitesse, en m/s.")]
        [Min(0f)] public float settleSpeed = 0.02f;
        [Tooltip("Et sous cette vitesse de rotation, en radians/s. Plus bas : on attend qu'elle ait fini de vaciller.")]
        [Min(0f)] public float settleSpin = 0.3f;
        [Tooltip("Le lancer est jugé au plus tard au bout de ce temps, en secondes, même si une quille bouge encore.")]
        [Min(1f)] public float settleTimeout = 6f;

        [Header("Textes (plaque du HUD)")]
        [Tooltip("{0} = frame, {1} = nombre de frames.")]
        public string frameFormat = "Frame {0}/{1}";
        public string finishedText = "Terminé";
        public string rollOffText = "Mort subite";
        [Tooltip("{0} = score de l'autre joueur.")]
        public string opponentFormat = "l'autre : {0}";

        [Header("Textes (cris)")]
        public string strikeShout = "STRIKE !";
        public string spareShout = "SPARE !";
        public string gutterShout = "Gouttière !";
        [Tooltip("{0} = quilles tombées.")]
        public string pinsShoutFormat = "{0} !";
        public string tieShout = "Égalité : mort subite !";
        public Color gutterColor = new Color(0.9f, 0.25f, 0.2f);

        [Header("Diagnostic")]
        [Tooltip("Logs [Bowling] : quilles tombées à chaque lancer, score, fin de partie.")]
        public bool debugLogs = true;
    }
}
