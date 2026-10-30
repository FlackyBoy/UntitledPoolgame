#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // Play Anywhere set-up in the open scene:
    // - Tools > Pool > Play Anywhere > Créer un trou: a PlayAnywhereHole on
    //   the ground under the Scene view's centre, with both tees (coasters)
    //   and the cup 3 m ahead (black disc and a flag). Move the pieces, set
    //   the par on the hole.
    // - Tools > Pool > Play Anywhere > Créer une zone hors limites: a trigger
    //   box (PlayAnywhereOutOfBounds) to stretch over what's off the course.
    // - Tools > Pool > Play Anywhere > Créer la caisse: the crate the
    //   players take their object from at each round (PlayAnywhereCrate).
    // - Tools > Pool > Play Anywhere > Générer les objets de la caisse: the
//   crate's prefabs (block, ramp, cone, gadgets), added to Props.
// - Tools > Pool > Play Anywhere > Surface de la sélection > ...: the
    //   selected décor objects roll and bounce like parquet, carpet...
    // Both undoable. The scene still needs a "Lancer sans le menu" set to
    // PlayAnywhere, or the mode picked in the menus.
    public static class PlayAnywhereTools
    {
        private const string MaterialFolder = "Assets/Materials/PlayAnywhere";

        [MenuItem("Tools/Pool/Play Anywhere/Créer un trou", priority = 50)]
        private static void CreateHole()
        {
            Vector3 ground = GroundUnderSceneView(out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // Numbered after the scene's other holes: played after them.
            int number = 1;
            foreach (PlayAnywhereHole other in Object.FindObjectsByType<PlayAnywhereHole>())
                number = Mathf.Max(number, other.Order + 1);
            var root = new GameObject($"Play Anywhere - Trou {number}");
            Undo.RegisterCreatedObjectUndo(root, "Créer un trou Play Anywhere");
            root.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(forward));
            PlayAnywhereHole hole = root.AddComponent<PlayAnywhereHole>();

            Transform tee1 = Disc("Depart J1", root.transform, ground - right * 0.15f, 0.05f, Colour("Tee_P1", new Color(0.25f, 0.5f, 1f)));
            Transform tee2 = Disc("Depart J2", root.transform, ground + right * 0.15f, 0.05f, Colour("Tee_P2", new Color(1f, 0.3f, 0.25f)));
            Transform cup = Disc("Trou", root.transform, ground + forward * 3f, 0.09f, Colour("Cup", new Color(0.02f, 0.02f, 0.02f)));
            Transform flag = Flag(cup);

            var so = new SerializedObject(hole);
            so.FindProperty("player1Tee").objectReferenceValue = tee1;
            so.FindProperty("player2Tee").objectReferenceValue = tee2;
            so.FindProperty("cup").objectReferenceValue = cup;
            so.FindProperty("flag").objectReferenceValue = flag;
            so.FindProperty("order").intValue = number;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            Debug.Log($"[PlayAnywhere] Trou {number} créé : tire les sphères des départs et du trou dans la Scene (elles se posent sur la surface sous la souris), ajoute des points au parcours avec « + », règle le par sur « {root.name} ». Les trous se jouent dans l'ordre de leur champ Order. Pour lancer le mode : composant « Lancer sans le menu », mode PlayAnywhere.", root);
        }

        [MenuItem("Tools/Pool/Play Anywhere/Créer la caisse", priority = 52)]
        private static void CreateCrate()
        {
            Vector3 ground = GroundUnderSceneView(out Vector3 forward);
            var root = new GameObject("Play Anywhere - Caisse");
            Undo.RegisterCreatedObjectUndo(root, "Créer la caisse Play Anywhere");
            root.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(forward));
            root.AddComponent<PlayAnywhereCrate>();

            // A plain low box the objects stand on; its top is the crate's
            // Top Height (0.45 m). Solid: it's part of the décor.
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Caisse";
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = new Vector3(0f, 0.225f, 0f);
            box.transform.localScale = new Vector3(2.3f, 0.45f, 0.8f);
            Material wood = Colour("Crate", new Color(0.55f, 0.36f, 0.2f));
            if (wood != null) box.GetComponent<Renderer>().sharedMaterial = wood;

            Selection.activeGameObject = root;
            Debug.Log("[PlayAnywhere] Caisse créée : à chaque manche, les objets de Props (PlayAnywhereSettings) y sont posés. Place-la à l'écart du parcours.", root);
        }

        [MenuItem("Tools/Pool/Play Anywhere/Créer une zone hors limites", priority = 51)]
        private static void CreateOutOfBounds()
        {
            Vector3 ground = GroundUnderSceneView(out Vector3 forward);
            var zone = new GameObject("Play Anywhere - Hors limites");
            Undo.RegisterCreatedObjectUndo(zone, "Créer une zone hors limites");
            zone.transform.SetPositionAndRotation(ground + Vector3.up * 0.5f, Quaternion.LookRotation(forward));
            zone.transform.localScale = new Vector3(2f, 1f, 2f);
            zone.AddComponent<BoxCollider>().isTrigger = true;
            zone.AddComponent<PlayAnywhereOutOfBounds>();
            Selection.activeGameObject = zone;
        }

        // Simple course pieces for the crate, made once in PropFolder: a
        // block, a ramp the ball can roll up, a cone, and the gadgets
        // (portal, pipe, fan, spring, glue and ice plates). Their colliders
        // follow their shape (Use Own Colliders). Ensure Config Assets Exist
        // adds them to Props.
        private const string PropFolder = "Assets/Prefabs/PlayAnywhere";

        [MenuItem("Tools/Pool/Play Anywhere/Générer les objets de la caisse", priority = 53)]
        private static void GeneratePropsMenu()
        {
            // Also makes the glue and ice surfaces first, and adds the
            // pieces to Props.
            PoolTableBuilder.EnsurePlayAnywhereSettingsAsset();
            AssetDatabase.SaveAssets();
            GameObject[] made = EnsurePropPrefabs();
            Selection.objects = made;
            Debug.Log($"[PlayAnywhere] Objets dans {PropFolder} : {made.Length}, ajoutés à Props de PlayAnywhereSettings.");
        }

        private static readonly string[] PropNames = { "Bloc", "Rampe", "Cone", "Portail", "Tuyau", "Ventilateur", "Ressort", "Glue", "Verglas", "Destructeur" };

        // A prefab made by an older version of this tool, rebuilt in place
        // (same file, so Props keeps pointing at it): the one-piece pipe.
        private static bool IsOutdated(GameObject prefab) =>
            prefab.TryGetComponent(out PlayAnywherePipe pipe) && (pipe.FirstPart == null || pipe.SecondPart == null);

        // Entering Play with a crate piece missing (new ones added to the
        // code, the menu never run): made then, and added to Props, so the
        // crate and the test menu (C + A) have them.
        [InitializeOnLoadMethod]
        private static void WatchPlayMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                foreach (string name in PropNames)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{name}.prefab");
                    if (prefab != null && !IsOutdated(prefab)) continue;
                    PoolTableBuilder.EnsurePlayAnywhereSettingsAsset();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[PlayAnywhere] Objets de la caisse manquants ou d'une ancienne version ({name}…) : créés dans {PropFolder} et ajoutés à Props.");
                    return;
                }
            };
        }

        // The prefabs, made when missing (existing ones are kept).
        internal static GameObject[] EnsurePropPrefabs()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PropFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "PlayAnywhere");
            return new[]
            {
                EnsurePropPrefab("Bloc", () => BoxMesh(new Vector3(0.5f, 0.25f, 0.5f)), new Color(1f, 0.55f, 0.1f), box: true),
                EnsurePropPrefab("Rampe", () => RampMesh(0.6f, 1f, 0.2f), new Color(0.2f, 0.6f, 1f), box: false),
                EnsurePropPrefab("Cone", () => ConeMesh(0.15f, 0.4f, 20), new Color(1f, 0.25f, 0.3f), box: false),
                EnsureBuiltPrefab("Portail", BuildPortal),
                EnsureBuiltPrefab("Tuyau", BuildPipe),
                EnsureBuiltPrefab("Ventilateur", BuildFan),
                EnsureBuiltPrefab("Ressort", BuildSpring),
                EnsureBuiltPrefab("Glue", (root, parts) => BuildPlate(root, parts, "Glue", 1f, new Color(0.7f, 0.85f, 0.1f))),
                EnsureBuiltPrefab("Verglas", (root, parts) => BuildPlate(root, parts, "Verglas", 1.2f, new Color(0.65f, 0.9f, 1f))),
                EnsureBuiltPrefab("Destructeur", BuildDestroyer),
            };
        }

        // A prefab of several parts (children with their own mesh and
        // colour), its meshes saved together in one asset. build adds the
        // parts, the colliders (on the root, where LocalGrabbable needs
        // them) and the gadget.
        private static GameObject EnsureBuiltPrefab(string name, System.Action<GameObject, PartMaker> build)
        {
            string path = $"{PropFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !IsOutdated(existing)) return existing;

            // Left by a run that didn't get to the prefab, or an old version.
            string meshPath = $"{PropFolder}/{name}_Mesh.asset";
            if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);

            var go = new GameObject(name);
            build(go, new PartMaker(go, meshPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private sealed class PartMaker
        {
            private readonly GameObject root;
            private readonly string meshPath;
            private bool saved;

            public PartMaker(GameObject root, string meshPath)
            {
                this.root = root;
                this.meshPath = meshPath;
            }

            // A child showing this mesh (saved once, even used twice).
            public Transform Add(string partName, Mesh mesh, Material material, Vector3 localPosition, Transform parent = null)
            {
                if (!AssetDatabase.Contains(mesh))
                {
                    mesh.name = partName;
                    if (!saved) AssetDatabase.CreateAsset(mesh, meshPath);
                    else AssetDatabase.AddObjectToAsset(mesh, meshPath);
                    saved = true;
                }
                var part = new GameObject(partName);
                part.transform.SetParent(parent != null ? parent : root.transform, false);
                part.transform.localPosition = localPosition;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = part.AddComponent<MeshRenderer>();
                if (material != null) r.sharedMaterial = material;
                return part.transform;
            }
        }

        private static void AddBox(GameObject root, Vector3 center, Vector3 size)
        {
            var c = root.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        // Two posts and a bar around a glowing veil; the ball goes through
        // the opening (PlayAnywherePortal's zone matches it).
        private static void BuildPortal(GameObject root, PartMaker parts)
        {
            Material frame = Colour("Prop_Portail", new Color(0.35f, 0.15f, 0.6f));
            Mesh post = BoxMesh(new Vector3(0.08f, 0.55f, 0.08f));
            parts.Add("Montant G", post, frame, new Vector3(-0.25f, 0f, 0f));
            parts.Add("Montant D", post, frame, new Vector3(0.25f, 0f, 0f));
            parts.Add("Linteau", BoxMesh(new Vector3(0.58f, 0.08f, 0.08f)), frame, new Vector3(0f, 0.55f, 0f));
            Transform veil = parts.Add("Voile", BoxMesh(new Vector3(0.42f, 0.55f, 0.01f)), GlowColour("Prop_Portail_Voile", new Color(0.75f, 0.3f, 1f, 0.45f)), Vector3.zero);
            veil.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AddBox(root, new Vector3(-0.25f, 0.275f, 0f), new Vector3(0.08f, 0.55f, 0.08f));
            AddBox(root, new Vector3(0.25f, 0.275f, 0f), new Vector3(0.08f, 0.55f, 0.08f));
            AddBox(root, new Vector3(0f, 0.59f, 0f), new Vector3(0.58f, 0.08f, 0.08f));
            root.AddComponent<PlayAnywherePortal>();
        }

        // Two parts (PlayAnywherePipe): Entrée, a dark hole with a green rim
        // flat on the floor, the ball rolls onto it and drops in; Sortie, a
        // short green pipe end lying along Z, its mouth at Z = 0 and an
        // arrow on top. In the prefab the exit sits 2 m ahead (dropped
        // straight in the scene, it works as is); placed from the crate, it
        // comes second. The root's box is the entry's, then the exit's.
        private static void BuildPipe(GameObject root, PartMaker parts)
        {
            Material green = Colour("Prop_Tuyau", new Color(0.1f, 0.7f, 0.2f));
            Material dark = Colour("Prop_Tuyau_Bouche", new Color(0.02f, 0.02f, 0.02f));

            var entry = new GameObject("Entrée").transform;
            entry.SetParent(root.transform, false);
            parts.Add("Bord", PlateMesh(0.4f, 0.4f, 0.006f, 0.02f), green, Vector3.zero, entry);
            parts.Add("Trou", PlateMesh(0.28f, 0.28f, 0.008f, 0.01f), dark, Vector3.zero, entry);

            var exit = new GameObject("Sortie").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(0f, 0f, 2f);
            parts.Add("Tube", BoxMesh(new Vector3(0.3f, 0.3f, 0.45f)), green, new Vector3(0f, 0f, -0.235f), exit);
            parts.Add("Bouche", BoxMesh(new Vector3(0.22f, 0.22f, 0.01f)), dark, new Vector3(0f, 0.04f, 0f), exit);
            parts.Add("Flèche", ArrowMesh(0.16f, 0.2f, 0.01f), Colour("Prop_Ressort_Fleche", new Color(1f, 0.85f, 0.1f)), new Vector3(0f, 0.3f, -0.235f), exit);

            AddBox(root, new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.04f, 0.4f));
            var pipe = root.AddComponent<PlayAnywherePipe>();
            var so = new SerializedObject(pipe);
            so.FindProperty("entry").objectReferenceValue = entry;
            so.FindProperty("exit").objectReferenceValue = exit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A squat dark block with a red band and a fuse: placed, it clears
        // the placed objects around it (PlayAnywhereDestroyer).
        private static void BuildDestroyer(GameObject root, PartMaker parts)
        {
            parts.Add("Corps", BoxMesh(new Vector3(0.3f, 0.25f, 0.3f)), Colour("Prop_Destructeur", new Color(0.12f, 0.12f, 0.14f)), Vector3.zero);
            parts.Add("Bande", BoxMesh(new Vector3(0.31f, 0.05f, 0.31f)), Colour("Prop_Destructeur_Bande", new Color(1f, 0.15f, 0.1f)), new Vector3(0f, 0.1f, 0f));
            parts.Add("Mèche", BoxMesh(new Vector3(0.03f, 0.12f, 0.03f)), Colour("Prop_Destructeur_Meche", new Color(0.95f, 0.8f, 0.5f)), new Vector3(0f, 0.25f, 0f));
            AddBox(root, new Vector3(0f, 0.125f, 0f), new Vector3(0.3f, 0.25f, 0.3f));
            root.AddComponent<PlayAnywhereDestroyer>();
        }

        // A box with a grille facing Z and an arrow on top: it blows that
        // way (PlayAnywhereFan's zone, 2 m ahead).
        private static void BuildFan(GameObject root, PartMaker parts)
        {
            parts.Add("Boîtier", BoxMesh(new Vector3(0.45f, 0.45f, 0.15f)), Colour("Prop_Ventilateur", new Color(0.75f, 0.78f, 0.85f)), Vector3.zero);
            parts.Add("Grille", BoxMesh(new Vector3(0.36f, 0.36f, 0.01f)), Colour("Prop_Ventilateur_Grille", new Color(0.12f, 0.12f, 0.15f)), new Vector3(0f, 0.045f, 0.075f));
            parts.Add("Flèche", ArrowMesh(0.18f, 0.2f, 0.01f), Colour("Prop_Ventilateur_Fleche", new Color(0.3f, 0.75f, 1f)), new Vector3(0f, 0.45f, 0f));
            AddBox(root, new Vector3(0f, 0.225f, 0f), new Vector3(0.45f, 0.45f, 0.15f));
            root.AddComponent<PlayAnywhereFan>();
        }

        // A low base with a red pad facing Z and an arrow on top: the ball
        // is sent that way (PlayAnywhereSpring's zone is in front of the pad).
        private static void BuildSpring(GameObject root, PartMaker parts)
        {
            parts.Add("Socle", BoxMesh(new Vector3(0.4f, 0.2f, 0.2f)), Colour("Prop_Ressort", new Color(0.4f, 0.4f, 0.45f)), Vector3.zero);
            Transform pad = parts.Add("Plaque", BoxMesh(new Vector3(0.36f, 0.16f, 0.04f)), Colour("Prop_Ressort_Plaque", new Color(1f, 0.2f, 0.2f)), new Vector3(0f, 0.02f, 0.12f));
            parts.Add("Flèche", ArrowMesh(0.16f, 0.16f, 0.01f), Colour("Prop_Ressort_Fleche", new Color(1f, 0.85f, 0.1f)), new Vector3(0f, 0.2f, 0f));
            AddBox(root, new Vector3(0f, 0.1f, 0.02f), new Vector3(0.4f, 0.2f, 0.24f));
            var spring = root.AddComponent<PlayAnywhereSpring>();
            var so = new SerializedObject(spring);
            so.FindProperty("pad").objectReferenceValue = pad;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A thin square plate with bevelled edges (the ball rolls onto it),
        // marked as its surface: glue brakes, ice glides.
        private static void BuildPlate(GameObject root, PartMaker parts, string surfaceName, float size, Color colour)
        {
            Mesh plate = PlateMesh(size, size, 0.006f, 0.03f);
            parts.Add("Plaque", plate, Colour("Prop_" + surfaceName, colour), Vector3.zero);
            var c = root.AddComponent<MeshCollider>();
            c.sharedMesh = plate;
            c.convex = true;

            var type = AssetDatabase.LoadAssetAtPath<PlayAnywhereSurfaceType>($"Assets/Resources/PlayAnywhereSurfaces/{surfaceName}.asset");
            if (type == null)
            {
                Debug.LogWarning($"[PlayAnywhere] Pas de surface « {surfaceName} » : la plaque roulera comme le sol. Lance Tools > Pool > Ensure Config Assets Exist, supprime {PropFolder}/{root.name}.prefab et régénère les objets.");
                return;
            }
            var surface = root.AddComponent<PlayAnywhereSurface>();
            var so = new SerializedObject(surface);
            so.FindProperty("type").objectReferenceValue = type;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject EnsurePropPrefab(string name, System.Func<Mesh> makeMesh, Color colour, bool box)
        {
            string path = $"{PropFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            Mesh mesh = makeMesh();
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, $"{PropFolder}/{name}_Mesh.asset");

            // One object, pivot at the bottom centre, the collider on it.
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material m = Colour("Prop_" + name, colour);
            if (m != null) go.AddComponent<MeshRenderer>().sharedMaterial = m;
            else go.AddComponent<MeshRenderer>();
            if (box)
            {
                var c = go.AddComponent<BoxCollider>();
                c.center = mesh.bounds.center;
                c.size = mesh.bounds.size;
            }
            else
            {
                var c = go.AddComponent<MeshCollider>();
                c.sharedMesh = mesh;
                c.convex = true;
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // Flat-shaded solids: every face has its own vertices, each turned
        // to face away from the solid's centre.
        private sealed class MeshBuilder
        {
            private readonly System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
            private readonly System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
            private readonly Vector3 centre;

            public MeshBuilder(Vector3 centre) { this.centre = centre; }

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                // Unity's front face: cross(b - a, c - a) points at the viewer.
                Vector3 outward = (a + b + c) / 3f - centre;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b);
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh Build()
            {
                var mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static Mesh BoxMesh(Vector3 size)
        {
            float x = size.x / 2f, z = size.z / 2f, h = size.y;
            var b = new MeshBuilder(new Vector3(0f, h / 2f, 0f));
            Vector3 p000 = new Vector3(-x, 0, -z), p100 = new Vector3(x, 0, -z), p101 = new Vector3(x, 0, z), p001 = new Vector3(-x, 0, z);
            Vector3 p010 = new Vector3(-x, h, -z), p110 = new Vector3(x, h, -z), p111 = new Vector3(x, h, z), p011 = new Vector3(-x, h, z);
            b.Quad(p000, p100, p101, p001);
            b.Quad(p010, p110, p111, p011);
            b.Quad(p000, p100, p110, p010);
            b.Quad(p001, p101, p111, p011);
            b.Quad(p000, p001, p011, p010);
            b.Quad(p100, p101, p111, p110);
            return b.Build();
        }

        // A wedge along +Z: flush with the ground at the front (the ball
        // rolls on without a step), Height high at the back.
        private static Mesh RampMesh(float width, float length, float height)
        {
            float x = width / 2f, z = length / 2f;
            var b = new MeshBuilder(new Vector3(0f, height / 3f, z / 3f));
            Vector3 fl = new Vector3(-x, 0, -z), fr = new Vector3(x, 0, -z);
            Vector3 bl = new Vector3(-x, 0, z), br = new Vector3(x, 0, z);
            Vector3 tl = new Vector3(-x, height, z), tr = new Vector3(x, height, z);
            b.Quad(fl, fr, br, bl);   // bottom
            b.Quad(fl, fr, tr, tl);   // slope
            b.Quad(bl, br, tr, tl);   // back
            b.Tri(fl, bl, tl);        // sides
            b.Tri(fr, br, tr);
            return b.Build();
        }

        // A flat slab, its top smaller by Bevel on every side: edges a ball
        // rolls up instead of a step.
        private static Mesh PlateMesh(float width, float length, float height, float bevel)
        {
            float x = width / 2f, z = length / 2f, tx = x - bevel, tz = z - bevel;
            var b = new MeshBuilder(new Vector3(0f, height / 2f, 0f));
            Vector3 b0 = new Vector3(-x, 0, -z), b1 = new Vector3(x, 0, -z), b2 = new Vector3(x, 0, z), b3 = new Vector3(-x, 0, z);
            Vector3 t0 = new Vector3(-tx, height, -tz), t1 = new Vector3(tx, height, -tz), t2 = new Vector3(tx, height, tz), t3 = new Vector3(-tx, height, tz);
            b.Quad(b0, b1, b2, b3);
            b.Quad(t0, t1, t2, t3);
            b.Quad(b0, b1, t1, t0);
            b.Quad(b1, b2, t2, t1);
            b.Quad(b2, b3, t3, t2);
            b.Quad(b3, b0, t0, t3);
            return b.Build();
        }

        // A flat triangle pointing along +Z, lying on its base: shows which
        // way a gadget works, on the object and on its placing preview.
        private static Mesh ArrowMesh(float width, float length, float thickness)
        {
            float x = width / 2f, z = length / 2f;
            var b = new MeshBuilder(new Vector3(0f, thickness / 2f, -z / 3f));
            Vector3 tip = new Vector3(0, 0, z), left = new Vector3(-x, 0, -z), right = new Vector3(x, 0, -z);
            Vector3 up = Vector3.up * thickness;
            b.Tri(tip, left, right);
            b.Tri(tip + up, left + up, right + up);
            b.Quad(tip, left, left + up, tip + up);
            b.Quad(left, right, right + up, left + up);
            b.Quad(right, tip, tip + up, right + up);
            return b.Build();
        }

        private static Mesh ConeMesh(float radius, float height, int segments)
        {
            var b = new MeshBuilder(new Vector3(0f, height / 4f, 0f));
            Vector3 apex = new Vector3(0f, height, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                b.Tri(p0, p1, apex);
                b.Tri(p0, p1, Vector3.zero);
            }
            return b.Build();
        }

        // Marks the selected décor objects as one kind of ground (a
        // PlayAnywhereSurface with the type of Assets/Resources/PlayAnywhereSurfaces).
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Parquet", priority = 60)]
        private static void SurfaceParquet() => SetSurface("Parquet");
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Carrelage", priority = 61)]
        private static void SurfaceTiles() => SetSurface("Carrelage");
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Comptoir", priority = 62)]
        private static void SurfaceCounter() => SetSurface("Comptoir");
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Tapis", priority = 63)]
        private static void SurfaceRug() => SetSurface("Tapis");
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Moquette", priority = 64)]
        private static void SurfaceCarpet() => SetSurface("Moquette");

        private static void SetSurface(string typeName)
        {
            string path = $"Assets/Resources/PlayAnywhereSurfaces/{typeName}.asset";
            var type = AssetDatabase.LoadAssetAtPath<PlayAnywhereSurfaceType>(path);
            if (type == null)
            {
                // Not made yet: the same as Ensure Config Assets Exist does.
                PoolTableBuilder.EnsurePlayAnywhereSettingsAsset();
                type = AssetDatabase.LoadAssetAtPath<PlayAnywhereSurfaceType>(path);
            }
            if (type == null)
            {
                Debug.LogWarning($"[PlayAnywhere] Pas de surface « {typeName} » ({path}), même après l'avoir créée : renommée ou déplacée ?");
                return;
            }
            foreach (GameObject go in Selection.gameObjects)
            {
                PlayAnywhereSurface surface = go.GetComponent<PlayAnywhereSurface>();
                if (surface == null) surface = Undo.AddComponent<PlayAnywhereSurface>(go);
                var so = new SerializedObject(surface);
                so.FindProperty("type").objectReferenceValue = type;
                so.ApplyModifiedProperties();
            }
            Debug.Log($"[PlayAnywhere] {Selection.gameObjects.Length} objet(s) en {typeName}.");
        }

        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Parquet", true)]
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Carrelage", true)]
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Comptoir", true)]
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Tapis", true)]
        [MenuItem("Tools/Pool/Play Anywhere/Surface de la sélection/Moquette", true)]
        private static bool HasSelection() => Selection.gameObjects.Length > 0;

        // The first solid surface under the Scene view's centre, and the
        // view's heading on the ground.
        private static Vector3 GroundUnderSceneView(out Vector3 forward)
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 pivot = view != null ? view.pivot : Vector3.zero;
            forward = view != null ? Vector3.ProjectOnPlane(view.rotation * Vector3.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            return Physics.Raycast(pivot + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point : pivot;
        }

        // A flat disc lying on the ground, without a collider: the balls roll
        // over it.
        private static Transform Disc(string name, Transform parent, Vector3 position, float radius, Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.position = position + Vector3.up * 0.001f;
            disc.transform.localScale = new Vector3(radius * 2f, 0.001f, radius * 2f);
            if (material != null) disc.GetComponent<Renderer>().sharedMaterial = material;
            return disc.transform;
        }

        // A pole and a pennant beside the cup, also without colliders. Not a
        // child of the disc, whose flat scale would squash it.
        private static Transform Flag(Transform cup)
        {
            var flag = new GameObject("Drapeau");
            flag.transform.SetParent(cup.parent, false);
            flag.transform.position = cup.position + cup.parent.right * 0.09f;

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Mat";
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(flag.transform, false);
            pole.transform.localPosition = Vector3.up * 0.4f;
            pole.transform.localScale = new Vector3(0.012f, 0.4f, 0.012f);
            pole.GetComponent<Renderer>().sharedMaterial = Colour("Pole", new Color(0.9f, 0.9f, 0.9f));

            GameObject pennant = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pennant.name = "Fanion";
            Object.DestroyImmediate(pennant.GetComponent<Collider>());
            pennant.transform.SetParent(flag.transform, false);
            pennant.transform.localPosition = new Vector3(0.09f, 0.72f, 0f);
            pennant.transform.localScale = new Vector3(0.18f, 0.11f, 0.005f);
            pennant.GetComponent<Renderer>().sharedMaterial = Colour("Flag", new Color(1f, 0.85f, 0.1f));
            return flag.transform;
        }

        // One plain URP material per colour, saved once in MaterialFolder.
        private static Material Colour(string name, Color colour)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "PlayAnywhere");
            m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // The same, see-through (the colour's alpha) and glowing: URP Lit
        // switched to its Transparent surface the way its material editor does.
        private static Material GlowColour(string name, Color colour)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = Colour(name, colour);
            if (m == null) return null;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(colour.r, colour.g, colour.b) * 1.5f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
#endif
