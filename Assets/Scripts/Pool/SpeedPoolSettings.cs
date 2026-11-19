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
        [Tooltip("Teinte de la blanche de J3 (parties à 3 ou 4).")]
        public Color player3CueColor = new Color(0.2f, 0.8f, 0.35f);
        [Tooltip("Teinte de la blanche de J4.")]
        public Color player4CueColor = new Color(0.65f, 0.35f, 0.95f);
        [Tooltip("Écart de chaque blanche par rapport au point de départ habituel, en fraction de la demi-largeur du tapis (0 = au même endroit, 1 = contre la bande). J1 d'un côté, J2 de l'autre.")]
        [Range(0.1f, 0.9f)] public float cueSpread = 0.5f;

        [Header("Balls")]
        [Tooltip("Billes en plus pour chaque joueur, en plus de ses 7 : des copies des billes de son groupe, ajoutées en rangées derrière le triangle (une bille de plus par rangée), ou à la place libre la plus proche quand on arrive à la bande. 0 = les billes habituelles.")]
        [Range(0, 14)] public int extraBallsPerPlayer = 0;
        [Tooltip("À 4 joueurs chacun pour soi, quand les motifs des propriétaires sont coupés (Owner Markers From à 5) : teinte multipliée sur les billes du second triangle (pleines de J3, rayées de J4), pour les distinguer de celles de J1 et J2.")]
        public Color secondRackTint = new Color(0.55f, 0.55f, 0.55f);

        [Header("Owner marks")]
        [Tooltip("À partir de combien de joueurs les billes de chaque propriétaire prennent son motif (J1, J2, J3, J4 ; en 2 contre 2, ceux de J1 et J2) au lieu de pleines et rayées. 2 = toujours.")]
        [Range(2, 5)] public int ownerMarkersFrom = 3;
        [Tooltip("Motif des billes de J1, J2, J3, J4, à la couleur de la bille autour de son numéro : pleine, bande (rayée), croix, carré ou triangle.")]
        public BallOwnerMarker.Pattern[] ownerPatterns =
            { BallOwnerMarker.Pattern.Solid, BallOwnerMarker.Pattern.Stripe, BallOwnerMarker.Pattern.Cross, BallOwnerMarker.Pattern.Square };

        [Tooltip("À partir de combien de joueurs un anneau à la couleur du joueur entoure ses billes sur le tapis, visible seulement dans sa vue (et celle de son équipier). 2 = toujours, 5 = jamais.")]
        [Range(2, 5)] public int ownBallHaloFrom = 3;
        [Tooltip("Diamètre de l'anneau, en diamètres de bille.")]
        [Range(1f, 3f)] public float ownBallHaloSize = 1.6f;

        [Tooltip("En 2 contre 2 (tous les modes à équipes sauf les Sumo, où la bille du joueur porte déjà la couleur de l'équipe) : une pointe à la couleur de l'équipe au-dessus de la tête de chaque joueur, vue par les autres, cachée derrière ce qui cache le joueur.")]
        public bool teamChevrons = true;
        [Tooltip("Taille de la pointe, en part de la taille du joueur.")]
        [Range(0.05f, 0.6f)] public float teamChevronSize = 0.2f;
        [Tooltip("Distance (en tailles de joueur) à laquelle la pointe a la taille Team Chevron Size. Plus près elle rapetisse, plus loin elle grossit, pour garder à peu près la même taille à l'écran.")]
        [Range(0.5f, 10f)] public float teamChevronSizeAt = 3f;
        [Tooltip("Taille minimale de la pointe de près, en part de Team Chevron Size.")]
        [Range(0.1f, 1f)] public float teamChevronMinScale = 0.35f;
        [Tooltip("Taille maximale de la pointe de loin, en part de Team Chevron Size.")]
        [Range(1f, 10f)] public float teamChevronMaxScale = 4f;
        [Tooltip("Espace entre le haut de la tête et la pointe, en part de la taille du joueur.")]
        [Range(0f, 0.6f)] public float teamChevronHeight = 0.04f;
        [Tooltip("En 2 contre 2 : un anneau à la couleur de l'équipe au sol autour de chaque joueur, vu par tous (coupé depuis le contour).")]
        public bool teamRings = false;
        [Tooltip("Diamètre de cet anneau, en largeurs du corps du joueur.")]
        [Range(1f, 5f)] public float teamRingSize = 3f;

        public BallOwnerMarker.Pattern PatternOf(int side) =>
            ownerPatterns != null && side >= 0 && side < ownerPatterns.Length ? ownerPatterns[side] : BallOwnerMarker.Pattern.Solid;

        public Color PlayerCueColor(int player) => player switch
        {
            0 => player1CueColor,
            1 => player2CueColor,
            2 => player3CueColor,
            _ => player4CueColor,
        };

        [Header("Scratch (cue ball potted)")]
        [Tooltip("Temps (secondes, temps réel) avant qu'une blanche rentrée revienne sur la table, à son point de départ ou au plus près s'il est occupé. Le joueur ne peut pas tirer pendant ce temps.")]
        [Min(0f)] public float cueRespawnDelay = 5f;
        [Tooltip("Blanche rentrée : la dernière bille de son groupe que le joueur a rentrée ressort sur la table (à sa place du rack, ou au plus près). Rien ne ressort s'il n'en a encore rentré aucune.")]
        public bool respotOwnBallOnScratch = true;
    }
}
