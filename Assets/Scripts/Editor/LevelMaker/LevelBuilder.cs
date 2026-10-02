#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Core;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // Scene queries and operations behind the Level Maker window and its
    // checks. Everything goes through Undo and only adds or adjusts what's
    // missing: nothing aligned by hand is rebuilt.
    public static class LevelBuilder
    {
        public const string SpawnRootName = "PlayerSpawnPoints";
        public const string RoomPowerPointsName = "PowerSpawnPoints (salle)";

        // ---------- Queries ----------

        public static List<T> FindAll<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();

        public static PoolMatchRules Rules => FindAll<PoolMatchRules>().FirstOrDefault();
        public static PoolTableSurface Surface => FindAll<PoolTableSurface>().FirstOrDefault();
        public static PlayerInputManager InputManager => FindAll<PlayerInputManager>().FirstOrDefault();

        // The table as placed in the scene: the model instance holding the
        // physics (or the generated table itself).
        public static GameObject TableRoot
        {
            get
            {
                PoolMatchRules rules = Rules;
                if (rules == null) return null;
                GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(rules.gameObject);
                if (outer != null) return outer;
                // "Attach Physics" nests PoolPhysics under the model.
                Transform parent = rules.transform.parent;
                return parent != null ? parent.gameObject : rules.gameObject;
            }
        }

        // World bounds of the table's visible model (falls back to the felt rectangle).
        public static bool TryGetTableBounds(out Bounds bounds)
        {
            bounds = default;
            GameObject table = TableRoot;
            bool found = false;
            if (table != null)
            {
                foreach (Renderer r in table.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    if (!found) { bounds = r.bounds; found = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }
            PoolTableSurface surface = Surface;
            if (!found && surface != null)
            {
                bounds = new Bounds(surface.transform.position, Vector3.zero);
                foreach (Vector3 corner in FeltCorners(surface)) bounds.Encapsulate(corner);
                found = true;
            }
            return found;
        }

        public static Vector3[] FeltCorners(PoolTableSurface surface, float inflate = 0f)
        {
            Transform t = surface.transform;
            // Same rotation-only frame as PoolTableSurface (its scale isn't meters).
            // The felt's top face: the surface collider's top, not its center.
            float top = 0f;
            if (surface.TryGetComponent(out BoxCollider box)) top = box.bounds.max.y - t.position.y;
            Vector3 up = Vector3.up * top;
            float l = surface.HalfLength + inflate, w = surface.HalfWidth + inflate;
            return new[]
            {
                t.position + up + t.rotation * new Vector3(-l, 0f, -w),
                t.position + up + t.rotation * new Vector3(l, 0f, -w),
                t.position + up + t.rotation * new Vector3(l, 0f, w),
                t.position + up + t.rotation * new Vector3(-l, 0f, w),
            };
        }

        // Everything a cue needs for LocalCueHolder, by name (empty = usable).
        // Cue fills its own properties in Awake, which doesn't run in edit mode.
        public static List<string> MissingCueParts(Cue cue)
        {
            var missing = new List<string>();
            if (cue.GetComponent<LocalGrabbable>() == null) missing.Add("LocalGrabbable");
            if (cue.GetComponent<CueChargeSlide>() == null) missing.Add("CueChargeSlide");
            bool left = false, right = false;
            foreach (InteractionTarget grip in cue.GetComponentsInChildren<InteractionTarget>(true))
            {
                if (grip.effectorType == FullBodyBipedEffector.LeftHand) left = true;
                else if (grip.effectorType == FullBodyBipedEffector.RightHand) right = true;
            }
            if (!left) missing.Add("poignée main gauche (InteractionTarget)");
            if (!right) missing.Add("poignée main droite (InteractionTarget)");
            return missing;
        }

        public static Transform[] SpawnPoints
        {
            get
            {
                ScenePlayerSpawner spawner = FindAll<ScenePlayerSpawner>().FirstOrDefault();
                if (spawner == null) return new Transform[0];
                SerializedProperty list = new SerializedObject(spawner).FindProperty("spawnPoints");
                var points = new Transform[list.arraySize];
                for (int i = 0; i < points.Length; i++)
                    points[i] = list.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                return points;
            }
        }

        // First solid surface below a point (triggers ignored).
        public static bool TryFindGround(Vector3 from, out RaycastHit hit, float height = 3f, float depth = 10f)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(from + Vector3.up * height, Vector3.down, out hit, height + depth,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        // ---------- Table ----------

        public static GameObject PlaceTable(GameObject prefab, Vector3 position, float yaw)
        {
            var table = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(table, "Placer la table");
            table.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Selection.activeGameObject = table;
            return table;
        }

        // Lays the missing cues on the floor past one end of the table,
        // side by side and parallel to the end rail.
        public static List<GameObject> PlaceMissingCues(LevelMakerSettings settings)
        {
            var placed = new List<GameObject>();
            if (settings.cuePrefab == null) return placed;
            int usable = FindAll<Cue>().Count(c => MissingCueParts(c).Count == 0);
            int toPlace = settings.cueCount - usable;
            if (toPlace <= 0) return placed;

            Vector3 center = Vector3.zero;
            Vector3 along = Vector3.right, across = Vector3.forward;
            float endDistance = 1.5f;
            PoolTableSurface surface = Surface;
            if (surface != null)
            {
                center = surface.transform.position;
                along = Flat(surface.transform.right);
                across = Flat(surface.transform.forward);
                endDistance = surface.HalfLength + 0.5f;
            }
            else if (TryGetTableBounds(out Bounds b))
            {
                center = b.center;
                endDistance = b.extents.x + 0.5f;
            }

            for (int i = 0; i < toPlace; i++)
            {
                var cue = (GameObject)PrefabUtility.InstantiatePrefab(settings.cuePrefab, SceneManager.GetActiveScene());
                Undo.RegisterCreatedObjectUndo(cue, "Placer une queue");
                float side = (usable + i) * 0.3f - 0.15f;
                Vector3 spot = center + along * endDistance + across * side;
                // Lying along the end rail: its longest axis turned onto `across`.
                cue.transform.rotation = Quaternion.FromToRotation(LongestAxis(cue), across) * cue.transform.rotation;
                cue.transform.position = spot;
                float lift = 0.03f;
                if (TryFindGround(spot, out RaycastHit ground) && TryGetBounds(cue, out Bounds cb))
                    cue.transform.position += Vector3.up * (ground.point.y + lift - cb.min.y);
                cue.name = $"Cue {usable + i + 1}";
                placed.Add(cue);
            }
            return placed;
        }

        // ---------- Players ----------

        public static PlayerInputManager EnsureInputManager(LevelMakerSettings settings)
        {
            PlayerInputManager manager = InputManager;
            if (manager == null)
            {
                var go = new GameObject("PlayerInputManager");
                Undo.RegisterCreatedObjectUndo(go, "Gestionnaire de joueurs");
                manager = go.AddComponent<PlayerInputManager>();
            }

            var so = new SerializedObject(manager);
            if (so.FindProperty("m_PlayerPrefab").objectReferenceValue == null && settings.playerPrefab != null)
                so.FindProperty("m_PlayerPrefab").objectReferenceValue = settings.playerPrefab;
            so.FindProperty("m_SplitScreen").boolValue = true;
            so.FindProperty("m_MaxPlayerCount").intValue = 2;
            so.ApplyModifiedProperties();

            if (manager.GetComponent<ScenePlayerSpawner>() == null)
                Undo.AddComponent<ScenePlayerSpawner>(manager.gameObject);
            return manager;
        }

        // Two spawn points facing the table across its long axis (or around
        // the origin without a table), wired into ScenePlayerSpawner.
        // Existing valid points are kept.
        public static void EnsureSpawnPoints(LevelMakerSettings settings)
        {
            PlayerInputManager manager = EnsureInputManager(settings);
            var spawner = manager.GetComponent<ScenePlayerSpawner>();
            var so = new SerializedObject(spawner);
            SerializedProperty list = so.FindProperty("spawnPoints");

            Vector3 center = Vector3.zero, along = Vector3.right;
            float distance = 2f;
            PoolTableSurface surface = Surface;
            if (surface != null)
            {
                center = surface.transform.position;
                along = Flat(surface.transform.right);
                distance = surface.HalfLength + 1.2f;
            }

            GameObject root = GameObject.Find(SpawnRootName);
            if (root == null)
            {
                root = new GameObject(SpawnRootName);
                Undo.RegisterCreatedObjectUndo(root, "Points d'apparition");
            }

            if (list.arraySize < 2) list.arraySize = 2;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
                var point = new GameObject($"J{i + 1}");
                Undo.RegisterCreatedObjectUndo(point, "Point d'apparition");
                point.transform.SetParent(root.transform, false);
                Vector3 pos = center + along * (i % 2 == 0 ? -distance : distance);
                if (TryFindGround(pos, out RaycastHit ground)) pos.y = ground.point.y;
                else pos.y = 0f;
                Vector3 look = Flat(center - pos);
                point.transform.SetPositionAndRotation(pos, look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look) : Quaternion.identity);
                list.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
            }
            so.ApplyModifiedProperties();
        }

        // ---------- Powers ----------

        public static GameObject AddPowerPoint(Vector3 position, Transform under)
        {
            Transform parent = under;
            if (parent == null)
            {
                GameObject room = GameObject.Find(RoomPowerPointsName);
                if (room == null)
                {
                    room = new GameObject(RoomPowerPointsName);
                    Undo.RegisterCreatedObjectUndo(room, "Point de pouvoir");
                }
                parent = room.transform;
            }
            var point = new GameObject($"SpawnPoint_{parent.childCount}");
            Undo.RegisterCreatedObjectUndo(point, "Point de pouvoir");
            point.transform.SetParent(parent, false);
            point.transform.position = position;
            point.AddComponent<PoolPowerSpawnPoint>();
            return point;
        }

        // The table's own spawn point folder, when the click landed on the table.
        public static Transform TablePowerFolder(Transform hit)
        {
            PoolMatchRules rules = Rules;
            if (rules == null || hit == null) return null;
            GameObject table = TableRoot;
            if (table == null || !hit.IsChildOf(table.transform)) return null;
            PoolPowerSpawnPoint existing = rules.GetComponentInChildren<PoolPowerSpawnPoint>(true);
            if (existing != null) return existing.transform.parent;
            var folder = new GameObject("PowerSpawnPoints");
            Undo.RegisterCreatedObjectUndo(folder, "Point de pouvoir");
            folder.transform.SetParent(rules.transform, false);
            return folder.transform;
        }

        // ---------- New level ----------

        public static bool CreateLevel(string levelName, LevelMakerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(levelName)) return false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            string folder = settings.levelFolder.TrimEnd('/');
            EnsureFolder(folder);
            string path = $"{folder}/{levelName}.unity";
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Level Maker",
                    $"{path} existe déjà. Le remplacer ?", "Remplacer", "Annuler"))
                return false;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            if (settings.volumeProfile != null)
            {
                var volumeGo = new GameObject("Global Volume");
                Volume volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = settings.volumeProfile;
            }

            if (settings.addTemporaryFloor)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Sol provisoire";
                floor.transform.localScale = new Vector3(2f, 1f, 2f);
                floor.isStatic = true;
            }

            EnsureInputManager(settings);
            EnsureSpawnPoints(settings);

            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            return true;
        }

        public static bool InBuildSettings(string scenePath) =>
            EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath);

        public static void AddToBuildSettings(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || InBuildSettings(scenePath)) return;
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- Helpers ----------

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        public static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        // The world direction of an object's longest dimension.
        private static Vector3 LongestAxis(GameObject go)
        {
            if (!TryGetBounds(go, out Bounds b)) return go.transform.up;
            Vector3 s = b.size;
            if (s.x >= s.y && s.x >= s.z) return Vector3.right;
            return s.y >= s.z ? Vector3.up : Vector3.forward;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
