#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // Builds a placeholder pool table with a standard rack, or the physics
    // under an imported table model. Sizes are in PoolTableAssetSettings
    // (Tools > Pool > Select Custom Table Settings).
    public static class PoolTableBuilder
    {
        // Set at the top of each build, read by the helpers below.
        private static PoolTableAssetSettings settings;

        // Tools > Pool is grouped by priority (a gap over 10 adds a
        // separator): 0-2 editors, 20-22 project setup, 40-43 table, 60+ R&D.
        [MenuItem("Tools/Pool/Build Table", priority = 40)]
        public static void BuildTableMenu() => BuildTable();

        // For an imported table model: builds the same invisible physics
        // (rails, felt, pockets, rack, cues, rules) without the placeholder
        // cubes, as a "PoolPhysics" child of the model. The model is
        // customTablePrefab, or the selected object if none is set.
        //
        // PoolPhysics isn't aligned automatically (guessing it on an arbitrary
        // model went wrong too often): move and rotate it by hand onto the
        // felt. Never scale it, non-uniform scale turns the sphere colliders
        // into ellipsoids; change the sizes in the settings and rebuild.
        [MenuItem("Tools/Pool/Attach Physics To Custom Table", priority = 41)]
        public static void AttachPhysicsToCustomTable() => BuildTable(hideVisuals: true, parentToSelection: true);

        [MenuItem("Tools/Pool/Select Custom Table Settings", priority = 42)]
        public static void SelectCustomTableSettings() => Selection.activeObject = GetOrCreateTableAssetSettings();

        // Separate from the table builds, which regenerate PoolPhysics and
        // would lose its alignment. Safe to run any time.
        [MenuItem("Tools/Pool/Ensure Config Assets Exist", priority = 20)]
        public static void EnsureConfigAssetsExist()
        {
            EnsurePhysicsSettingsAsset();
            EnsureScreenJuiceSettingsAsset();
            EnsurePotEffectSettingsAsset();
            EnsurePowerSpawnSettingsAsset();
            EnsureSpeedPoolSettingsAsset();
            EnsureModeSettingsAsset();
            EnsureBallBlastPowerAsset();
            EnsureMagnetPowerAsset();
            EnsureIdenticalGroupsPowerAsset();
            EnsureTableHolePowerAsset();
            EnsureCatchTheHoleSettingsAsset();
            EnsureHungryHoleSettingsAsset();
            EnsureSnakePoolSettingsAsset();
            EnsurePowerWashPoolSettingsAsset();
            EnsurePowerWashPowerAssets();
            // The powers' tokens on the table, for those that have none yet.
            PowerTokenGenerator.Generate(onlyMissing: true);
            EnsureHudSettingsAsset();
            GameFlowTools.EnsureAssets();
            Debug.Log("[PoolTableBuilder] Config assets ready in Assets/Resources (created any that were missing, left existing ones untouched).");
        }

        // Texts, colours and timings of the in-game HUD (MatchHud).
        private static void EnsureHudSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/HudSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<UntitledPoolGame.Core.HudSettings>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UntitledPoolGame.Core.HudSettings>(), path);
        }

        // "Bille destructrice", with the CFXR2 WW Explosion, added to the
        // powers handed out if missing.
        private static void EnsureBallBlastPowerAsset()
        {
            EnsurePowerAsset<BallBlastPower>("BallBlastPower", "Bille destructrice", serialized =>
            {
                const string explosionPath = "Assets/Plugins/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR2 WW Explosion.prefab";
                serialized.FindProperty("explosionPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(explosionPath);
            });
        }

        // "Trou": an attack usable during the opponent's turn, with CFXR fire
        // for the meteor and impact and smoke for the hole.
        private static void EnsureTableHolePowerAsset()
        {
            EnsurePowerAsset<TableHolePower>("TableHolePower", "Trou", serialized =>
            {
                const string cfxr = "Assets/Plugins/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/";
                serialized.FindProperty("requiresOwnTurn").boolValue = false;
                serialized.FindProperty("meteorPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(cfxr + "Fire/CFXR Fire.prefab");
                serialized.FindProperty("impactPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(cfxr + "Explosions/CFXR3 Fire Explosion B.prefab");
                serialized.FindProperty("smokePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(cfxr + "Misc/CFXR Smoke Source 3D.prefab");
            });
            // The scorch decal, also for an asset created before it existed.
            TableHoleScorchGenerator.Ensure("Assets/Powers/TableHolePower.asset");
        }

        // Catch the Hole's living holes, looking like the Trou power's hole.
        private static void EnsureCatchTheHoleSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/CatchTheHoleSettings.asset";
            CatchTheHoleSettings settings = AssetDatabase.LoadAssetAtPath<CatchTheHoleSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CatchTheHoleSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            if (settings.look != null) return;
            settings.look = AssetDatabase.LoadAssetAtPath<TableHolePower>("Assets/Powers/TableHolePower.asset");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Hungry Hole: the cue-ball holes and the balls' sizes.
        private static void EnsureHungryHoleSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/HungryHoleSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<HungryHoleSettings>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<HungryHoleSettings>(), path);
        }

        // Snake Pool: the chains behind the cue balls.
        private static void EnsureSnakePoolSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/SnakePoolSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<SnakePoolSettings>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SnakePoolSettings>(), path);
        }

        // Power Wash Pool: the grime on the felt and the match clock, with
        // Cartoon FX Remaster's water splash at the pocket (also given to an
        // existing asset that has none).
        private static void EnsurePowerWashPoolSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/PowerWashPoolSettings.asset";
            PowerWashPoolSettings settings = AssetDatabase.LoadAssetAtPath<PowerWashPoolSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PowerWashPoolSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            if (settings.splashVfx != null) return;
            const string vfxPath = "Assets/Plugins/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR Water Splash (Smaller).prefab";
            settings.splashVfx = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
            EditorUtility.SetDirty(settings);
        }

        // Power Wash Pool's own powers, created for that mode only (Only In
        // Modes = PowerWashPool; the user can widen it). Imperméable is a
        // defence: usable off-turn.
        private static void EnsurePowerWashPowerAssets()
        {
            EnsurePowerAsset<MudBucketPower>("MudBucketPower", "Seau de boue", OnlyInPowerWash);
            EnsurePowerAsset<WaterBombPower>("WaterBombPower", "Bombe à eau", OnlyInPowerWash);
            EnsurePowerAsset<BigRollerPower>("BigRollerPower", "Gros rouleau", OnlyInPowerWash);
            EnsurePowerAsset<WaterproofPower>("WaterproofPower", "Imperméable", serialized =>
            {
                OnlyInPowerWash(serialized);
                serialized.FindProperty("requiresOwnTurn").boolValue = false;
            });
            EnsurePowerAsset<MudFlipPower>("MudFlipPower", "Boue retournée", OnlyInPowerWash);
            EnsurePowerAsset<RoombaPower>("RoombaPower", "Roomba", OnlyInPowerWash);
        }

        private static void OnlyInPowerWash(SerializedObject serialized)
        {
            SerializedProperty modes = serialized.FindProperty("onlyInModes");
            modes.arraySize = 1;
            modes.GetArrayElementAtIndex(0).intValue = (int)PoolGameMode.PowerWashPool;
        }

        // The "Aimant" power asset (MagnetPower), with Cartoon FX Remaster's
        // looping "CFXR Electrified 3" as its in-pocket VFX.
        private static void EnsureMagnetPowerAsset()
        {
            EnsurePowerAsset<MagnetPower>("MagnetPower", "Aimant", serialized =>
            {
                const string vfxPath = "Assets/Plugins/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR Electrified 3.prefab";
                serialized.FindProperty("pocketVfxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
            });
        }

        // Creates Assets/Powers/<fileName>.asset when missing and adds it to
        // the powers handed out.
        private static void EnsurePowerAsset<T>(string fileName, string powerName, System.Action<SerializedObject> configure) where T : PoolPower
        {
            EnsureFolder("Assets/Powers");
            string path = $"Assets/Powers/{fileName}.asset";
            T power = AssetDatabase.LoadAssetAtPath<T>(path);
            if (power == null)
            {
                power = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(power, path);
                var serialized = new SerializedObject(power);
                serialized.FindProperty("powerName").stringValue = powerName;
                configure?.Invoke(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[PoolTableBuilder] Created {path}.");
            }

            PoolPowerSpawnSettings spawn = AssetDatabase.LoadAssetAtPath<PoolPowerSpawnSettings>("Assets/Resources/PoolPowerSpawnSettings.asset");
            if (spawn == null) return;
            var list = new System.Collections.Generic.List<PoolPower>(spawn.availablePowers ?? new PoolPower[0]);
            if (list.Contains(power)) return;
            list.Add(power);
            spawn.availablePowers = list.ToArray();
            EditorUtility.SetDirty(spawn);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PoolTableBuilder] {powerName} added to PoolPowerSpawnSettings > Available Powers.");
        }

        // "Groupe identique": an attack, so Requires Own Turn is off.
        private static void EnsureIdenticalGroupsPowerAsset()
        {
            EnsurePowerAsset<IdenticalGroupsPower>("IdenticalGroupsPower", "Groupe identique",
                serialized => serialized.FindProperty("requiresOwnTurn").boolValue = false);
        }

        // Adds the power spawners to a table built before they existed,
        // without rebuilding it (that would lose its alignment). Select the
        // table's root first ("PoolPhysics" or "PoolTable").
        [MenuItem("Tools/Pool/Add Power Spawn System To Selected Table", priority = 43)]
        public static void AddPowerSpawnSystemToSelectedTable()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[PoolTableBuilder] Nothing selected — select the table's root GameObject first (\"PoolPhysics\" for a custom table, \"PoolTable\"/\"PoolTable (Offline)\" for a generated one).");
                return;
            }
            AddPowerSpawnSystemTo(root);
        }

        // Same as the menu item, on a given table root (used by the Level Maker).
        public static void AddPowerSpawnSystemTo(GameObject root)
        {
            settings = GetOrCreateTableAssetSettings();
            EnsurePowerSpawnSettingsAsset();

            if (root.GetComponentInChildren<PoolPowerSpawnPoint>() == null)
                BuildPowerSpawnPoints(root.transform);
            else
                Debug.Log("[PoolTableBuilder] Skipped generating spawn points — this table already has some.");

            if (root.GetComponent<PoolPowerCrateManager>() == null)
                Undo.AddComponent<PoolPowerCrateManager>(root);
            if (root.GetComponent<PoolPowerBallRotator>() == null)
                Undo.AddComponent<PoolPowerBallRotator>(root);

            int addedTo = 0;
            foreach (PoolBall ball in root.GetComponentsInChildren<PoolBall>(true))
            {
                if (ball.IsCueBall || ball.GetComponent<PowerBall>() != null) continue;
                Undo.AddComponent<PowerBall>(ball.gameObject);
                addedTo++;
            }

            Debug.Log($"[PoolTableBuilder] Power spawn system added — PowerBall added to {addedTo} ball(s) that didn't already have one.");
        }

        // Level Maker: physics under a table model already in the scene. No
        // cues by default: the Level Maker places the Cue prefab.
        public static GameObject AttachPhysicsTo(GameObject table, bool createCues)
            => BuildTable(hideVisuals: true, parentToSelection: true, target: table, createCues: createCues);

        public static PoolTableAssetSettings TableAssetSettings => GetOrCreateTableAssetSettings();

        private static GameObject BuildTable(bool hideVisuals = false, bool parentToSelection = false,
            GameObject target = null, bool createCues = true)
        {
            settings = GetOrCreateTableAssetSettings();

            // Balls: nearly elastic (0.92), very low friction. Bounce combine
            // Average because Unity uses the higher-priority mode of the two
            // materials: with Maximum, ball-on-felt contacts bounced at 0.92
            // too. Average never wins, so rails (Maximum) and felt (Minimum)
            // keep their own.
            PhysicsMaterial ballMaterial = GetOrCreatePhysicsMaterial("BallPhysics", friction: 0.03f, bounciness: 0.92f,
                PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Average);
            // Rails: Maximum wins over the balls' Average, so rebounds stay lively.
            PhysicsMaterial railMaterial = GetOrCreatePhysicsMaterial("RailPhysics", friction: 0.15f, bounciness: 0.85f,
                PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Maximum);
            // Felt: rolling and sliding slow-down is done in PoolBall, PhysX has
            // no rolling resistance.
            PhysicsMaterial feltMaterial = GetOrCreatePhysicsMaterial("FeltPhysics", friction: 0.35f, bounciness: 0.05f,
                PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Minimum);

            EnsurePhysicsSettingsAsset();
            EnsureScreenJuiceSettingsAsset();
            EnsurePotEffectSettingsAsset();
            EnsurePowerSpawnSettingsAsset();

            GameObject root;
            if (parentToSelection)
            {
                GameObject tableInstance = target != null ? target : InstantiateCustomTablePrefab() ?? Selection.activeGameObject;
                if (tableInstance == null)
                {
                    Debug.LogWarning("[PoolTableBuilder] No custom table prefab assigned (Tools > Pool > Select Custom Table Settings) and nothing selected in the Hierarchy — nothing to attach physics to.");
                    return null;
                }

                // Replace the old one: two sets of colliders at slightly
                // different heights make resting balls jitter.
                Transform existing = tableInstance.transform.Find("PoolPhysics");
                if (existing != null)
                {
                    Debug.Log("[PoolTableBuilder] Removing a previous 'PoolPhysics' child before rebuilding — re-running this used to leave old ones behind, stacking up overlapping colliders.");
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }

                root = new GameObject("PoolPhysics");
                root.transform.SetParent(tableInstance.transform);
                // At the model's pivot, to be aligned by hand.
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
            }
            else
            {
                root = new GameObject("PoolTable");
            }
            Undo.RegisterCreatedObjectUndo(root, "Build Pool Table");

            BuildSurface(root.transform, feltMaterial, hideVisuals);
            BuildRails(root.transform, railMaterial, hideVisuals);
            BuildPockets(root.transform);
            RackBalls(root.transform, ballMaterial);
            // Two cues, not one — each player needs their own to pick up
            // instead of having to fight over/wait for a single shared one.
            if (createCues)
            {
                CreateCue(root.transform, "Cue_P1", new Vector3(settings.playLength / 2f + 0.2f, settings.tableSurfaceY + 0.1f, -0.2f));
                CreateCue(root.transform, "Cue_P2", new Vector3(settings.playLength / 2f + 0.2f, settings.tableSurfaceY + 0.1f, 0.2f));
            }
            root.AddComponent<PoolMatchRules>();
            BuildPowerSpawnPoints(root.transform);
            root.AddComponent<PoolPowerCrateManager>();
            root.AddComponent<PoolPowerBallRotator>();

            Selection.activeGameObject = root;
            return root;
        }

        // A grid with some random jitter per point: a plain grid looked too
        // regular, pure random clusters or lands on a rail.
        // Parented with worldPositionStays false so the points sit relative
        // to the table even when it was moved away from the origin.
        private static void BuildPowerSpawnPoints(Transform parent)
        {
            GameObject spawnPointsParent = new GameObject("PowerSpawnPoints");
            spawnPointsParent.transform.SetParent(parent, worldPositionStays: false);
            spawnPointsParent.transform.localPosition = Vector3.zero;
            spawnPointsParent.transform.localRotation = Quaternion.identity;

            const int columns = 4;
            const int rows = 2;
            float marginX = settings.playLength * 0.15f;
            float marginZ = settings.playWidth * 0.25f;
            float y = settings.tableSurfaceY + 0.02f;

            // Jitter stays within a fraction of a cell, so points can't overlap.
            float cellSpacingX = (settings.playLength - 2f * marginX) / (columns - 1);
            float cellSpacingZ = rows > 1 ? (settings.playWidth - 2f * marginZ) / (rows - 1) : 0f;
            float jitterX = cellSpacingX * 0.35f;
            float jitterZ = cellSpacingZ * 0.35f;

            for (int row = 0; row < rows; row++)
            {
                float z = Mathf.Lerp(-settings.playWidth / 2f + marginZ, settings.playWidth / 2f - marginZ, rows == 1 ? 0.5f : row / (float)(rows - 1));
                for (int col = 0; col < columns; col++)
                {
                    float x = Mathf.Lerp(-settings.playLength / 2f + marginX, settings.playLength / 2f - marginX, col / (float)(columns - 1));
                    x += Random.Range(-jitterX, jitterX);
                    z += Random.Range(-jitterZ, jitterZ);

                    GameObject point = new GameObject($"SpawnPoint_{row}_{col}");
                    point.transform.SetParent(spawnPointsParent.transform, worldPositionStays: false);
                    point.transform.localPosition = new Vector3(x, y, z);
                    point.AddComponent<PoolPowerSpawnPoint>();
                }
            }
        }

        private static void CreateCue(Transform parent, string name, Vector3 localPosition)
        {
            GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cue.name = name;
            cue.transform.SetParent(parent);
            // Resting near the end of the table, slightly above the surface —
            // it's a real Rigidbody, so it settles naturally under gravity.
            cue.transform.localPosition = localPosition;
            cue.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // lying flat, long axis along X
            cue.transform.localScale = new Vector3(0.016f, 0.7f, 0.016f); // ~1.4m long, 16mm diameter

            Rigidbody rb = cue.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // guards against tunneling through the floor on drop

            cue.AddComponent<LocalGrabbable>();
            cue.AddComponent<Cue>();
        }

        private static void BuildSurface(Transform parent, PhysicsMaterial material, bool hideVisuals = false)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Surface";
            surface.transform.SetParent(parent);
            surface.transform.localPosition = new Vector3(0f, settings.tableSurfaceY - settings.surfaceThickness / 2f, 0f);
            surface.transform.localScale = new Vector3(settings.playLength, settings.surfaceThickness, settings.playWidth);
            surface.GetComponent<BoxCollider>().sharedMaterial = material;
            surface.AddComponent<PoolTableSurface>().Configure(settings.playLength / 2f, settings.playWidth / 2f);
            if (hideVisuals) StripVisuals(surface);
        }

        private static void BuildRails(Transform parent, PhysicsMaterial material, bool hideVisuals = false)
        {
            float longSegmentLength = settings.playLength / 2f - 2f * settings.pocketRadius;
            float shortSegmentLength = settings.playWidth - 2f * settings.pocketRadius;

            // Long rails (front/back), each split in two by the middle (side) pockets
            CreateRail(parent, material, "Rail_Front_Left", -settings.playLength / 4f, settings.playWidth / 2f + settings.railThickness / 2f, longSegmentLength, settings.railThickness, hideVisuals);
            CreateRail(parent, material, "Rail_Front_Right", settings.playLength / 4f, settings.playWidth / 2f + settings.railThickness / 2f, longSegmentLength, settings.railThickness, hideVisuals);
            CreateRail(parent, material, "Rail_Back_Left", -settings.playLength / 4f, -(settings.playWidth / 2f + settings.railThickness / 2f), longSegmentLength, settings.railThickness, hideVisuals);
            CreateRail(parent, material, "Rail_Back_Right", settings.playLength / 4f, -(settings.playWidth / 2f + settings.railThickness / 2f), longSegmentLength, settings.railThickness, hideVisuals);

            // Short rails (left/right ends), between the two corner pockets on each end
            CreateRail(parent, material, "Rail_Left", -(settings.playLength / 2f + settings.railThickness / 2f), 0f, settings.railThickness, shortSegmentLength, hideVisuals);
            CreateRail(parent, material, "Rail_Right", settings.playLength / 2f + settings.railThickness / 2f, 0f, settings.railThickness, shortSegmentLength, hideVisuals);
        }

        private static void CreateRail(Transform parent, PhysicsMaterial material, string name, float x, float z, float sizeX, float sizeZ, bool hideVisuals = false)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = name;
            rail.transform.SetParent(parent);
            rail.transform.localPosition = new Vector3(x, settings.tableSurfaceY + settings.railHeight / 2f, z);
            rail.transform.localScale = new Vector3(sizeX, settings.railHeight, sizeZ);
            rail.GetComponent<BoxCollider>().sharedMaterial = material;
            if (hideVisuals) StripVisuals(rail);
        }

        // Keeps the collider, removes the rendering (the model has the visuals).
        private static void StripVisuals(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        }

        private static void BuildPockets(Transform parent)
        {
            Vector3[] positions =
            {
                new Vector3(-settings.playLength / 2f, settings.tableSurfaceY, -settings.playWidth / 2f),
                new Vector3(-settings.playLength / 2f, settings.tableSurfaceY, settings.playWidth / 2f),
                new Vector3(settings.playLength / 2f, settings.tableSurfaceY, -settings.playWidth / 2f),
                new Vector3(settings.playLength / 2f, settings.tableSurfaceY, settings.playWidth / 2f),
                new Vector3(0f, settings.tableSurfaceY, -settings.playWidth / 2f),
                new Vector3(0f, settings.tableSurfaceY, settings.playWidth / 2f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pocket = new GameObject($"Pocket_{i}");
                pocket.transform.SetParent(parent);
                pocket.transform.localPosition = positions[i];

                SphereCollider collider = pocket.AddComponent<SphereCollider>();
                collider.radius = settings.pocketRadius;
                collider.isTrigger = true;

                // Halo and aura settings are shared, in PoolPotEffectSettings.
                pocket.AddComponent<PoolPocket>();
            }
        }

        // Standard rack from the apex to the back row: 1 at the apex, 8 in the
        // centre, a solid and a stripe in the back corners.
        private static readonly int[] RackOrder =
        {
            1,
            15, 6,
            4, 8, 11,
            14, 2, 12, 7,
            5, 3, 13, 9, 10,
        };

        private static void RackBalls(Transform parent, PhysicsMaterial material)
        {
            GameObject rackParent = new GameObject("Balls");
            // Local origin of the table, whatever its current transform.
            rackParent.transform.SetParent(parent, worldPositionStays: false);
            rackParent.transform.localPosition = Vector3.zero;
            rackParent.transform.localRotation = Quaternion.identity;

            float footSpotX = settings.playLength / 4f;
            float rowSpacing = settings.ballDiameter * Mathf.Sqrt(3f) / 2f;
            float ballY = settings.tableSurfaceY + settings.BallRadius;

            int index = 0;
            for (int row = 0; row < 5; row++)
            {
                int ballsInRow = row + 1;
                float rowX = footSpotX + row * rowSpacing;
                float startZ = -(ballsInRow - 1) * settings.ballDiameter / 2f;

                for (int col = 0; col < ballsInRow; col++)
                {
                    int number = RackOrder[index++];
                    Vector3 pos = new Vector3(rowX, ballY, startZ + col * settings.ballDiameter);
                    string name = $"Ball_{number}";
                    Color color = number == 8 ? Color.black : BallColor(number);

                    CreateBall(rackParent.transform, name, pos, material, color, number: number);
                }
            }

            // Cue ball at the head spot, opposite end of the table from the rack.
            CreateBall(rackParent.transform, "CueBall", new Vector3(-footSpotX, ballY, 0f), material, Color.white, number: 0, isCueBall: true);
        }

        private static Color BallColor(int number)
        {
            Color[] palette =
            {
                Color.yellow, Color.blue, Color.red, new Color(0.5f, 0f, 0.5f),
                new Color(1f, 0.5f, 0f), Color.green, new Color(0.5f, 0f, 0f),
            };
            // 1 and 9 share a colour, 2 and 10... (the 8 is handled by the caller).
            int paletteIndex = number <= 8 ? number - 1 : number - 9;
            return palette[paletteIndex % palette.Length];
        }

        private static void CreateBall(Transform parent, string name, Vector3 localPosition, PhysicsMaterial material, Color color, int number, bool isCueBall = false)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = name;
            ball.transform.SetParent(parent);
            ball.transform.localPosition = localPosition;
            ball.transform.localScale = Vector3.one * settings.ballDiameter;
            // PhysicsLayerSetup keeps players and cues out of the balls by
            // this layer; a generated ball left on Default collided with them.
            int ballLayer = LayerMask.NameToLayer("Poolball");
            if (ballLayer >= 0) ball.layer = ballLayer;

            ball.GetComponent<SphereCollider>().sharedMaterial = material;

            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.17f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            // PoolBall.cs handles the actual felt friction/rolling deceleration —
            // linear damping would fight that model, so it's left at 0 here.
            rb.maxAngularVelocity = 50f; // default (7) clips a hard-hit ball's spin
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // thin rails + fast balls can tunnel otherwise
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            PoolBall poolBall = ball.AddComponent<PoolBall>();
            SerializedObject so = new SerializedObject(poolBall);
            so.FindProperty("number").intValue = number;
            if (isCueBall) so.FindProperty("isCueBall").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Any object ball can be picked by PoolPowerBallRotator.
            if (!isCueBall) ball.AddComponent<PowerBall>();

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                mainTexture = PoolBallTextureGenerator.GetOrCreate(name, number, color, isCueBall),
            };
            EnsureFolder("Assets/Materials/Balls");
            AssetDatabase.CreateAsset(mat, $"Assets/Materials/Balls/{name}.mat");
            ball.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // Instantiates customTablePrefab as a prefab instance; null if unset
        // (the caller then uses the selection).
        private static GameObject InstantiateCustomTablePrefab()
        {
            GameObject prefab = GetOrCreateTableAssetSettings().customTablePrefab;
            if (prefab == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Custom Pool Table");
            return instance;
        }

        // Created once, then tuned by hand: never overwritten.
        private static PoolTableAssetSettings GetOrCreateTableAssetSettings()
        {
            EnsureFolder("Assets/Editor");
            string path = "Assets/Editor/PoolTableAssetSettings.asset";

            PoolTableAssetSettings asset = AssetDatabase.LoadAssetAtPath<PoolTableAssetSettings>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<PoolTableAssetSettings>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsurePhysicsSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            string path = "Assets/Resources/PoolPhysicsSettings.asset";

            if (AssetDatabase.LoadAssetAtPath<PoolPhysicsSettings>(path) != null) return;

            PoolPhysicsSettings settings = ScriptableObject.CreateInstance<PoolPhysicsSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        private static void EnsureScreenJuiceSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            string path = "Assets/Resources/PoolScreenJuiceSettings.asset";

            if (AssetDatabase.LoadAssetAtPath<PoolScreenJuiceSettings>(path) != null) return;

            PoolScreenJuiceSettings settings = ScriptableObject.CreateInstance<PoolScreenJuiceSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        // Which types each mode is played in (PoolModeSettings.Defaults).
        private static void EnsureModeSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/PoolModeSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<PoolModeSettings>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PoolModeSettings>(), path);
        }

        private static void EnsureSpeedPoolSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            const string path = "Assets/Resources/SpeedPoolSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<SpeedPoolSettings>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SpeedPoolSettings>(), path);
        }

        private static void EnsurePotEffectSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            string path = "Assets/Resources/PoolPotEffectSettings.asset";

            if (AssetDatabase.LoadAssetAtPath<PoolPotEffectSettings>(path) != null) return;

            PoolPotEffectSettings settings = ScriptableObject.CreateInstance<PoolPotEffectSettings>();

            const string auraPath = "Assets/Plugins/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab";
            settings.risingAuraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(auraPath);

            AssetDatabase.CreateAsset(settings, path);
        }

        private static void EnsurePowerSpawnSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            string path = "Assets/Resources/PoolPowerSpawnSettings.asset";

            if (AssetDatabase.LoadAssetAtPath<PoolPowerSpawnSettings>(path) != null) return;

            PoolPowerSpawnSettings settings = ScriptableObject.CreateInstance<PoolPowerSpawnSettings>();

            // Starts with every PoolPower asset in the project (an empty list
            // spawns nothing).
            string[] guids = AssetDatabase.FindAssets("t:PoolPower");
            PoolPower[] powers = new PoolPower[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                powers[i] = AssetDatabase.LoadAssetAtPath<PoolPower>(AssetDatabase.GUIDToAssetPath(guids[i]));
            settings.availablePowers = powers;

            AssetDatabase.CreateAsset(settings, path);
        }

        private static PhysicsMaterial GetOrCreatePhysicsMaterial(string name, float friction, float bounciness,
            PhysicsMaterialCombine frictionCombine, PhysicsMaterialCombine bounceCombine)
        {
            EnsureFolder("Assets/Materials/Physics");
            string path = $"Assets/Materials/Physics/{name}.physicMaterial";

            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            bool isNew = material == null;
            if (isNew) material = new PhysicsMaterial(name);

            // Always reapplied, so changed values reach an existing material.
            material.dynamicFriction = friction;
            material.staticFriction = friction;
            material.bounciness = bounciness;
            material.frictionCombine = frictionCombine;
            material.bounceCombine = bounceCombine;

            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);

            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
