using System;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // How long a table hole stays open (TableHolePower.Duration).
    public enum TableHoleDuration
    {
        OpponentTurn, // until the activating player's opponent's turn ends
        NextShot,     // until the next shot is over
        WholeMatch,   // until the end of the match
        Turns,        // for a number of turn changes (Turns)
    }

    // "Trou": the player points at the table, a fireball smashes a hole in
    // the felt, with a shake and a rumble for both players. A ball falling in
    // comes back where it was before the shot; the cue ball is a foul.
    // Level 1: one hole; level 2: a group around the spot.
    [CreateAssetMenu(fileName = "TableHolePower", menuName = "Pool/Powers/Table Hole (Attack)")]
    public class TableHolePower : TablePointPower
    {
        [Header("Trous")]
        [Tooltip("Rayon d'un trou (mètres). Une bille dont le centre passe au-dessus tombe. Une poche fait ~6 cm, une bille ~3 cm.")]
        [SerializeField] private float holeRadius = 0.075f;
        [Tooltip("Nombre de trous au niveau 1.")]
        [Min(1)] [SerializeField] private int level1Holes = 1;
        [Tooltip("Nombre de trous au niveau 2, répartis au hasard autour du point visé (voir les réglages ci-dessous).")]
        [Min(1)] [SerializeField] private int level2Holes = 3;
        [Tooltip("Niveau 2 : rayon (mètres) du cercle autour du point visé, sur lequel tombent la plupart des trous.")]
        [SerializeField] private float level2Spread = 0.2f;
        [Tooltip("Niveau 2 : de combien la distance au point visé varie autour du cercle, en part de son rayon (0 = pile sur le cercle, 0,3 = entre 70 % et 130 %).")]
        [Range(0f, 1f)] [SerializeField] private float level2SpreadJitter = 0.3f;
        [Tooltip("Niveau 2 : chance qu'un trou tombe à l'intérieur du cercle plutôt que dessus (0 = jamais, 1 = toujours).")]
        [Range(0f, 1f)] [SerializeField] private float level2InsideChance = 0.3f;
        [Tooltip("Niveau 2 : un des trous tombe toujours pile sur le point visé.")]
        [SerializeField] private bool level2CenterHole;
        [Tooltip("Niveau 2 : écart minimal (mètres) entre deux trous, bord à bord.")]
        [SerializeField] private float level2MinGap = 0.02f;

        [Header("Durée")]
        [Tooltip("Tour de l'adversaire : jusqu'à la fin du tour de l'adversaire de celui qui lance le pouvoir. Prochain tir : jusqu'à la fin du prochain tir. Toute la partie. Tours : pendant Turns changements de tour.")]
        [SerializeField] private TableHoleDuration duration = TableHoleDuration.OpponentTurn;
        [Tooltip("Durée « Tours » : nombre de changements de tour avant que le trou se referme.")]
        [Min(1)] [SerializeField] private int turns = 2;

        [Header("Météore")]
        [Tooltip("Boule de feu qui tombe (un effet en boucle). Vide = une simple sphère orange. Par défaut : Cartoon FX Remaster > CFXR Fire.")]
        [SerializeField] private GameObject meteorPrefab;
        [Tooltip("Échelle de la boule de feu par rapport à son prefab.")]
        [SerializeField] private float meteorScale = 0.25f;
        [Tooltip("Hauteur (mètres) d'où part le météore, au-dessus de la table.")]
        [SerializeField] private float fallHeight = 2.5f;
        [Tooltip("Décalage horizontal (mètres) du point de départ : le météore arrive en diagonale.")]
        [SerializeField] private float fallSideOffset = 1.5f;
        [Tooltip("Durée de la chute (secondes). Elle accélère jusqu'à l'impact.")]
        [SerializeField] private float fallTime = 0.55f;
        [Tooltip("Niveau 2 : délai (secondes) entre deux météores.")]
        [SerializeField] private float level2Stagger = 0.18f;
        [Tooltip("Effet joué à l'impact. Par défaut : CFXR3 Fire Explosion B.")]
        [SerializeField] private GameObject impactPrefab;
        [Tooltip("Échelle de l'effet d'impact par rapport à son prefab.")]
        [SerializeField] private float impactScale = 0.25f;
        [Tooltip("Durée (secondes) avant de supprimer l'effet d'impact.")]
        [SerializeField] private float impactLifetime = 3f;

        [Header("Le trou")]
        [Tooltip("Forme du trou généré : à quel point son contour est irrégulier (0 = rond, 0,4 = bosselé, 1 = très déchiqueté). Tirée au hasard à chaque impact ; les billes tombent selon cette forme.")]
        [Range(0f, 1f)] [SerializeField] private float shapeRoughness = 0.35f;
        [Tooltip("Nombre d'éclats (pointes plus longues, comme une déchirure) autour du trou, tirés au hasard à chaque impact. 0 = aucun.")]
        [Min(0)] [SerializeField] private int shapeSpikes = 5;
        [Tooltip("Longueur des éclats, en part du rayon du trou (0,5 = jusqu'à une fois et demie le rayon).")]
        [Range(0f, 1.5f)] [SerializeField] private float shapeSpikeLength = 0.5f;
        [Tooltip("Bord fondu du trou : largeur (en part du rayon) du dégradé entre le noir et le tapis brûlé autour. 0 = bord net.")]
        [Range(0f, 1f)] [SerializeField] private float edgeSoftness = 0.45f;
        [Tooltip("Braises sur le bord du trou, qui suivent sa forme (0 = aucune, 1 = bord très rougeoyant). Plus ou moins chaudes au hasard le long du bord.")]
        [Range(0f, 1f)] [SerializeField] private float emberAmount = 0.7f;
        [Tooltip("Couleur des braises.")]
        [SerializeField] private Color emberColor = new Color(1f, 0.42f, 0.08f, 1f);
        [Tooltip("Modèle du trou (facultatif), utilisé tel quel et mis à l'échelle du diamètre, tourné au hasard. Vide = la forme d'explosion générée (noire).")]
        [SerializeField] private GameObject holePrefab;
        [Tooltip("Échelle du modèle de trou, en plus du diamètre.")]
        [SerializeField] private float holePrefabScale = 1f;
        [Tooltip("Couleur du disque généré quand il n'y a pas de modèle.")]
        [SerializeField] private Color holeColor = Color.black;
        [Tooltip("Contour brûlé du trou, en decal projeté sur le tapis (matériau au shader « Shader Graphs/Decal »). Par défaut : TableHoleScorch, généré par Ensure Config Assets Exist. Il faut la fonction Decal dans le renderer URP (PC_Renderer > Add Renderer Feature > Decal) pour le voir. Vide = un simple cercle (Rim Color / Width).")]
        [SerializeField] private Material scorchDecal;
        [Tooltip("Taille du decal, en multiple du diamètre du trou (2,2 avec la texture générée : les braises tombent alors autour du trou).")]
        [SerializeField] private float scorchScale = 2.2f;
        [Tooltip("Épaisseur (mètres) de la zone où le decal se projette, centrée sur le tapis. Petite, pour ne pas marquer les billes qui passent.")]
        [SerializeField] private float scorchDepth = 0.02f;
        [Tooltip("Opacité du decal (0 à 1).")]
        [Range(0f, 1f)] [SerializeField] private float scorchOpacity = 1f;
        [Tooltip("Étirement au hasard du decal à chaque impact (0 = toujours rond ; 0,25 = jusqu'à 25 % plus long dans un sens que dans l'autre). Il est aussi tourné au hasard.")]
        [Range(0f, 0.6f)] [SerializeField] private float scorchStretch = 0.25f;
        [Tooltip("Couleur du cercle de bord, sans decal, et des cercles qui marquent l'endroit visé en attendant le tour de l'adversaire (0 d'opacité = pas de bord).")]
        [SerializeField] private Color rimColor = new Color(1f, 0.45f, 0.1f, 1f);
        [Tooltip("Épaisseur (mètres) de ces cercles.")]
        [SerializeField] private float rimWidth = 0.012f;
        [Tooltip("Fumée qui sort du trou tant qu'il est ouvert (effet en boucle, facultatif). Par défaut : CFXR Smoke Source 3D.")]
        [SerializeField] private GameObject smokePrefab;
        [Tooltip("Échelle de la fumée par rapport à son prefab.")]
        [SerializeField] private float smokeScale = 0.3f;
        [Tooltip("Débit de la fumée : multiplie le nombre de particules émises par seconde par le prefab (1 = celui du prefab, 0,5 = moitié moins, 2 = double).")]
        [Min(0f)] [SerializeField] private float smokeRate = 1f;
        [Tooltip("Durée (secondes) pendant laquelle le trou fume, avant que la fumée s'éteigne en douceur. 0 = tant que le trou est ouvert.")]
        [Min(0f)] [SerializeField] private float smokeDuration = 0f;
        [Tooltip("Durée (secondes) de la chute d'une bille dans le trou, avant qu'elle disparaisse.")]
        [SerializeField] private float sinkTime = 0.35f;
        [Tooltip("Durée (secondes) de la fermeture du trou.")]
        [SerializeField] private float closeTime = 0.3f;

        [Header("Impact : souffle, secousse, vibration")]
        [Tooltip("Vitesse (m/s) donnée aux billes proches de l'impact, diminuant jusqu'à 0 au bord du rayon. Les billes sur le trou en sont sorties d'abord.")]
        [SerializeField] private float blastSpeed = 0.6f;
        [Tooltip("Rayon (mètres) du souffle.")]
        [SerializeField] private float blastRadius = 0.3f;
        [Tooltip("Secousse de la vue des deux joueurs (mètres, 0 = aucune).")]
        [SerializeField] private float shakeMagnitude = 0.06f;
        [Tooltip("Durée de la secousse (secondes).")]
        [SerializeField] private float shakeDuration = 0.4f;
        [Tooltip("Flash de l'écran à l'impact (opacité 0 = aucun).")]
        [SerializeField] private Color flashColor = new Color(1f, 0.55f, 0.15f, 1f);
        [Range(0f, 1f)] [SerializeField] private float flashAlpha = 0.15f;
        [SerializeField] private float flashDuration = 0.25f;
        [Tooltip("Vibration des manettes : moteur lent (0 à 1).")]
        [Range(0f, 1f)] [SerializeField] private float rumbleLow = 0.7f;
        [Tooltip("Vibration des manettes : moteur rapide (0 à 1).")]
        [Range(0f, 1f)] [SerializeField] private float rumbleHigh = 1f;
        [Tooltip("Durée de la vibration (secondes, 0 = aucune).")]
        [SerializeField] private float rumbleDuration = 0.35f;

        public override PowerType Type => PowerType.Attack;

        public float HoleRadius => holeRadius;
        public TableHoleDuration Duration => duration;
        public int Turns => turns;
        public GameObject MeteorPrefab => meteorPrefab;
        public float MeteorScale => meteorScale;
        public float FallHeight => fallHeight;
        public float FallSideOffset => fallSideOffset;
        public float FallTime => fallTime;
        public GameObject ImpactPrefab => impactPrefab;
        public float ImpactScale => impactScale;
        public float ImpactLifetime => impactLifetime;
        public GameObject HolePrefab => holePrefab;
        public float HolePrefabScale => holePrefabScale;
        public Color HoleColor => holeColor;
        public float ShapeRoughness => shapeRoughness;
        public int ShapeSpikes => shapeSpikes;
        public float ShapeSpikeLength => shapeSpikeLength;
        public float EdgeSoftness => edgeSoftness;
        public float EmberAmount => emberAmount;
        public Color EmberColor => emberColor;
        public float ScorchStretch => scorchStretch;
        public float SmokeRate => smokeRate;
        public float SmokeDuration => smokeDuration;
        public Material ScorchDecal => scorchDecal;
        public float ScorchScale => scorchScale;
        public float ScorchDepth => scorchDepth;
        public float ScorchOpacity => scorchOpacity;
        public Color RimColor => rimColor;
        public float RimWidth => rimWidth;
        public GameObject SmokePrefab => smokePrefab;
        public float SmokeScale => smokeScale;
        public float SinkTime => sinkTime;
        public float CloseTime => closeTime;
        public float ShakeMagnitude => shakeMagnitude;
        public float ShakeDuration => shakeDuration;
        public Color FlashColor => flashColor;
        public float FlashAlpha => flashAlpha;
        public float FlashDuration => flashDuration;
        public float RumbleLow => rumbleLow;
        public float RumbleHigh => rumbleHigh;
        public float RumbleDuration => rumbleDuration;

        // A meteor hit the table (where, who launched it) — Feel's
        // "impact de météore" moment.
        public static event Action<Vector3, int> Impact;
        public static void RaiseImpact(Vector3 point, int owner) => Impact?.Invoke(point, owner);

        public int HoleCount(int level) => level >= 2 ? level2Holes : level1Holes;

        public override float Margin(int level) => holeRadius;

        // The whole area the group can land in.
        public override float PreviewRadius(int level) =>
            HoleCount(level) > 1 ? level2Spread * (1f + level2SpreadJitter) + holeRadius : holeRadius;

        // Used during one's own turn, the spots are locked and the meteors fall
        // when the opponent's turn starts; during the opponent's turn, at once.
        public override void Activate(PoolMatchRules match, int activatingPlayer, Vector3 point)
        {
            match.QueueTableHoles(this, HoleCenters(point, HoleCount(match.ActivatingPowerLevel)), activatingPlayer);
        }

        public void LaunchMeteors(IReadOnlyList<Vector3> centers, int owner, int generation)
        {
            for (int i = 0; i < centers.Count; i++)
                TableHoleMeteor.Launch(this, centers[i], owner, i * level2Stagger, generation);
        }

        // One hole on the spot, or a group scattered mostly around the Level 2
        // Spread circle, some inside, never overlapping (Min Gap), on the felt
        // and away from pockets.
        private List<Vector3> HoleCenters(Vector3 point, int count)
        {
            var centers = new List<Vector3>();
            if (count <= 1 || level2CenterHole) centers.Add(TablePointPower.ClampToTable(point, holeRadius));

            float minDistance = holeRadius * 2f + level2MinGap;
            float ringMin = level2Spread * (1f - level2SpreadJitter);
            float ringMax = level2Spread * (1f + level2SpreadJitter);
            while (centers.Count < count)
            {
                Vector3 best = Vector3.zero;
                float bestClearance = -1f;
                // A few tries for a spot clear of the others; the clearest
                // one if the table is too crowded for a perfect fit.
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    bool inside = UnityEngine.Random.value < level2InsideChance;
                    float distance = inside
                        ? Mathf.Sqrt(UnityEngine.Random.value) * ringMin   // even over the inner disc
                        : UnityEngine.Random.Range(ringMin, ringMax);
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    Vector3 candidate = TablePointPower.ClampToTable(
                        point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance, holeRadius);

                    float clearance = float.MaxValue;
                    foreach (Vector3 other in centers)
                        clearance = Mathf.Min(clearance, Vector3.Distance(candidate, other));
                    if (clearance > bestClearance) { bestClearance = clearance; best = candidate; }
                    if (clearance >= minDistance) break;
                }
                centers.Add(best);
            }
            return centers;
        }

        // On impact: balls sitting on the hole are moved to its edge, then
        // everything near is pushed away.
        public void BlastBalls(TableHole hole)
        {
            Vector3 center = hole.transform.position;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.Rigidbody == null || ball.Rigidbody.isKinematic) continue;
                Vector3 away = ball.transform.position - center;
                away.y = 0f;
                float distance = away.magnitude;
                Vector3 direction = distance > 1e-4f ? away / distance : Vector3.forward;
                float edge = hole.RadiusToward(direction) + ball.Radius + 0.005f;
                if (distance < edge)
                {
                    Vector3 outside = center + direction * edge;
                    ball.PlaceAt(new Vector3(outside.x, ball.transform.position.y, outside.z));
                    distance = edge;
                }
                if (blastSpeed <= 0f || blastRadius <= 0f || distance > blastRadius) continue;
                ball.Rigidbody.AddForce(direction * (blastSpeed * (1f - distance / blastRadius)), ForceMode.VelocityChange);
            }
        }
    }
}
