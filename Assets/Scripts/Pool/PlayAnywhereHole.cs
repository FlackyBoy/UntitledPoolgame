using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // One point of a hole's course, in the hole's space: the path goes
    // through it, the play area is Width wide there.
    [System.Serializable]
    public struct PlayAnywherePathPoint
    {
        public Vector3 position;
        [Min(0.1f)] public float width;
    }

    // Play Anywhere: one course (hole) of the match, set by hand in the scene
    // (Tools > Pool > Play Anywhere > Créer un trou). Each player starts on
    // their tee and sinks their own cue ball in the cup. A match plays the
    // scene's holes in Order. The rules are in PlayAnywhereRuleSet; this
    // only says where things are.
    //
    // The course runs from the tees through the path points to the cup; the
    // play area is the corridor along it, as wide as each point says, and
    // following its height (a course on the counter: off the counter is
    // out). Edited with handles in the Scene view (PlayAnywhereHoleEditor).
    [AddComponentMenu("Pool/Play Anywhere/Trou")]
    public class PlayAnywhereHole : MonoBehaviour, IPlayAnywhereArrowLayout
    {
        [Tooltip("Place de ce parcours dans la partie : les trous de la scène se jouent du plus petit numéro au plus grand.")]
        [SerializeField] private int order = 1;
        [Tooltip("Décoché : ce parcours se joue sans caisse, donc sans phase de pose ; aucune caisse n'est affichée.")]
        [SerializeField] private bool useCrate = true;
        [Tooltip("Caisse des objets de ce parcours. Vide = la première caisse de la scène.")]
        [SerializeField] private PlayAnywhereCrate crate;
        [Tooltip("Nombre de coups attendu pour ce trou (affiché à côté du score).")]
        [Min(1)] [SerializeField] private int par = 3;
        [Tooltip("Départ du joueur 1 : la bille est posée dessus (point au sol).")]
        [SerializeField] private Transform player1Tee;
        [Tooltip("Départ du joueur 2. Vide = à côté du départ du joueur 1.")]
        [SerializeField] private Transform player2Tee;
        [Tooltip("Centre du trou, au sol.")]
        [SerializeField] private Transform cup;
        [Tooltip("Rayon du trou (mètres). Une bille dont le centre passe à moins de cette distance, assez lentement, tombe dedans.")]
        [Min(0.01f)] [SerializeField] private float cupRadius = 0.0648f;
        [Tooltip("Drapeau du trou (créé par l'outil). Vide = l'enfant « Drapeau ».")]
        [SerializeField] private Transform flag;
        [Tooltip("Taille du drapeau (1 = 80 cm de haut).")]
        [Min(0.1f)] [SerializeField] private float flagSize = 1f;

        [Header("Flèches au-dessus du drapeau")]
        [Tooltip("Flèches néon flottantes, qui pointent vers le drapeau, rebondissent et clignotent. Aperçu jaune dans la Scene quand le trou est sélectionné. Rebonds et clignotement : Flag Marker de PlayAnywhereSettings.")]
        [SerializeField] private bool flagMarker = true;
        [Tooltip("Nombre de flèches, en couronne. Plusieurs, pour que chaque joueur en voie toujours une de face. 0 = aucune.")]
        [Min(0)] [SerializeField] private int flagArrowsCount = 4;
        [Tooltip("Taille des flèches (1 = 90 cm de long).")]
        [Min(0.05f)] [SerializeField] private float flagArrowsSize = 0.8f;
        [Tooltip("Position (mètres) du point que visent les flèches, par rapport au haut du drapeau. Y = plus haut, X et Z = sur le côté.")]
        [SerializeField] private Vector3 flagArrowsOffset = new Vector3(0f, 0.15f, 0f);
        [Tooltip("Distance (mètres) entre ce point et le centre d'une flèche, au plus près.")]
        [Min(0f)] [SerializeField] private float flagArrowsDistance = 0.75f;
        [Tooltip("Inclinaison (degrés) des flèches : 5 = presque au-dessus du point visé, 85 = presque à l'horizontale autour de lui.")]
        [Range(5f, 85f)] [SerializeField] private float flagArrowsTilt = 35f;
        [Tooltip("Vitesse (degrés par seconde) à laquelle les flèches tournent autour du drapeau. 0 = fixes.")]
        [SerializeField] private float flagArrowsSpin = 30f;

        [Header("Parcours (zone de jeu)")]
        [Tooltip("Points du parcours entre les départs et le trou, dans l'ordre (dans l'espace du trou). Se posent et se déplacent avec les poignées dans la Scene.")]
        [SerializeField] private List<PlayAnywherePathPoint> path = new List<PlayAnywherePathPoint>();
        [Tooltip("Largeur (mètres) du couloir aux départs et au trou, et des nouveaux points. 0 = pas de zone de jeu : seules la chute, les poches de la table et les zones hors limites comptent.")]
        [Min(0f)] [SerializeField] private float pathWidth = 3f;
        [Tooltip("Une bille plus bas que le parcours de ce nombre de mètres est hors limites (tombée du comptoir, par exemple). Entre deux points, le parcours est à la hauteur du plus bas des deux : on peut sauter d'un point en hauteur vers un point au sol.")]
        [Min(0.05f)] [SerializeField] private float pathFallDepth = 0.5f;
        [Tooltip("Panneaux d'orientation sur les côtés du parcours, déduits de ses points (réglages : Signs de PlayAnywhereSettings). Aperçu orange dans la Scene quand le trou est sélectionné ; le bouton « Placer les panneaux dans la scène » permet de les déplacer et de les redimensionner.")]
        [SerializeField] private bool courseSigns = true;
        [Tooltip("Mur de lumière rouge sur les bords de la zone de jeu, en jeu (réglages : Course Wall de PlayAnywhereSettings). Sans effet sur la bille.")]
        [SerializeField] private bool courseWall = true;

        public int Order => order;
        public bool UsesCrate => useCrate;
        // Its own crate, else the scene's; none if it plays without.
        public PlayAnywhereCrate Crate => !useCrate ? null : crate != null ? crate : PlayAnywhereCrate.Current;
        public int Par => par;
        public float CupRadius => cupRadius;
        public Vector3 CupPosition => cup != null ? cup.position : transform.position;
        public Transform Cup => cup;
        public Transform Flag => flag;
        public Transform Player1Tee => player1Tee;
        public Transform Player2Tee => player2Tee;
        public float PathWidth => pathWidth;
        public bool CourseSigns => courseSigns;
        public int ArrowCount => flagArrowsCount;
        public float ArrowSize => flagArrowsSize;
        public Vector3 ArrowOffset => flagArrowsOffset;
        public float ArrowDistance => flagArrowsDistance;
        public float ArrowTilt => flagArrowsTilt;
        public float ArrowSpin => flagArrowsSpin;

        // The top of the flag, or the cup without one.
        private Vector3 FlagTop
        {
            get
            {
                Vector3 top = CupPosition;
                bool found = false;
                if (flag != null)
                    foreach (Renderer r in flag.GetComponentsInChildren<Renderer>(true))
                    {
                        top.y = found ? Mathf.Max(top.y, r.bounds.max.y) : r.bounds.max.y;
                        found = true;
                    }
                return top;
            }
        }

        // The signs placed in the scene from the Inspector, if any.
        public Transform PlacedSigns => transform.Find(PlayAnywhereCourseSigns.RootName);

        // Its pieces (tees, cup, flag, any décor put under it), shown only
        // while it's the course being played. Those already off stay off.
        private Renderer[] shownRenderers;
        private Collider[] shownColliders;
        private bool shown = true;

        // Made the first time it's shown: a level laid out for every mode
        // doesn't pay for them, and the hole's own décor is solid by then.
        private Transform signs;
        private Transform wall;
        private Transform marker;
        private bool markingsBuilt;

        // A level is laid out for every mode: in play, a hole stays hidden
        // until a Play Anywhere match shows it (one already running has
        // decided for it).
        private void Awake()
        {
            if (PlayAnywhereRuleSet.Current == null) SetShown(false);
        }

        public void SetShown(bool shown)
        {
            this.shown = shown;
            if (shownRenderers == null)
            {
                shownRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r => r.enabled);
                shownColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(), c => c.enabled);
            }
            foreach (Renderer r in shownRenderers)
                if (r != null) r.enabled = shown;
            foreach (Collider c in shownColliders)
                if (c != null) c.enabled = shown;

            if (shown && !markingsBuilt)
            {
                markingsBuilt = true;
                var settings = PoolSettingsLoader.LoadOrDefault<PlayAnywhereSettings>("PlayAnywhereSettings");
                Transform placed = PlacedSigns;
                if (!courseSigns && placed != null) placed.gameObject.SetActive(false);
                else if (courseSigns) signs = placed != null ? placed : PlayAnywhereCourseSigns.Build(this, settings);
                if (courseWall) wall = PlayAnywhereCourseWall.Build(this, settings);
                if (flagMarker) marker = PlayAnywhereArrowRing.Build("Repère du drapeau", settings.flagMarkerPrefab, this, FlagTop, transform, settings);
            }
            if (signs != null) signs.gameObject.SetActive(shown);
            if (wall != null) wall.gameObject.SetActive(shown);
            if (marker != null) marker.gameObject.SetActive(shown);
        }

        // Where the player's ball rests at the start: on the tee, raised by
        // its radius. Player 2 without a tee stands a few balls aside.
        public Vector3 TeePosition(int player, float ballRadius)
        {
            Transform tee = player == 1 && player2Tee != null ? player2Tee : player1Tee != null ? player1Tee : transform;
            Vector3 spot = tee.position;
            if (player == 1 && player2Tee == null) spot += tee.right * ballRadius * 6f;
            return spot + Vector3.up * ballRadius;
        }

        // ---- Path: node 0 is between the tees, then the points, then the cup.

        public int PointCount => path.Count;
        public int NodeCount => path.Count + 2;

        public void GetNode(int index, out Vector3 position, out float width)
        {
            if (index <= 0)
            {
                position = (TeePosition(0, 0f) + TeePosition(1, 0f)) * 0.5f;
                width = pathWidth;
            }
            else if (index > path.Count)
            {
                position = CupPosition;
                width = pathWidth;
            }
            else
            {
                PlayAnywherePathPoint point = path[index - 1];
                position = transform.TransformPoint(point.position);
                width = point.width;
            }
        }

        public void SetPoint(int index, Vector3 worldPosition, float width) =>
            path[index] = new PlayAnywherePathPoint { position = transform.InverseTransformPoint(worldPosition), width = Mathf.Max(0.1f, width) };

        public void InsertPoint(int index, Vector3 worldPosition, float width)
        {
            path.Insert(index, default);
            SetPoint(index, worldPosition, width);
        }

        public void RemovePoint(int index) => path.RemoveAt(index);

        // In the corridor of some stretch of the course (round ends, so the
        // turns join up), and not fallen below it. The floor of a stretch is
        // its lower end, not a slope between its ends: a course that drops
        // off a counter is a fall, not a ramp. No ceiling: a ball can jump
        // over a wall as long as it lands back in.
        public bool InPlayArea(Vector3 point)
        {
            if (pathWidth <= 0f) return true;
            GetNode(0, out Vector3 a, out float widthA);
            for (int i = 1; i < NodeCount; i++)
            {
                GetNode(i, out Vector3 b, out float widthB);
                Vector2 ab = Flat(b - a);
                float length2 = ab.sqrMagnitude;
                float t = length2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(Flat(point - a), ab) / length2) : 0f;
                Vector3 closest = Vector3.Lerp(a, b, t);
                float half = Mathf.Lerp(widthA, widthB, t) * 0.5f;
                if (Flat(point - closest).sqrMagnitude <= half * half && point.y >= Mathf.Min(a.y, b.y) - pathFallDepth)
                    return true;
                a = b;
                widthA = widthB;
            }
            return false;
        }

        // How wide the play area is at its stretch nearest to this point
        // (infinite without a course). In 3D: a floor above isn't near.
        public float WidthAt(Vector3 point)
        {
            if (pathWidth <= 0f) return float.PositiveInfinity;
            float width = pathWidth, nearest = float.PositiveInfinity;
            GetNode(0, out Vector3 a, out float widthA);
            for (int i = 1; i < NodeCount; i++)
            {
                GetNode(i, out Vector3 b, out float widthB);
                Vector2 ab = Flat(b - a);
                float length2 = ab.sqrMagnitude;
                float t = length2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(Flat(point - a), ab) / length2) : 0f;
                float distance = (point - Vector3.Lerp(a, b, t)).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    width = Mathf.Lerp(widthA, widthB, t);
                }
                a = b;
                widthA = widthB;
            }
            return width;
        }

        // Its narrowest stretch (infinite without a course).
        public float NarrowestWidth
        {
            get
            {
                if (pathWidth <= 0f) return float.PositiveInfinity;
                float narrowest = float.PositiveInfinity;
                for (int i = 0; i < NodeCount; i++)
                {
                    GetNode(i, out _, out float width);
                    narrowest = Mathf.Min(narrowest, width);
                }
                return narrowest;
            }
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        // Lowest point of the hole (tees, path and cup): below it by Out Of
        // Bounds Depth, a ball has fallen out of the course.
        public float LowestY
        {
            get
            {
                float y = CupPosition.y;
                if (player1Tee != null) y = Mathf.Min(y, player1Tee.position.y);
                if (player2Tee != null) y = Mathf.Min(y, player2Tee.position.y);
                foreach (PlayAnywherePathPoint point in path)
                    y = Mathf.Min(y, transform.TransformPoint(point.position).y);
                return y;
            }
        }

        // The cup's flat disc (Créer un trou) follows the radius, so what's
        // drawn is what catches the ball. A cup set on another object keeps
        // its own scale. The flag follows its size.
        private void OnValidate()
        {
            if (flag == null) flag = transform.Find("Drapeau");
            if (flag != null) flag.localScale = Vector3.one * flagSize;
            if (cup == null || cup.localScale.y > 0.01f) return;
            cup.localScale = new Vector3(cupRadius * 2f, cup.localScale.y, cupRadius * 2f);
        }

        private void OnDrawGizmosSelected()
        {
            if (flagMarker) PlayAnywhereArrowRing.DrawPreviewGizmos(this, FlagTop, new Color(1f, 0.85f, 0.1f));
        }

        private void OnDrawGizmos()
        {
            // Nor its course in the Game view when it isn't played.
            if (Application.isPlaying && !shown) return;
            Gizmos.color = Color.yellow;
            DrawCircle(CupPosition, cupRadius);
            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            if (player1Tee != null) Gizmos.DrawLine(player1Tee.position, CupPosition);
            Gizmos.color = new Color(1f, 0.35f, 0.3f);
            if (player2Tee != null) Gizmos.DrawLine(player2Tee.position, CupPosition);

            // The course's corridor: out of it, out of bounds.
            if (pathWidth <= 0f) return;
            Gizmos.color = new Color(0.3f, 1f, 0.4f);
            GetNode(0, out Vector3 a, out float widthA);
            DrawCircle(a, widthA * 0.5f);
            for (int i = 1; i < NodeCount; i++)
            {
                GetNode(i, out Vector3 b, out float widthB);
                Vector3 along = b - a;
                along.y = 0f;
                Vector3 side = along.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, along.normalized) : Vector3.right;
                Gizmos.DrawLine(a + side * (widthA * 0.5f), b + side * (widthB * 0.5f));
                Gizmos.DrawLine(a - side * (widthA * 0.5f), b - side * (widthB * 0.5f));
                Gizmos.DrawLine(a, b);
                DrawCircle(b, widthB * 0.5f);
                a = b;
                widthA = widthB;
            }
        }

        private static void DrawCircle(Vector3 c, float radius)
        {
            const int Segments = 24;
            for (int i = 0; i < Segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / Segments, a1 = (i + 1) * Mathf.PI * 2f / Segments;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                                c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }
    }
}
