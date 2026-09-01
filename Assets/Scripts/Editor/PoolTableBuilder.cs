#if UNITY_EDITOR
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // One-click generator for a placeholder pool table + standard 8-ball rack,
    // so we can validate ball/cushion physics before building the aiming/shot
    // mechanic on top. Dimensions are approximate regulation values in meters,
    // editable on the PoolTableAssetSettings asset (Tools > Pool > Select
    // Custom Table Settings) — tune them there and re-run to iterate.
    public static class PoolTableBuilder
    {
        // Set once at the top of BuildTable() and read by every helper method
        // below it in the same call — dimensions now live as editable fields
        // on PoolTableAssetSettings (Tools > Pool > Select Custom Table
        // Settings) instead of hardcoded constants, so fitting a new table
        // model doesn't need a code edit.
        private static PoolTableAssetSettings settings;

        [MenuItem("Tools/Pool/Build Table (Online)")]
        public static void BuildTableOnline() => BuildTable(networked: true);

        [MenuItem("Tools/Pool/Build Table (Offline / Split-Screen)")]
        public static void BuildTableOffline() => BuildTable(networked: false);

        // For a custom table asset (a purchased/imported model) instead of our
        // placeholder cubes: builds the exact same invisible physics scaffold
        // (rail/surface colliders with the tuned PhysicsMaterials, pocket
        // triggers, rack, cues, PoolMatchRules) but skips the visible
        // surface/rail cubes — nested as a child under your table asset
        // instead of standing alone. The asset comes from
        // PoolTableAssetSettings.customTablePrefab (Tools > Pool > Select
        // Custom Table Settings to assign it) and gets instantiated
        // automatically; if that's not assigned, falls back to whatever
        // GameObject is currently selected in the Hierarchy.
        //
        // The generated "PoolPhysics" child is built at local (0,0,0)/identity
        // rotation, sized from PoolTableAssetSettings' dimension fields — it's
        // internally consistent (rack/rails/pockets/cues correctly proportioned
        // relative to each other) but has no idea where your asset's felt
        // actually is. Deliberately NOT auto-measured/auto-aligned from the
        // model (an earlier version tried guessing the pivot offset and long
        // axis from a placed guide object — too many ways for a guess like
        // that to be wrong on an arbitrary asset, and hard to debug blind).
        // Instead: after running this, select the generated "PoolPhysics"
        // object and Move/Rotate it (Scene view gizmo) until it lines up with
        // your table's felt — one direct, visual, undo-able step instead of
        // chasing measurements. Do NOT use Scale to resize it, non-uniform
        // scale distorts every SphereCollider under it (balls, pockets) into
        // ellipsoids — Unity spheres don't support non-uniform scale. If the
        // size is off, change Play Length/Play Width/Table Surface Y on
        // PoolTableAssetSettings instead (real-world meters) and regenerate;
        // PoolPhysics itself should always stay at scale (1,1,1).
        [MenuItem("Tools/Pool/Attach Physics To Custom Table (Online)")]
        public static void AttachPhysicsToCustomTableOnline() => BuildTable(networked: true, hideVisuals: true, parentToSelection: true);

        [MenuItem("Tools/Pool/Attach Physics To Custom Table (Offline / Split-Screen)")]
        public static void AttachPhysicsToCustomTableOffline() => BuildTable(networked: false, hideVisuals: true, parentToSelection: true);

        [MenuItem("Tools/Pool/Select Custom Table Settings")]
        public static void SelectCustomTableSettings() => Selection.activeObject = GetOrCreateTableAssetSettings();

        // Standalone from BuildTable() on purpose: "Attach Physics To Custom
        // Table" destroys and fully regenerates the whole "PoolPhysics"
        // child every time it runs (see the comment above it) — including
        // the 6 pockets, wiping out any manual alignment already done on an
        // existing table. Someone just wanting the Resources config assets
        // created (PoolPhysicsSettings/PoolScreenJuiceSettings/
        // PoolPotEffectSettings) shouldn't have to risk that. Safe to run
        // any time — each Ensure*Asset() is a no-op if its asset already
        // exists.
        [MenuItem("Tools/Pool/Ensure Config Assets Exist")]
        public static void EnsureConfigAssetsExist()
        {
            EnsurePhysicsSettingsAsset();
            EnsureScreenJuiceSettingsAsset();
            EnsurePotEffectSettingsAsset();
            Debug.Log("[PoolTableBuilder] Config assets ready in Assets/Resources (created any that were missing, left existing ones untouched).");
        }

        private static void BuildTable(bool networked, bool hideVisuals = false, bool parentToSelection = false)
        {
            settings = GetOrCreateTableAssetSettings();

            // Balls: near-elastic collisions between themselves (real pool balls have
            // a coefficient of restitution around 0.92-0.98), very low friction.
            // bounceCombine=Average, NOT Maximum: Unity resolves a contact's
            // combine mode by picking whichever of the two touching materials
            // declares the higher-priority mode (order: Average < Minimum <
            // Multiply < Maximum), then applies THAT mode using both
            // materials' values — regardless of which side it came from. With
            // Maximum here, ball-felt contacts were ALSO forced through
            // Maximum (nothing beats it), giving bounce = max(0.92, felt's
            // 0.05) = 0.92 — the ball bouncing off the flat felt itself like
            // it would off another ball, intermittently whenever it moved
            // fast enough to clear the bounce-velocity threshold. Average has
            // the lowest priority, so it never overrides what it's touching:
            // ball-ball stays 0.92 (both sides Average), ball-rail still gets
            // Rail's own Maximum (0.92, lively), ball-felt gets Felt's own
            // Minimum (0.05, correctly dead).
            PhysicsMaterial ballMaterial = GetOrCreatePhysicsMaterial("BallPhysics", friction: 0.03f, bounciness: 0.92f,
                PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Average);
            // Rails: bouncy. bounceCombine=Maximum guarantees a lively rebound
            // by itself (highest priority — wins regardless of what it's
            // touching), so the ball material no longer needs to also claim
            // Maximum for rail bounces to stay lively.
            PhysicsMaterial railMaterial = GetOrCreatePhysicsMaterial("RailPhysics", friction: 0.15f, bounciness: 0.85f,
                PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Maximum);
            // Felt: friction here mostly affects settling/contact behaviour — the
            // actual rolling/sliding deceleration is handled by PoolBall.cs, since
            // PhysX has no native rolling resistance.
            PhysicsMaterial feltMaterial = GetOrCreatePhysicsMaterial("FeltPhysics", friction: 0.35f, bounciness: 0.05f,
                PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Minimum);

            EnsurePhysicsSettingsAsset();
            EnsureScreenJuiceSettingsAsset();
            EnsurePotEffectSettingsAsset();

            GameObject root;
            if (parentToSelection)
            {
                GameObject tableInstance = InstantiateCustomTablePrefab() ?? Selection.activeGameObject;
                if (tableInstance == null)
                {
                    Debug.LogWarning("[PoolTableBuilder] No custom table prefab assigned (Tools > Pool > Select Custom Table Settings) and nothing selected in the Hierarchy — nothing to attach physics to.");
                    return;
                }

                // Re-running this after tweaking dimensions used to just add
                // ANOTHER "PoolPhysics" alongside the old one instead of
                // replacing it — easy to end up with several overlapping
                // Surface/rail colliders at slightly different heights,
                // which is exactly what makes a resting ball jitter/"bounce
                // on itself". Only ever keep the latest.
                Transform existing = tableInstance.transform.Find("PoolPhysics");
                if (existing != null)
                {
                    Debug.Log("[PoolTableBuilder] Removing a previous 'PoolPhysics' child before rebuilding — re-running this used to leave old ones behind, stacking up overlapping colliders.");
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }

                root = new GameObject("PoolPhysics");
                root.transform.SetParent(tableInstance.transform);
                // Starts at the asset's own pivot with no rotation — see the
                // menu item's comment above for why this isn't auto-aligned,
                // and adjust this object's transform by hand afterward.
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
            }
            else
            {
                root = new GameObject(networked ? "PoolTable" : "PoolTable (Offline)");
            }
            Undo.RegisterCreatedObjectUndo(root, "Build Pool Table");

            BuildSurface(root.transform, feltMaterial, hideVisuals);
            BuildRails(root.transform, railMaterial, hideVisuals);
            BuildPockets(root.transform);
            RackBalls(root.transform, ballMaterial);
            // Two cues, not one — with split-screen (or a second player just
            // joining online), each player needs their own to pick up instead
            // of having to fight over/wait for a single shared one.
            CreateCue(root.transform, networked, "Cue_P1", new Vector3(settings.playLength / 2f + 0.2f, settings.tableSurfaceY + 0.1f, -0.2f));
            CreateCue(root.transform, networked, "Cue_P2", new Vector3(settings.playLength / 2f + 0.2f, settings.tableSurfaceY + 0.1f, 0.2f));
            root.AddComponent<PoolMatchRules>();

            Selection.activeGameObject = root;
        }

        private static void CreateCue(Transform parent, bool networked, string name, Vector3 localPosition)
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

            if (networked)
            {
                cue.AddComponent<NetworkObject>();

                NetworkTransform networkTransform = cue.AddComponent<NetworkTransform>();
                SerializedObject transformSo = new SerializedObject(networkTransform);
                transformSo.FindProperty("AuthorityMode").enumValueIndex = 1; // Owner
                transformSo.ApplyModifiedPropertiesWithoutUndo();

                cue.AddComponent<Grabbable>();
            }
            else
            {
                cue.AddComponent<LocalGrabbable>();
            }

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

        // Keeps the collider (for physics) but removes the rendering — used
        // when a custom table asset already provides the visuals, so we don't
        // end up with a duplicate placeholder cube floating over/under it.
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

                // No per-pocket wiring needed anymore — halo/aura tuning
                // (including the rising aura prefab reference) lives on the
                // shared PoolPotEffectSettings asset, see
                // EnsurePotEffectSettingsAsset().
                pocket.AddComponent<PoolPocket>();
            }
        }

        // Standard tournament rack, row-major from the apex (closest to the head
        // spot / cue ball) to the back row: 1-ball at the apex, 8-ball dead
        // center, one solid and one stripe in the two back corners. The
        // previous version placed balls via a running counter that skipped
        // incrementing at the 8-ball's slot — it never actually reached 15,
        // and reused the value 8 a second time on a later slot. Hardcoded here
        // instead of derived, since the only real constraints (apex/center/
        // corners) leave everything else free — this exact layout matches
        // what's normally shown as "the" standard rack.
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
            rackParent.transform.SetParent(parent);

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
            // Solids (1-7) and stripes (9-15) pair up by color like a real set
            // (1&9 yellow, 2&10 blue, ...) — number 8 is handled separately by
            // the caller (black), so it never reaches this palette indexing.
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

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                mainTexture = PoolBallTextureGenerator.GetOrCreate(name, number, color, isCueBall),
            };
            EnsureFolder("Assets/Materials/Balls");
            AssetDatabase.CreateAsset(mat, $"Assets/Materials/Balls/{name}.mat");
            ball.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // Instantiates PoolTableAssetSettings.customTablePrefab, if assigned,
        // as a real prefab instance (keeping its blue prefab link, unlike
        // Object.Instantiate) — returns null if nothing's assigned there, so
        // the caller can fall back to the current Hierarchy selection.
        private static GameObject InstantiateCustomTablePrefab()
        {
            GameObject prefab = GetOrCreateTableAssetSettings().customTablePrefab;
            if (prefab == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Custom Pool Table");
            return instance;
        }

        // Created once and left alone afterwards — unlike the PhysicsMaterials
        // below, this asset is meant to be hand-tuned directly in the
        // Inspector (prefab reference + dimensions), so re-running the
        // builder must never overwrite whatever values are already saved on it.
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

        private static void EnsurePotEffectSettingsAsset()
        {
            EnsureFolder("Assets/Resources");
            string path = "Assets/Resources/PoolPotEffectSettings.asset";

            if (AssetDatabase.LoadAssetAtPath<PoolPotEffectSettings>(path) != null) return;

            PoolPotEffectSettings settings = ScriptableObject.CreateInstance<PoolPotEffectSettings>();

            const string auraPath = "Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab";
            settings.risingAuraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(auraPath);

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

            // Always (re)apply the tuning, even on an existing asset — otherwise
            // re-running the builder after adjusting these values silently keeps
            // whatever was saved the first time this material was created.
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
