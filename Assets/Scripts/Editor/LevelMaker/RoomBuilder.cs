#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Builds rooms from a RoomOutline and a PrefabPalette. Pieces are placed
    // from their measured bounds, not their pivots, so any kit works. The
    // outline is the source of truth: a rebuild replaces every module.
    public static class RoomBuilder
    {
        public const string PropsRootName = "Décor (props)";
        private const int MaxTiles = 4000;

        // A piece a build would place: the ghost previews of the Level Maker
        // (PrefabGhost) draw these instead of instantiating anything.
        public struct GhostPiece
        {
            public GameObject prefab;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        // Set while previewing: pieces are recorded instead of instantiated,
        // so the ghost runs the very same layout code as the build.
        private static List<GhostPiece> recording;

        private static void Record(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale) =>
            recording.Add(new GhostPiece { prefab = prefab, position = position, rotation = rotation, scale = scale });

        // Runs layout code with Place / PlaceStairs recording into pieces.
        private static void Preview(List<GhostPiece> pieces, System.Action layout)
        {
            recording = pieces;
            try { layout(); }
            finally { recording = null; }
        }

        private static readonly Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();

        // ---------- Measuring ----------

        // Mesh bounds in the placement frame (root rotation and scale applied,
        // origin at the pivot).
        public static bool TryMeasure(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            if (prefab == null) return false;
            if (boundsCache.TryGetValue(prefab, out bounds)) return true;

            Transform root = prefab.transform;
            Matrix4x4 frame = Matrix4x4.TRS(Vector3.zero, root.localRotation, root.localScale) * root.worldToLocalMatrix;
            bool found = false;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                Matrix4x4 m = frame * renderer.transform.localToWorldMatrix;
                Bounds mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (found) boundsCache[prefab] = bounds;
            return found;
        }

        // Prefabs may have been edited since the last build.
        public static void ClearMeasures() => boundsCache.Clear();

        // Logs each piece's bounds: one that differs from the wall's gets
        // placed shifted relative to it.
        public static void LogPalette(PrefabPalette palette)
        {
            if (palette == null) return;
            ClearMeasures();
            var log = new System.Text.StringBuilder($"[Level Maker] Palette {palette.name} : module {ModuleWidth(palette):0.###} m, " +
                                                    $"mur le long de {(WallRunsAlongZ(palette) ? "Z" : "X")}, hauteur {WallHeight(palette):0.###} m\n");
            void Line(string label, GameObject prefab)
            {
                if (prefab == null) { log.AppendLine($"  {label} : (vide)"); return; }
                if (!TryMeasure(prefab, out Bounds b)) { log.AppendLine($"  {label} : {prefab.name} — aucun mesh mesurable"); return; }
                int renderers = prefab.GetComponentsInChildren<Renderer>(true).Length;
                bool lod = prefab.GetComponentInChildren<LODGroup>(true) != null;
                log.AppendLine($"  {label} : {prefab.name} — taille {b.size.x:0.###} × {b.size.y:0.###} × {b.size.z:0.###}, " +
                               $"centre {b.center.x:0.###} / {b.center.y:0.###} / {b.center.z:0.###}, bas {b.min.y:0.###}, " +
                               $"{renderers} renderer(s){(lod ? ", LODGroup" : "")}");
            }
            void Lines(string label, List<GameObject> list)
            {
                if (list == null || list.Count == 0) { log.AppendLine($"  {label} : (aucune variante)"); return; }
                for (int i = 0; i < list.Count; i++) Line($"{label} {i + 1}", list[i]);
            }
            Lines("Mur", palette.walls);
            Lines("Porte", palette.doors);
            Lines("Fenêtre", palette.windows);
            Lines("Poteau", palette.pillars);
            Lines("Sol", palette.floorTiles);
            Lines("Plafond", palette.ceilingTiles);
            Debug.Log(log.ToString(), palette);
        }

        // Width of a wall module, measured on the variant in use (variants of
        // different widths overlapped or left gaps). Doors and windows are
        // expected to match it.
        public static float ModuleWidth(PrefabPalette palette, int wallVariant = 0)
        {
            if (palette == null) return 2f;
            if (palette.moduleWidthOverride > 0f) return palette.moduleWidthOverride;
            GameObject wall = palette.Wall(wallVariant);
            if (!TryMeasure(wall, out Bounds b)) return 2f;
            float w = RunsAlongZ(palette, wall) ? b.size.z : b.size.x;
            return w > 0.05f ? w : 2f;
        }

        public static float ModuleWidth(RoomOutline room) => ModuleWidth(room.palette, room.wallVariant);

        // Which local horizontal axis of a wall variant follows the wall.
        public static bool WallRunsAlongZ(PrefabPalette palette, int wallVariant = 0) =>
            palette != null && RunsAlongZ(palette, palette.Wall(wallVariant));

        // Same for any piece laid along a wall (wall, door, window, railing):
        // the palette's axis if forced, else the piece's longer horizontal side.
        public static bool RunsAlongZ(PrefabPalette palette, GameObject piece)
        {
            if (palette != null && palette.wallAxis != PrefabPalette.Axis.Auto) return palette.wallAxis == PrefabPalette.Axis.Z;
            return TryMeasure(piece, out Bounds b) && b.size.z > b.size.x;
        }

        public static float WallHeight(PrefabPalette palette, int wallVariant = 0) =>
            palette != null && TryMeasure(palette.Wall(wallVariant), out Bounds b) && b.size.y > 0.05f ? b.size.y : 3f;

        // The floor or ceiling tile whose size is closest to a wall module
        // (so it covers the room with the least stretching).
        public static int BestTileFor(List<GameObject> tiles, float moduleWidth)
        {
            int best = 0;
            float bestError = float.MaxValue;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null) continue;
                Vector2 size = TileSize(tiles[i]);
                float error = Mathf.Max(Mathf.Abs(size.x - moduleWidth), Mathf.Abs(size.y - moduleWidth));
                if (error < bestError) { bestError = error; best = i; }
            }
            return best;
        }

        public static Vector2 TileSize(GameObject tile) =>
            TryMeasure(tile, out Bounds b) && b.size.x > 0.05f && b.size.z > 0.05f ? new Vector2(b.size.x, b.size.z) : new Vector2(2f, 2f);

        // ---------- Rooms ----------

        // A closed room gets its floor right away if the kit has a tile.
        // build = false: the caller sets openings first.
        public static RoomOutline CreateRoom(List<Vector3> points, bool closed, PrefabPalette palette,
            int wallVariant, int pillarVariant, int floorVariant, int ceilingVariant,
            int level = 0, Transform parent = null, bool build = true, bool hasWalls = true)
        {
            int index = Object.FindObjectsByType<RoomOutline>(FindObjectsInactive.Include).Length + 1;
            string label = hasWalls ? "Salle" : "Sol";
            var go = new GameObject(level > 0 ? $"{label} {index} (étage {level})" : $"{label} {index}");
            Undo.RegisterCreatedObjectUndo(go, "Tracer une salle");
            if (parent != null) go.transform.SetParent(parent, false);
            var room = go.AddComponent<RoomOutline>();
            room.points = new List<Vector3>(points);
            room.closed = closed;
            room.palette = palette;
            room.level = level;
            room.hasWalls = hasWalls;
            room.wallVariant = wallVariant;
            room.pillarVariant = pillarVariant;
            room.floorVariant = floorVariant;
            room.ceilingVariant = ceilingVariant;
            room.hasFloor = closed && palette != null && palette.FloorTile(floorVariant) != null;
            if (build)
            {
                Build(room);
                Selection.activeGameObject = go;
            }
            return room;
        }

        // Height of one level: the wall height (the next level's floor sits
        // on top of the walls below) — of the given wall variant.
        public static float StoreyHeight(PrefabPalette palette, int wallVariant = 0) => WallHeight(palette, wallVariant);

        public static List<RoomOutline> AllRooms() =>
            Object.FindObjectsByType<RoomOutline>(FindObjectsInactive.Exclude).ToList();

        // Deletes every built module of the room and places them again.
        public static void Build(RoomOutline room)
        {
            if (room == null) return;
            ClearMeasures();
            Undo.RecordObject(room, "Construire la salle");
            foreach (RoomModule module in room.GetComponentsInChildren<RoomModule>(true).ToList())
                if (module != null) Undo.DestroyObjectImmediate(module.gameObject);
            foreach (Transform child in room.transform.Cast<Transform>().ToList())
                if (child.childCount == 0 && child.GetComponents<Component>().Length == 1)
                    Undo.DestroyObjectImmediate(child.gameObject);

            PrefabPalette palette = room.palette;
            if (palette == null || room.SegmentCount == 0) return;

            Transform walls = room.hasWalls ? Folder(room.transform, "Murs") : null;
            float moduleWidth = ModuleWidth(room);
            for (int s = 0; s < room.SegmentCount && room.hasWalls; s++)
            {
                int cells = CellCount(room, s, palette);
                SegmentEnds(room, s, out Vector3 sa, out Vector3 sb);
                float length = Vector3.Distance(sa, sb);
                if (Mathf.Abs(length - cells * moduleWidth) > 0.02f)
                    Debug.LogWarning($"[Level Maker] {room.name} : le côté {s + 1} fait {length:0.##} m, pas un nombre entier de modules " +
                                     $"de {moduleWidth:0.##} m : ses {cells} murs se chevauchent ou s'écartent. Retrace la salle (la fermeture garde maintenant des murs entiers).", room);
                for (int c = 0; c < cells; c++) BuildWallCell(room, s, c, walls);
            }

            GameObject pillar = room.hasWalls && room.pillarVariant >= 0 ? palette.Pillar(room.pillarVariant) : null;
            if (pillar != null)
            {
                Transform pillars = Folder(room.transform, "Poteaux");
                for (int i = 0; i < room.points.Count; i++)
                {
                    int seg = i < room.SegmentCount ? i : room.SegmentCount - 1;
                    Place(pillar, pillars, room.points[i], SegmentYaw(room, seg), room.points[i].y, false, palette,
                        RoomModuleKind.Pillar, -1, -1);
                }
            }

            List<RoomOutline> rooms = AllRooms();

            // Floor: holed where stairs from the level below come up.
            GameObject floorTile = palette.FloorTile(room.floorVariant);
            if (room.closed && room.hasFloor && floorTile != null)
            {
                List<StairsFootprint> arriving = rooms.Where(r => r.level == room.level - 1 && r.palette != null)
                    .SelectMany(Footprints).ToList();
                FillOutline(room, floorTile, Folder(room.transform, "Sol"), room.points[0].y, true, RoomModuleKind.Floor,
                    (center, half) => (room.HasVoid && Inside(center, room.floorVoid)) || arriving.Any(f => f.Overlaps(center, half)));
            }
            if (room.HasVoid) BuildVoidRailings(room, Folder(room.transform, "Garde-corps"));

            // Ceiling: not under a room of the level above (its floor is the ceiling).
            GameObject ceilingTile = palette.CeilingTile(room.ceilingVariant);
            if (room.closed && room.hasCeiling && ceilingTile != null)
            {
                List<RoomOutline> above = rooms.Where(r => r.level == room.level + 1 && r.closed && r.hasFloor).ToList();
                FillOutline(room, ceilingTile, Folder(room.transform, "Plafond"),
                    room.points[0].y + WallHeight(palette, room.wallVariant), false, RoomModuleKind.Ceiling,
                    (center, half) => above.Any(r => Inside(center, r.points)));
            }

            if (room.stairs.Count > 0)
            {
                Transform folder = Folder(room.transform, "Escaliers");
                foreach (RoomStairs flight in room.stairs) PlaceStairs(room, flight, folder);
            }
        }

        // Rebuilds a room and the rooms of the levels just above and below,
        // whose floor holes and ceilings depend on it.
        public static void BuildWithNeighbours(RoomOutline room)
        {
            Build(room);
            foreach (RoomOutline other in AllRooms())
                if (other != room && Mathf.Abs(other.level - room.level) == 1) Build(other);
        }

        // Railings along the floor void, one per module, except along the
        // room's own walls and where stairs leave a cell open.
        public static IEnumerable<(int index, Vector3 center, float yaw)> VoidCells(RoomOutline room)
        {
            float w = ModuleWidth(room);
            int index = 0;
            for (int i = 0; i < room.floorVoid.Count; i++)
            {
                Vector3 a = room.floorVoid[i], b = room.floorVoid[(i + 1) % room.floorVoid.Count];
                int cells = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / w));
                Vector3 dir = LevelBuilder.Flat(b - a);
                Vector3 axis = RunsAlongZ(room.palette, room.palette.Railing(room.railingVariant)) ? Vector3.forward : Vector3.right;
                float yaw = Vector3.SignedAngle(axis, dir, Vector3.up);
                for (int c = 0; c < cells; c++, index++)
                {
                    Vector3 center = Vector3.Lerp(a, b, (c + 0.5f) / cells);
                    if (!OnOutline(center, room.points)) yield return (index, center, yaw);
                }
            }
        }

        private static void BuildVoidRailings(RoomOutline room, Transform parent)
        {
            GameObject railing = room.palette.Railing(room.railingVariant);
            if (railing == null) return;
            foreach (var (index, center, yaw) in VoidCells(room))
                if (!room.openRailingCells.Contains(index))
                    Place(railing, parent, center, yaw, room.points[0].y, false, room.palette, RoomModuleKind.Railing, -1, index);
        }

        // On one of the outline's edges (within 5 cm).
        private static bool OnOutline(Vector3 p, List<Vector3> outline)
        {
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector3 ab = new Vector3(b.x - a.x, 0f, b.z - a.z);
                Vector3 ap = new Vector3(p.x - a.x, 0f, p.z - a.z);
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                if ((ap - ab * t).sqrMagnitude < 0.0025f) return true;
            }
            return false;
        }

        // ---------- Stairs ----------

        // Where a flight stands: its footprint on the floor and the way it climbs.
        public struct StairsFootprint
        {
            public Vector3 center;
            public Vector3 uphill;
            public float halfLength, halfWidth;

            // Any of the tile's corners (pulled in a little) inside the footprint.
            public bool Overlaps(Vector3 tileCenter, Vector2 tileHalf)
            {
                Vector3 across = Vector3.Cross(Vector3.up, uphill);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = tileCenter + new Vector3(((i & 1) == 0 ? -0.75f : 0.75f) * tileHalf.x, 0f,
                        ((i & 2) == 0 ? -0.75f : 0.75f) * tileHalf.y);
                    Vector3 d = corner - center;
                    if (Mathf.Abs(Vector3.Dot(d, uphill)) < halfLength && Mathf.Abs(Vector3.Dot(d, across)) < halfWidth) return true;
                }
                Vector3 c = tileCenter - center;
                return Mathf.Abs(Vector3.Dot(c, uphill)) < halfLength && Mathf.Abs(Vector3.Dot(c, across)) < halfWidth;
            }
        }

        public static IEnumerable<StairsFootprint> Footprints(RoomOutline room)
        {
            foreach (RoomStairs flight in room.stairs)
            {
                GameObject prefab = room.palette.Stairs(flight.variant);
                if (prefab == null || !TryStairsShape(room.palette, prefab, out Bounds b, out Vector3 uphillLocal)) continue;
                yield return Footprint(flight, b, uphillLocal);
            }
        }

        public static StairsFootprint Footprint(RoomStairs flight, Bounds b, Vector3 uphillLocal)
        {
            bool alongZ = Mathf.Abs(uphillLocal.z) > Mathf.Abs(uphillLocal.x);
            return new StairsFootprint
            {
                center = flight.position,
                uphill = Quaternion.Euler(0f, flight.uphillYaw, 0f) * Vector3.forward,
                halfLength = (alongZ ? b.size.z : b.size.x) / 2f,
                halfWidth = (alongZ ? b.size.x : b.size.z) / 2f,
            };
        }

        // Stairs bounds and the direction they climb: along the longest side,
        // toward the higher half of the mesh (else +axis). The palette's
        // stairsReversed fixes kits where the guess is wrong.
        public static bool TryStairsShape(PrefabPalette palette, GameObject prefab, out Bounds bounds, out Vector3 uphillLocal)
        {
            uphillLocal = Vector3.forward;
            if (!TryMeasure(prefab, out bounds)) return false;
            Vector3 axis = bounds.size.z >= bounds.size.x ? Vector3.forward : Vector3.right;

            float highPlus = float.NegativeInfinity, highMinus = float.NegativeInfinity;
            Transform root = prefab.transform;
            Matrix4x4 frame = Matrix4x4.TRS(Vector3.zero, root.localRotation, root.localScale) * root.worldToLocalMatrix;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Matrix4x4 m = frame * filter.transform.localToWorldMatrix;
                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    if (Vector3.Dot(p - bounds.center, axis) >= 0f) highPlus = Mathf.Max(highPlus, p.y);
                    else highMinus = Mathf.Max(highMinus, p.y);
                }
            }
            uphillLocal = highMinus > highPlus + 0.01f ? -axis : axis;
            if (palette != null && palette.stairsReversed) uphillLocal = -uphillLocal;
            return true;
        }

        private static void PlaceStairs(RoomOutline room, RoomStairs flight, Transform parent)
        {
            PrefabPalette palette = room.palette;
            GameObject prefab = palette.Stairs(flight.variant);
            if (prefab == null || !TryStairsShape(palette, prefab, out Bounds b, out Vector3 uphillLocal)) return;

            float floorY = room.points[0].y;
            float storey = StoreyHeight(palette, room.wallVariant);
            // Stretched to exactly one level, so the top step meets the floor above.
            float stretch = b.size.y > 0.05f ? Mathf.Clamp(storey / b.size.y, 0.3f, 3f) : 1f;

            float yaw = flight.uphillYaw - Vector3.SignedAngle(Vector3.forward, uphillLocal, Vector3.up);
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

            // Stretch the prefab's local axis that points up (its root may be
            // rotated, e.g. -90° on X for an imported model).
            Vector3 localUp = Quaternion.Inverse(prefab.transform.localRotation) * Vector3.up;
            Vector3 scale = prefab.transform.localScale;
            int upAxis = Mathf.Abs(localUp.x) > Mathf.Abs(localUp.y) ? (Mathf.Abs(localUp.x) > Mathf.Abs(localUp.z) ? 0 : 2)
                : (Mathf.Abs(localUp.y) > Mathf.Abs(localUp.z) ? 1 : 2);
            scale[upAxis] *= stretch;
            Vector3 position = flight.position - turn * new Vector3(b.center.x, 0f, b.center.z);
            position.y = floorY - b.min.y * stretch;
            Quaternion rotation = turn * prefab.transform.localRotation;
            if (recording != null) { Record(prefab, position, rotation, scale); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Escalier");
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.SetPositionAndRotation(position, rotation);
            Finish(go, palette, RoomModuleKind.Stairs, -1, -1, makeStatic: true);

            // An invisible ramp over the steps: the character controller
            // walks up a slope where it could stop against a step.
            StairsFootprint f = Footprint(flight, b, uphillLocal);
            float run = f.halfLength * 2f;
            var ramp = new GameObject("Rampe (collision)");
            Undo.RegisterCreatedObjectUndo(ramp, "Escalier");
            ramp.transform.SetParent(parent, false);
            Vector3 slope = (f.uphill * run + Vector3.up * storey).normalized;
            ramp.transform.SetPositionAndRotation(new Vector3(f.center.x, floorY + storey / 2f, f.center.z),
                Quaternion.LookRotation(slope, Vector3.up));
            var box = ramp.AddComponent<BoxCollider>();
            box.size = new Vector3(f.halfWidth * 2f, 0.1f, Mathf.Sqrt(run * run + storey * storey));
            box.center = new Vector3(0f, -0.05f, 0f);
            RoomModule marker = ramp.AddComponent<RoomModule>();
            marker.kind = RoomModuleKind.Stairs;
            float angle = Mathf.Atan2(storey, run) * Mathf.Rad2Deg;
            if (angle > 45f)
                Debug.LogWarning($"[Level Maker] {room.name} : escalier de {angle:0}° — au-delà de 45°, le joueur risque de ne pas pouvoir monter (pente max du CharacterController).", room);
        }

        public static void AddStairs(RoomOutline room, RoomStairs flight)
        {
            Undo.RecordObject(room, "Escalier");
            room.stairs.Add(flight);
            BuildWithNeighbours(room);
        }

        // Removes the flight closest to point (within maxDistance) from any room.
        public static bool RemoveStairsNear(Vector3 point, float maxDistance)
        {
            RoomOutline best = null;
            int bestIndex = -1;
            float bestDistance = maxDistance;
            foreach (RoomOutline room in AllRooms())
                for (int i = 0; i < room.stairs.Count; i++)
                {
                    float d = Vector3.Distance(room.stairs[i].position, point);
                    if (d < bestDistance) { best = room; bestIndex = i; bestDistance = d; }
                }
            if (best == null) return false;
            Undo.RecordObject(best, "Retirer l'escalier");
            best.stairs.RemoveAt(bestIndex);
            BuildWithNeighbours(best);
            return true;
        }

        // The closed room whose floor (within 0.5 m) holds this point.
        public static RoomOutline RoomAt(Vector3 point)
        {
            foreach (RoomOutline room in AllRooms())
                if (room.closed && room.points.Count > 2 && Mathf.Abs(room.points[0].y - point.y) < 0.5f && Inside(point, room.points))
                    return room;
            return null;
        }

        public static int CellCount(RoomOutline room, int segment, PrefabPalette palette)
        {
            SegmentEnds(room, segment, out Vector3 a, out Vector3 b);
            return Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / ModuleWidth(palette, room.wallVariant)));
        }

        public static void SegmentEnds(RoomOutline room, int segment, out Vector3 a, out Vector3 b)
        {
            a = room.points[segment];
            b = room.points[(segment + 1) % room.points.Count];
        }

        // Yaw that lays a piece's length along the segment. In a closed room
        // the same face ends up inside however it was traced; the palette's
        // flip picks that face, the room's flip overrides it.
        // piece: what's laid there (the room's wall when null).
        private static float SegmentYaw(RoomOutline room, int segment, GameObject piece = null)
        {
            SegmentEnds(room, segment, out Vector3 a, out Vector3 b);
            bool flip = room.closed && !room.IsCounterClockwise;
            if (room.flipWalls) flip = !flip;
            return SegmentYaw(a, b, room.palette, piece != null ? piece : room.palette?.Wall(room.wallVariant), flip);
        }

        // flip: the outline's own turn-around (clockwise closed trace, the
        // room's manual flip); the palette's flip is added here.
        private static float SegmentYaw(Vector3 a, Vector3 b, PrefabPalette palette, GameObject piece, bool flip)
        {
            Vector3 dir = LevelBuilder.Flat(b - a);
            Vector3 axis = RunsAlongZ(palette, piece) ? Vector3.forward : Vector3.right;
            float yaw = Vector3.SignedAngle(axis, dir, Vector3.up);
            if (palette != null && palette.flipWalls) flip = !flip;
            return flip ? yaw + 180f : yaw;
        }

        // The prefab a wall cell entry stands for (null for none).
        public static GameObject PieceFor(PrefabPalette palette, RoomModuleKind kind, int variant) => kind switch
        {
            RoomModuleKind.Door => palette.Door(variant),
            RoomModuleKind.Window => palette.Window(variant),
            RoomModuleKind.Railing => palette.Railing(variant),
            RoomModuleKind.Wall => palette.Wall(variant),
            _ => null,
        };

        // Cells a piece takes along the wall: a wide door takes its
        // neighbours (5 % tolerance).
        public static int SpanOf(PrefabPalette palette, GameObject piece, float moduleWidth)
        {
            if (piece == null || !TryMeasure(piece, out Bounds b)) return 1;
            float length = RunsAlongZ(palette, piece) ? b.size.z : b.size.x;
            return Mathf.Max(1, Mathf.CeilToInt(length / moduleWidth - 0.05f));
        }

        private static GameObject BuildWallCell(RoomOutline room, int segment, int cell, Transform parent)
        {
            PrefabPalette palette = room.palette;
            SegmentEnds(room, segment, out Vector3 a, out Vector3 b);
            int cells = CellCount(room, segment, palette);

            RoomOpening choice = room.CellAt(segment, cell);
            if (choice.kind == RoomModuleKind.None || choice.kind == RoomModuleKind.Covered) return null;   // open, or taken by a wider neighbour
            int span = Mathf.Min(choice.Span, cells - cell);
            Vector3 center = Vector3.Lerp(a, b, (cell + span / 2f) / cells);

            GameObject prefab = PieceFor(palette, choice.kind, choice.variant);
            RoomModuleKind kind = choice.kind;
            // A kit without railings: the side stays open rather than walled.
            if (prefab == null && kind == RoomModuleKind.Railing) return null;
            // A kit without doors/windows: a plain wall keeps the room closed.
            if (prefab == null) { prefab = palette.Wall(room.wallVariant); kind = RoomModuleKind.Wall; }
            if (prefab == null) return null;
            GameObject placed = Place(prefab, parent, center, SegmentYaw(room, segment, prefab), a.y, false, palette, kind, segment, cell);

            // A piece nested in this one (cage door in its frame): same spot, on the floor.
            if (choice.hasInsert)
            {
                GameObject insert = PieceFor(palette, choice.insertKind, choice.insertVariant);
                if (insert != null)
                    Place(insert, parent, center, SegmentYaw(room, segment, insert), a.y, false, palette, choice.insertKind, segment, cell);
            }
            return placed;
        }

        // Rebuilds every wall cell of one side (spans changed).
        private static void RebuildSide(RoomOutline room, int segment)
        {
            foreach (RoomModule module in room.GetComponentsInChildren<RoomModule>(true).ToList())
                if (module.segment == segment)   // void railings have segment -1
                    Undo.DestroyObjectImmediate(module.gameObject);
            Transform walls = Folder(room.transform, "Murs");
            int cells = CellCount(room, segment, room.palette);
            for (int c = 0; c < cells; c++) BuildWallCell(room, segment, c, walls);
        }

        // Frees a cell and everything tied to it: the cells its piece covers,
        // or — if it is covered — the wider piece covering it.
        private static void FreeCell(RoomOutline room, int segment, int cell)
        {
            int owner = room.OwnerCell(segment, cell);
            RoomOpening o = room.CellAt(segment, owner);
            for (int c = owner; c < owner + o.Span; c++)
                room.openings.RemoveAll(e => e.segment == segment && e.cell == c);
        }

        // Puts a piece in a wall cell (empty ones too). A wide piece takes
        // the cells it needs, centred when possible, replacing what was
        // there. insert = nest it in the existing piece (None removes it).
        public static bool SetCell(RoomOutline room, int segment, int cell, RoomModuleKind kind, int variant, bool insert = false)
        {
            ClearMeasures();
            PrefabPalette palette = room.palette;
            int cells = CellCount(room, segment, palette);
            Undo.RecordObject(room, "Ouverture");
            cell = room.OwnerCell(segment, cell);

            if (insert)
            {
                RoomOpening host = room.CellAt(segment, cell);
                host.hasInsert = kind != RoomModuleKind.None && kind != RoomModuleKind.Covered;
                host.insertKind = kind;
                host.insertVariant = variant;
                room.SetCell(host);
                RebuildSide(room, segment);
                return true;
            }

            int span = kind == RoomModuleKind.None ? 1 : SpanOf(palette, PieceFor(palette, kind, variant), ModuleWidth(room));
            if (span > cells)
            {
                Debug.LogWarning($"[Level Maker] {room.name} : la pièce fait {span} modules, ce côté n'en a que {cells}.", room);
                return false;
            }
            int start = Mathf.Clamp(cell - (span - 1) / 2, 0, cells - span);
            for (int c = start; c < start + span; c++) FreeCell(room, segment, c);
            room.SetCell(new RoomOpening { segment = segment, cell = start, kind = kind, variant = variant, span = span });
            for (int c = start + 1; c < start + span; c++)
                room.SetCell(new RoomOpening { segment = segment, cell = c, kind = RoomModuleKind.Covered, variant = start });
            RebuildSide(room, segment);
            return true;
        }

        // A room traced inside another on the same level takes no floor (two
        // floors flicker), and its wall cells that stand on an existing wall
        // are left empty. Returns the host, or null.
        public static RoomOutline FitIntoHost(RoomOutline room)
        {
            if (room.palette == null || room.points.Count < 2) return null;
            List<RoomOutline> others = AllRooms().Where(r => r != room && r.level == room.level && r.palette != null).ToList();
            RoomOutline host = others.FirstOrDefault(r => r.closed && r.points.Count > 2
                && room.points.All(p => Inside(p, r.points) || OnOutline(p, r.points))
                && room.points.Any(p => Inside(p, r.points)));
            if (host == null) return null;

            room.hasFloor = false;
            float ownHalf = WallHalfThickness(room.palette, room.wallVariant);
            for (int s = 0; s < room.SegmentCount; s++)
            {
                SegmentEnds(room, s, out Vector3 a, out Vector3 b);
                Vector3 dir = LevelBuilder.Flat(b - a);
                int cells = CellCount(room, s, room.palette);
                for (int c = 0; c < cells; c++)
                {
                    Vector3 center = Vector3.Lerp(a, b, (c + 0.5f) / cells);
                    if (others.Any(o => o.hasWalls && AlongExistingWall(o, center, dir, ownHalf)))
                        room.SetCell(s, c, RoomModuleKind.None, 0);
                }
            }
            return host;
        }

        // Half the thickness of a wall variant (its shorter horizontal side).
        private static float WallHalfThickness(PrefabPalette palette, int wallVariant) =>
            TryMeasure(palette.Wall(wallVariant), out Bounds b) ? Mathf.Min(b.size.x, b.size.z) / 2f : 0.1f;

        // A wall module at center along dir would stand inside one of other's
        // walls. Geometric, so it works when modules are shifted along it.
        private static bool AlongExistingWall(RoomOutline other, Vector3 center, Vector3 dir, float ownHalf)
        {
            float reach = ownHalf + WallHalfThickness(other.palette, other.wallVariant) + 0.05f;
            for (int s = 0; s < other.SegmentCount; s++)
            {
                SegmentEnds(other, s, out Vector3 a, out Vector3 b);
                Vector3 along = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float length = along.magnitude;
                if (length < 0.01f) continue;
                along /= length;
                if (Mathf.Abs(Vector3.Dot(along, dir)) < 0.98f) continue;   // not parallel
                Vector3 offset = new Vector3(center.x - a.x, 0f, center.z - a.z);
                float t = Vector3.Dot(offset, along);
                if (t < 0f || t > length) continue;                        // beyond its ends
                if ((offset - along * t).magnitude <= reach) return true;  // within its thickness
            }
            return false;
        }

        public static IEnumerable<(int segment, int cell, Vector3 center)> EmptyCells(RoomOutline room)
        {
            if (room.palette == null) yield break;
            for (int s = 0; s < room.SegmentCount; s++)
            {
                SegmentEnds(room, s, out Vector3 a, out Vector3 b);
                int cells = CellCount(room, s, room.palette);
                for (int c = 0; c < cells; c++)
                    if (room.CellAt(s, c).kind == RoomModuleKind.None)
                        yield return (s, c, Vector3.Lerp(a, b, (c + 0.5f) / cells));
            }
        }

        // Turns the clicked wall module into a door, a window, a wall… of the
        // given variant, or nests that piece in it (insert).
        public static void SetOpening(RoomModule module, RoomModuleKind kind, int variant, bool insert = false)
        {
            RoomOutline room = module.GetComponentInParent<RoomOutline>();
            if (room == null || module.segment < 0) return;
            SetCell(room, module.segment, module.cell, kind, variant, insert);
        }

        // Tiles on the room's grid whose centre is inside the outline (floor:
        // top at y; ceiling: bottom at y). skip leaves a tile out. A tile close
        // to the module size is stretched to exactly one cell so the floor
        // meets the walls; others keep their size and run on under the walls.
        private static void FillOutline(RoomOutline room, GameObject tile, Transform parent, float y, bool topAtY, RoomModuleKind kind,
            System.Func<Vector3, Vector2, bool> skip = null) =>
            FillOutline(room.points, room.palette, room.wallVariant, room.hasWalls, room.name, tile, parent, y, topAtY, kind, skip);

        // Same from an outline's data (a trace not yet turned into a room).
        private static void FillOutline(List<Vector3> points, PrefabPalette palette, int wallVariant, bool hasWalls, string label,
            GameObject tile, Transform parent, float y, bool topAtY, RoomModuleKind kind, System.Func<Vector3, Vector2, bool> skip = null)
        {
            Vector2 native = TileSize(tile);
            Vector2 size = native;
            Vector3 stretch = Vector3.one;
            float module = ModuleWidth(palette, wallVariant);
            // A floor-only zone is traced in whole tiles: always at their own size.
            bool fits = hasWalls && palette.fitTilesToModule
                        && Mathf.Abs(native.x - module) <= module * palette.tileFitTolerance
                        && Mathf.Abs(native.y - module) <= module * palette.tileFitTolerance;
            if (fits)
            {
                size = new Vector2(module, module);
                stretch = new Vector3(module / native.x, 1f, module / native.y);
            }
            Vector3 origin = points[0];
            float minX = points.Min(p => p.x), maxX = points.Max(p => p.x);
            float minZ = points.Min(p => p.z), maxZ = points.Max(p => p.z);
            int x0 = Mathf.FloorToInt((minX - origin.x) / size.x), x1 = Mathf.CeilToInt((maxX - origin.x) / size.x);
            int z0 = Mathf.FloorToInt((minZ - origin.z) / size.y), z1 = Mathf.CeilToInt((maxZ - origin.z) / size.y);
            if ((long)(x1 - x0) * (z1 - z0) > MaxTiles)
            {
                // Not repeated for every frame of a preview.
                if (recording == null) Debug.LogWarning($"[Level Maker] {label} : plus de {MaxTiles} dalles, sol/plafond ignoré (dalle trop petite ?).");
                return;
            }
            for (int ix = x0; ix < x1; ix++)
                for (int iz = z0; iz < z1; iz++)
                {
                    var center = new Vector3(origin.x + (ix + 0.5f) * size.x, y, origin.z + (iz + 0.5f) * size.y);
                    bool laid = fits ? Inside(center, points) : Touches(center, size / 2f, points);
                    if (laid && (skip == null || !skip(center, size / 2f)))
                        Place(tile, parent, center, 0f, y, topAtY, palette, kind, -1, -1, stretch);
                }
        }

        // Some of the tile (center, a corner or an edge middle, pulled in a
        // little) lies inside the outline.
        private static bool Touches(Vector3 center, Vector2 half, List<Vector3> polygon)
        {
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                    if (Inside(center + new Vector3(ix * half.x * 0.98f, 0f, iz * half.y * 0.98f), polygon)) return true;
            return false;
        }

        public static bool Inside(Vector3 p, List<Vector3> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector3 a = polygon[i], b = polygon[j];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        // ---------- Props ----------

        public static GameObject PlaceProp(GameObject prefab, RaycastHit hit, float yaw, float scale, bool alignToSurface, PrefabPalette palette) =>
            PlaceProp(prefab, hit.point, alignToSurface ? hit.normal : Vector3.up, yaw, scale, palette, null);

        // Base centered on point, standing along normal. parent = null: the
        // scene's props folder.
        public static GameObject PlaceProp(GameObject prefab, Vector3 point, Vector3 normal, float yaw, float scale,
            PrefabPalette palette, Transform parent)
        {
            if (prefab == null) return null;
            if (parent == null)
            {
                GameObject rootGo = GameObject.Find(PropsRootName);
                if (rootGo == null)
                {
                    rootGo = new GameObject(PropsRootName);
                    Undo.RegisterCreatedObjectUndo(rootGo, "Placer un prop");
                }
                parent = rootGo.transform;
            }

            PropPose(prefab, point, normal, yaw, scale, out Vector3 position, out Quaternion rotation, out Vector3 localScale);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Placer un prop");
            go.transform.SetParent(parent, false);
            go.transform.localScale = localScale;
            go.transform.SetPositionAndRotation(position, rotation);
            Finish(go, palette, RoomModuleKind.Prop, -1, -1, makeStatic: go.GetComponentInChildren<Rigidbody>() == null);
            return go;
        }

        // ---------- Previews (ghosts of the Level Maker's tools) ----------

        // Preview of a trace being drawn: walls, pillars, and the floor once
        // closed. faceAsClosed: walls face as in the closed room it will
        // likely become.
        public static void PreviewTrace(List<Vector3> points, bool closed, bool faceAsClosed, PrefabPalette palette,
            int wallVariant, int pillarVariant, int floorVariant, bool hasWalls, List<GhostPiece> into)
        {
            if (palette == null || points.Count < 2) return;
            Preview(into, () =>
            {
                int segments = closed ? points.Count : points.Count - 1;
                if (hasWalls)
                {
                    bool flip = (closed || faceAsClosed) && points.Count >= 3 && !RoomOutline.IsCounterClockwiseOutline(points);
                    float module = ModuleWidth(palette, wallVariant);
                    GameObject wall = palette.Wall(wallVariant);
                    GameObject pillar = pillarVariant >= 0 ? palette.Pillar(pillarVariant) : null;
                    for (int s = 0; s < segments; s++)
                    {
                        Vector3 a = points[s], b = points[(s + 1) % points.Count];
                        int cells = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / module));
                        if (wall != null)
                            for (int c = 0; c < cells; c++)
                                Place(wall, null, Vector3.Lerp(a, b, (c + 0.5f) / cells), SegmentYaw(a, b, palette, wall, flip), a.y, false,
                                    palette, RoomModuleKind.Wall, s, c);
                    }
                    if (pillar != null)
                        for (int i = 0; i < points.Count; i++)
                        {
                            int s = i < segments ? i : segments - 1;
                            Place(pillar, null, points[i], SegmentYaw(points[s], points[(s + 1) % points.Count], palette, wall, flip),
                                points[i].y, false, palette, RoomModuleKind.Pillar, -1, -1);
                        }
                }
                GameObject floor = palette.FloorTile(floorVariant);
                if (closed && floor != null)
                    FillOutline(points, palette, wallVariant, hasWalls, "Aperçu", floor, null, points[0].y, true, RoomModuleKind.Floor);
            });
        }

        // The piece SetCell would lay on that wall cell (kind None: nothing,
        // the cell is emptied), on the cells it would take.
        public static void PreviewOpening(RoomOutline room, int segment, int cell, RoomModuleKind kind, int variant, bool insert,
            List<GhostPiece> into)
        {
            if (room == null || room.palette == null || segment < 0 || kind == RoomModuleKind.None || kind == RoomModuleKind.Covered) return;
            PrefabPalette palette = room.palette;
            GameObject piece = PieceFor(palette, kind, variant);
            if (piece == null) return;
            int cells = CellCount(room, segment, palette);
            cell = room.OwnerCell(segment, cell);
            int start, span;
            if (insert)
            {
                start = cell;
                span = Mathf.Min(room.CellAt(segment, cell).Span, cells - cell);
            }
            else
            {
                span = SpanOf(palette, piece, ModuleWidth(room));
                if (span > cells) return;
                start = Mathf.Clamp(cell - (span - 1) / 2, 0, cells - span);
            }
            SegmentEnds(room, segment, out Vector3 a, out Vector3 b);
            Vector3 center = Vector3.Lerp(a, b, (start + span / 2f) / cells);
            Preview(into, () => Place(piece, null, center, SegmentYaw(room, segment, piece), a.y, false, palette, kind, segment, start));
        }

        // The floor (and ceiling, ceilingVariant >= 0) the Floors tool would
        // give the room. Holes for stairs and rooms above are not cut here.
        public static void PreviewFloors(RoomOutline room, int floorVariant, int ceilingVariant, List<GhostPiece> into)
        {
            if (room == null || room.palette == null || !room.closed) return;
            PrefabPalette palette = room.palette;
            Preview(into, () =>
            {
                GameObject floor = palette.FloorTile(floorVariant);
                if (floor != null) FillOutline(room, floor, null, room.points[0].y, true, RoomModuleKind.Floor);
                GameObject ceiling = ceilingVariant >= 0 ? palette.CeilingTile(ceilingVariant) : null;
                if (ceiling != null)
                    FillOutline(room, ceiling, null, room.points[0].y + WallHeight(palette, room.wallVariant), false, RoomModuleKind.Ceiling);
            });
        }

        // The flight AddStairs would place (stretched to one level).
        public static void PreviewStairs(RoomOutline room, RoomStairs flight, List<GhostPiece> into)
        {
            if (room == null || room.palette == null) return;
            Preview(into, () => PlaceStairs(room, flight, null));
        }

        // Pose of a prop placed with PlaceProp. Shared with the Props ghost.
        public static void PropPose(GameObject prefab, Vector3 point, Vector3 normal, float yaw, float scale,
            out Vector3 position, out Quaternion rotation, out Vector3 localScale)
        {
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f);
            TryMeasure(prefab, out Bounds b);
            Vector3 baseCenter = new Vector3(b.center.x, b.min.y, b.center.z) * scale;
            position = point - turn * baseCenter;
            rotation = turn * prefab.transform.localRotation;
            localScale = prefab.transform.localScale * scale;
        }

        // ---------- Helpers ----------

        // stretch: scale along the placement frame's X / Y / Z (world axes at
        // yaw 0), applied to whichever local axes of the prefab they map to.
        private static GameObject Place(GameObject prefab, Transform parent, Vector3 center, float yaw, float y, bool topAtY,
            PrefabPalette palette, RoomModuleKind kind, int segment, int cell, Vector3? stretch = null)
        {
            TryMeasure(prefab, out Bounds b);
            Vector3 s = stretch ?? Vector3.one;
            b = new Bounds(Vector3.Scale(b.center, s), Vector3.Scale(b.size, s));
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 position = center - turn * new Vector3(b.center.x, 0f, b.center.z);
            position.y = y - (topAtY ? b.max.y : b.min.y);
            Quaternion rotation = turn * prefab.transform.localRotation;
            Vector3 scale = stretch.HasValue ? StretchedScale(prefab.transform, s) : prefab.transform.localScale;
            if (recording != null) { Record(prefab, position, rotation, scale); return null; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Construire la salle");
            go.transform.SetParent(parent, false);
            if (stretch.HasValue) go.transform.localScale = scale;
            go.transform.SetPositionAndRotation(position, rotation);
            Finish(go, palette, kind, segment, cell, makeStatic: true);
            return go;
        }

        // Local scale stretched along the placement axes, for a rotated root.
        private static Vector3 StretchedScale(Transform root, Vector3 stretch)
        {
            Vector3 scale = root.localScale;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 local = Vector3.zero;
                local[axis] = 1f;
                Vector3 frame = root.localRotation * local;   // where this local axis points at yaw 0
                int dominant = Mathf.Abs(frame.x) >= Mathf.Abs(frame.y) && Mathf.Abs(frame.x) >= Mathf.Abs(frame.z) ? 0
                    : Mathf.Abs(frame.y) >= Mathf.Abs(frame.z) ? 1 : 2;
                scale[axis] *= stretch[dominant];
            }
            return scale;
        }

        private static void Finish(GameObject go, PrefabPalette palette, RoomModuleKind kind, int segment, int cell, bool makeStatic)
        {
            RoomModule marker = go.AddComponent<RoomModule>();
            marker.kind = kind;
            marker.segment = segment;
            marker.cell = cell;

            if (palette != null && palette.addMissingColliders && go.GetComponentInChildren<Collider>(true) == null)
                foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;

            // For baked lighting: contributes to the probes and gets its
            // indirect light from them (nothing goes in lightmaps).
            if (makeStatic)
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                        StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.receiveGI = ReceiveGI.LightProbes;
            }
        }

        private static Transform Folder(Transform room, string name)
        {
            if (recording != null) return null;   // previewing: nothing is created
            Transform folder = room.Find(name);
            if (folder != null) return folder;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Construire la salle");
            go.transform.SetParent(room, false);
            return go.transform;
        }
    }
}
#endif
