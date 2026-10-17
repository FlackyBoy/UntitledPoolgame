#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Core;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.PoolEditor
{
    // Movement and combat R&D tools:
    // - Tools > Pool > R&D > Préparer le joueur de R&D: adds what the R&D
    //   player variant needs (locomotion, boxing, hit gauge, animation
    //   layers). Safe to run again.
    // - Tools > Pool > R&D > Créer l'arène de test: a sandbox scene (ledges,
    //   ramp, steps, crates, two dummies, a PlayerInputManager). No table, no
    //   match. Shared scenes are left alone.
    public static class RnDArenaTools
    {
        private const string Folder = "Assets/Scenes/Sandbox/RnD";
        private const string ScenePath = Folder + "/ArenaRnD.unity";
        internal const string VariantPath = "Assets/Prefabs/PlayerLocal/New_PlayerVariant.prefab";
        private const string DummyPath = "Assets/Prefabs/PlayerLocal/Dummy.prefab";

        [MenuItem("Tools/Pool/R&D/Préparer le joueur de R&D", priority = 60)]
        private static void PreparePlayerMenu()
        {
            if (PreparePlayer())
                EditorUtility.DisplayDialog("Joueur de R&D", "Joueur de R&D prêt : composants et calques d'animation (détail dans la console).", "OK");
        }

        // Components on the variant, then the animation layers.
        private static bool PreparePlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath) == null)
            {
                EditorUtility.DisplayDialog("Joueur de R&D", $"Variante du joueur introuvable : {VariantPath}.", "OK");
                return false;
            }
            GameFlowTools.EnsureAssets();
            EnsureComponentsOnVariant();
            return RnDCombatAnimationTools.BuildLayers();
        }

        [MenuItem("Tools/Pool/R&D/Créer l'arène de test", priority = 61)]
        private static void CreateArena()
        {
            GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            if (variant == null)
            {
                EditorUtility.DisplayDialog("Arène de test", $"Variante du joueur introuvable : {VariantPath}.", "OK");
                return;
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Arène de test",
                    "L'arène existe déjà. La recréer (ce qui a été changé dedans sera perdu) ?", "Recréer", "Annuler"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            PreparePlayer();
            EnsureFolder(Folder);
            EnsureFolder(Folder + "/Materials");
            Material floor = Mat("Sol", new Color(0.42f, 0.44f, 0.47f));
            Material ledge = Mat("Rebord", new Color(0.93f, 0.55f, 0.2f));
            Material ramp = Mat("Rampe", new Color(0.24f, 0.55f, 0.88f));
            Material crate = Mat("Caisse", new Color(0.62f, 0.42f, 0.24f));
            Material tooHigh = Mat("Trop haut", new Color(0.8f, 0.25f, 0.25f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // The players bring their own cameras.
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.GetComponent<Camera>() != null) Object.DestroyImmediate(go);
            RenderSettings.ambientMode = AmbientMode.Trilight;

            Box("Sol", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), floor);
            // Ledges of growing height along +X: the last one is above the
            // reach (Ledge Max Height, 2.1 m by default).
            float[] heights = { 0.5f, 0.9f, 1.3f, 1.7f, 2.1f, 2.6f };
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                Box($"Rebord {h:0.0} m", new Vector3(-7.5f + i * 3f, h / 2f, 6f), new Vector3(2.2f, h, 2.2f), h > 2.11f ? tooHigh : ledge);
            }
            // A long wall to climb onto and run along.
            Box("Mur 1,5 m", new Vector3(0f, 0.75f, 11f), new Vector3(18f, 1.5f, 3f), ledge);

            GameObject r = Box("Rampe", new Vector3(-10f, 0.9f, -4f), new Vector3(3f, 0.3f, 8f), ramp);
            r.transform.rotation = Quaternion.Euler(-14f, 0f, 0f);
            for (int i = 0; i < 6; i++)
                Box($"Marche {i + 1}", new Vector3(10f, 0.15f + i * 0.15f, -6f + i * 0.5f), new Vector3(3f, 0.3f + i * 0.3f, 0.5f), ramp);

            for (int i = 0; i < 6; i++)
            {
                GameObject c = Box($"Caisse {i + 1}", new Vector3(-3f + (i % 3) * 1.1f, 0.35f + (i / 3) * 0.72f, -8f), Vector3.one * 0.7f, crate);
                Rigidbody rb = c.AddComponent<Rigidbody>();
                rb.mass = 6f;
            }

            GameObject dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);
            if (dummy != null)
                for (int i = 0; i < 2; i++)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(dummy);
                    d.name = $"Mannequin {i + 1}";
                    d.transform.SetPositionAndRotation(new Vector3(2f + i * 2.5f, 0f, -2f), Quaternion.Euler(0f, 180f, 0f));
                    // Left as is, its PlayerInput would join as a player and
                    // take the keyboard, and its camera draw over the players.
                    RagdollDummySpawner.MakeInert(d);
                }

            // Players: the R&D variant, joining with a button, split screen.
            var managerGo = new GameObject("PlayerInputManager");
            var manager = managerGo.AddComponent<PlayerInputManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_PlayerPrefab").objectReferenceValue = variant;
            so.FindProperty("m_SplitScreen").boolValue = true;
            so.FindProperty("m_MaxPlayerCount").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            var spawner = managerGo.AddComponent<ScenePlayerSpawner>();
            var spawnSo = new SerializedObject(spawner);
            SerializedProperty points = spawnSo.FindProperty("spawnPoints");
            points.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var p = new GameObject($"Départ J{i + 1}").transform;
                p.SetPositionAndRotation(new Vector3(-1.5f + i * 3f, 0.05f, -4f), Quaternion.identity);
                points.GetArrayElementAtIndex(i).objectReferenceValue = p;
            }
            spawnSo.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("Triche C+P (2e joueur)").AddComponent<SplitScreenCheatSpawner>();
            var dummySpawner = new GameObject("Mannequin sur G").AddComponent<RagdollDummySpawner>();
            var dummySo = new SerializedObject(dummySpawner);
            SerializedProperty dummyField = dummySo.FindProperty("dummyPrefab");
            if (dummyField != null && dummy != null) { dummyField.objectReferenceValue = dummy; dummySo.ApplyModifiedPropertiesWithoutUndo(); }

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[R&D] Arène créée : {ScenePath}. Play, puis un bouton (clavier ou manette) pour faire entrer J1 ; C+P pour un 2e joueur.");
        }

        // The R&D player gets the new movement, the boxing punches and the
        // hit gauge; the normal player doesn't.
        private static void EnsureComponentsOnVariant()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(VariantPath);
            try
            {
                bool changed = false;
                if (root.GetComponent<LocalPlayerLocomotion>() == null)
                {
                    root.AddComponent<LocalPlayerLocomotion>();
                    changed = true;
                    Debug.Log("[R&D] LocalPlayerLocomotion ajouté à la variante du joueur.");
                }
                // Also added at runtime on the first blow; on the prefab so
                // its Debug Logs box can be set.
                if (root.GetComponent<UntitledPoolGame.Interaction.LocalHitGauge>() == null)
                {
                    root.AddComponent<UntitledPoolGame.Interaction.LocalHitGauge>();
                    changed = true;
                    Debug.Log("[R&D] LocalHitGauge (jauge d'encaissement) ajouté à la variante du joueur.");
                }
                var melee = root.GetComponent<UntitledPoolGame.Interaction.LocalUnarmedMelee>();
                if (melee != null)
                {
                    var so = new SerializedObject(melee);
                    SerializedProperty flag = so.FindProperty("boxingPunches");
                    if (flag != null && !flag.boolValue)
                    {
                        flag.boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                        Debug.Log("[R&D] Poings de boxe activés sur la variante du joueur.");
                    }
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, VariantPath);
                else Debug.Log("[R&D] La variante du joueur a déjà la R&D activée.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = go.GetComponent<Rigidbody>() == null && !name.StartsWith("Caisse");
            return go;
        }

        // Saved as assets: a material made in memory isn't kept in the scene.
        private static Material Mat(string name, Color color)
        {
            string path = $"{Folder}/Materials/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
