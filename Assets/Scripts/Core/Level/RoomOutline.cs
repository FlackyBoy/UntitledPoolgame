using System;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Appended only: the values are serialized in scenes. None = nothing in
    // that wall cell (open side, wall already standing from a neighbour).
    // Covered = taken by a wider piece starting in another cell (a double
    // door): nothing of its own is built there.
    public enum RoomModuleKind { Wall, Door, Window, Pillar, Floor, Ceiling, Prop, None, Railing, Stairs, Covered }

    [Serializable]
    public struct RoomOpening
    {
        public int segment;   // index of the traced segment
        public int cell;      // module index along it
        public RoomModuleKind kind;
        public int variant;   // index in the palette's list for that kind (Covered: the cell it belongs to)
        [Tooltip("Nombre de modules occupés par la pièce, à partir de cette case (0 ou 1 = un seul).")]
        public int span;
        [Tooltip("Une seconde pièce imbriquée dans celle-ci (ex. porte de cage dans un cadre).")]
        public bool hasInsert;
        public RoomModuleKind insertKind;
        public int insertVariant;
        [Tooltip("Palette d'où vient la pièce (vide = celle de la salle).")]
        public PrefabPalette palette;
        [Tooltip("Pièce tournée de 180° (un modèle dont la face visible est de l'autre côté).")]
        public bool flipped;

        public int Span => Mathf.Max(1, span);
    }

    // A flight of stairs from this room's floor up to the next level.
    [Serializable]
    public struct RoomStairs
    {
        public Vector3 position;   // center of its footprint, on this room's floor
        public float uphillYaw;    // world direction it climbs toward (degrees, 0 = +Z)
        public int variant;
        public PrefabPalette palette;   // null = the room's
    }

    // One floor tile, ceiling tile or pillar of another variant than the
    // room's. position: centre of its cell (tiles) or the outline point
    // (pillars), compared on the horizontal.
    [Serializable]
    public struct RoomPieceVariant
    {
        public RoomModuleKind kind;
        public Vector3 position;
        public int variant;
        public PrefabPalette palette;   // null = the room's part palette (FloorPalette…)
    }

    // A room traced with the Level Maker: its outline, which variant of each
    // kit piece it uses, and which wall modules differ (doors, windows,
    // another wall variant). The modules themselves are children, rebuilt
    // from this data (another kit, flipped walls…) without losing the
    // layout. Variants are indices into the palette's lists, so they carry
    // over to another kit. Edit-time data only: nothing reads it while playing.
    public class RoomOutline : MonoBehaviour
    {
        [Tooltip("Points du tracé, en coordonnées monde, à la hauteur du sol.")]
        public List<Vector3> points = new List<Vector3>();
        public bool closed;
        public PrefabPalette palette;
        [Tooltip("Modules qui ne sont pas le mur de la salle : portes, fenêtres, autre variante de mur.")]
        public List<RoomOpening> openings = new List<RoomOpening>();
        public bool hasFloor;
        public bool hasCeiling;
        [Tooltip("Décoché : zone de sol seule (tracée librement), sans murs ni poteaux.")]
        public bool hasWalls = true;
        [Tooltip("Variante de mur de la salle (index dans la palette).")]
        public int wallVariant;
        [Tooltip("Variante de poteau (index dans la palette, -1 = pas de poteau).")]
        public int pillarVariant;
        [Tooltip("Variante de dalle de sol (index dans la palette).")]
        public int floorVariant;
        [Tooltip("Variante de dalle de plafond (index dans la palette).")]
        public int ceilingVariant;
        [Tooltip("Retourne les murs de cette salle d'un demi-tour (utile surtout pour un tracé ouvert, qui n'a pas d'intérieur).")]
        public bool flipWalls;
        [Tooltip("Étage de la salle (0 = rez-de-chaussée). Ses points sont à la hauteur de cet étage.")]
        public int level;
        [Tooltip("Escaliers qui partent du sol de cette salle vers l'étage au-dessus.")]
        public List<RoomStairs> stairs = new List<RoomStairs>();
        [Tooltip("Vide dans le sol (mezzanine) : pas de dalles à l'intérieur, garde-corps sur ses bords qui ne sont pas des murs.")]
        public List<Vector3> floorVoid = new List<Vector3>();
        [Tooltip("Variante de garde-corps autour du vide.")]
        public int railingVariant;
        [Tooltip("Modules du bord du vide laissés ouverts (arrivée d'un escalier), numérotés le long du vide.")]
        public List<int> openRailingCells = new List<int>();

        [Tooltip("Dalles de sol, de plafond et poteaux remplacés un par un (autre variante que celle de la salle).")]
        public List<RoomPieceVariant> pieceVariants = new List<RoomPieceVariant>();
        [Tooltip("Palette d'où viennent le sol, le plafond, les poteaux (vide = celle de la salle) : une salle Greybox avec le sol d'un autre kit.")]
        public PrefabPalette floorPalette, ceilingPalette, pillarPalette;

        public PrefabPalette FloorPalette => floorPalette != null ? floorPalette : palette;
        public PrefabPalette CeilingPalette => ceilingPalette != null ? ceilingPalette : palette;
        public PrefabPalette PillarPalette => pillarPalette != null ? pillarPalette : palette;

        // The palette a part of the room comes from (Floor, Ceiling, Pillar;
        // the room's for the rest).
        public PrefabPalette PartPalette(RoomModuleKind kind) => kind switch
        {
            RoomModuleKind.Floor => FloorPalette,
            RoomModuleKind.Ceiling => CeilingPalette,
            RoomModuleKind.Pillar => PillarPalette,
            _ => palette,
        };

        // A piece's own palette, or the one it falls back to.
        public PrefabPalette Own(PrefabPalette piecePalette, RoomModuleKind kind) => piecePalette != null ? piecePalette : PartPalette(kind);

        public bool HasVoid => floorVoid != null && floorVoid.Count > 2;

        // Within 5 cm on the horizontal: tiles are at least tens of cm wide.
        private static bool SameSpot(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz < 0.0025f;
        }

        // The variant (and palette) of the piece of that kind at position:
        // its own, else fallback and the part's palette.
        public int PieceVariant(RoomModuleKind kind, Vector3 position, int fallback, out PrefabPalette from)
        {
            from = PartPalette(kind);
            if (pieceVariants == null) return fallback;
            foreach (RoomPieceVariant p in pieceVariants)
                if (p.kind == kind && SameSpot(p.position, position))
                {
                    from = Own(p.palette, kind);
                    return p.variant;
                }
            return fallback;
        }

        // roomVariant: the room's own variant for that kind; the room's own
        // choice (same variant, same palette) isn't stored.
        public void SetPieceVariant(RoomModuleKind kind, Vector3 position, int variant, PrefabPalette from, int roomVariant)
        {
            pieceVariants ??= new List<RoomPieceVariant>();
            pieceVariants.RemoveAll(p => p.kind == kind && SameSpot(p.position, position));
            PrefabPalette part = PartPalette(kind);
            if (variant != roomVariant || (from != null && from != part))
                pieceVariants.Add(new RoomPieceVariant { kind = kind, position = position, variant = variant, palette = from == part ? null : from });
        }

        public int SegmentCount => points.Count < 2 ? 0 : closed ? points.Count : points.Count - 1;

        // Seen from above (X right, Z up): counter-clockwise outlines have
        // their inside on the left of each segment.
        public bool IsCounterClockwise => IsCounterClockwiseOutline(points);

        // Same for any outline (a trace not yet turned into a room, for the
        // Level Maker's previews).
        public static bool IsCounterClockwiseOutline(List<Vector3> outline)
        {
            float area = 0f;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % outline.Count];
                area += a.x * b.z - b.x * a.z;
            }
            return area > 0f;
        }

        // What goes in a wall cell: its own choice, else the room's wall.
        public RoomOpening CellAt(int segment, int cell)
        {
            foreach (RoomOpening o in openings)
                if (o.segment == segment && o.cell == cell) return o;
            return new RoomOpening { segment = segment, cell = cell, kind = RoomModuleKind.Wall, variant = wallVariant };
        }

        public void SetCell(int segment, int cell, RoomModuleKind kind, int variant)
        {
            openings.RemoveAll(o => o.segment == segment && o.cell == cell);
            if (kind != RoomModuleKind.Wall || variant != wallVariant)
                openings.Add(new RoomOpening { segment = segment, cell = cell, kind = kind, variant = variant });
        }

        // Stores a full cell description (span, insert); a plain room wall
        // with nothing else is the default and isn't stored.
        public void SetCell(RoomOpening entry)
        {
            openings.RemoveAll(o => o.segment == entry.segment && o.cell == entry.cell);
            if (entry.palette == palette) entry.palette = null;
            bool isDefault = entry.kind == RoomModuleKind.Wall && entry.variant == wallVariant && entry.Span == 1 && !entry.hasInsert
                             && entry.palette == null && !entry.flipped;
            if (!isDefault) openings.Add(entry);
        }

        // The cell a covered cell belongs to (itself otherwise).
        public int OwnerCell(int segment, int cell)
        {
            RoomOpening o = CellAt(segment, cell);
            return o.kind == RoomModuleKind.Covered ? o.variant : cell;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            for (int i = 0; i < SegmentCount; i++)
                Gizmos.DrawLine(points[i] + Vector3.up * 0.02f, points[(i + 1) % points.Count] + Vector3.up * 0.02f);
        }
    }
}
