#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // Tools > Pool > Level Maker: builds a playable level step by step
    // (scene base, table, players, power points) and checks it. Scene-view
    // helpers (felt template, J1/J2 handles, power point brush) are only
    // active on their own step. Spec: docs/content/level.md.
    // The room step (walls, openings, props) lives in LevelMakerWindow.Room.cs.
    public partial class LevelMakerWindow : EditorWindow
    {
        private enum Step { Level, Room, Table, Players, Powers, Ambience, Check }

        private static readonly string[] StepNames = { "① Niveau", "② Salle", "③ Table", "④ Joueurs", "⑤ Pouvoirs", "⑥ Ambiance", "⑦ Vérif." };

        private Step step;
        private LevelMakerSettings settings;
        private SerializedObject settingsSo;
        private Editor settingsEditor;
        private string newLevelName = "NouveauNiveau";
        private GameObject pendingTable;   // placed model still without its physics
        private bool showFeltTemplate = true;
        private bool snapSpawnsToGround = true;
        private bool painting;
        private Vector2 scroll;

        private List<LevelCheck> checks = new List<LevelCheck>();
        private bool checksDirty = true;

        [MenuItem("Tools/Pool/Level Maker")]
        public static void Open() => GetWindow<LevelMakerWindow>("Level Maker");

        private void OnEnable()
        {
            settings = LevelMakerSettings.GetOrCreate();
            settingsSo = new SerializedObject(settings);
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.hierarchyChanged += MarkDirty;
            Undo.undoRedoPerformed += MarkDirty;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.hierarchyChanged -= MarkDirty;
            Undo.undoRedoPerformed -= MarkDirty;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            if (settingsEditor != null) DestroyImmediate(settingsEditor);
            if (paletteEditor != null) DestroyImmediate(paletteEditor);
        }

        private void OnSceneChanged(Scene from, Scene to)
        {
            pendingTable = null;
            MarkDirty();
        }

        private void MarkDirty()
        {
            checksDirty = true;
            Repaint();
        }

        private void RefreshChecks()
        {
            if (!checksDirty) return;
            checksDirty = false;
            checks = LevelValidator.Run(settings);
        }

        // ---------- Window ----------

        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Le Level Maker ne fonctionne pas en mode Play.", MessageType.Info);
                return;
            }
            RefreshChecks();
            settingsSo.Update();

            EditorGUILayout.LabelField("Scène : " + SceneLabel(), EditorStyles.boldLabel);
            Step previous = step;
            step = (Step)GUILayout.Toolbar((int)step, StepNames, GUILayout.Height(24));
            if (step != previous)
            {
                painting = false;
                StopRoomTool();
                SceneView.RepaintAll();
            }
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (step)
            {
                case Step.Level: DrawLevelStep(); break;
                case Step.Room: DrawRoomStep(); break;
                case Step.Table: DrawTableStep(); break;
                case Step.Players: DrawPlayersStep(); break;
                case Step.Powers: DrawPowersStep(); break;
                case Step.Ambience: DrawAmbienceStep(); break;
                case Step.Check: DrawCheckStep(); break;
            }
            EditorGUILayout.EndScrollView();

            settingsSo.ApplyModifiedProperties();
            DrawSummaryBar();
        }

        private static string SceneLabel()
        {
            Scene scene = SceneManager.GetActiveScene();
            return string.IsNullOrEmpty(scene.path) ? "(non enregistrée)" : scene.name;
        }

        private void DrawLevelStep()
        {
            EditorGUILayout.LabelField("Nouveau niveau", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Crée une scène avec le socle commun : lumière, volume de post-process, sol provisoire, " +
                                    "PlayerInputManager (écran partagé, 2 joueurs) et deux points d'apparition. " +
                                    "Elle est enregistrée dans le dossier des niveaux et ajoutée au build.", MessageType.None);
            newLevelName = EditorGUILayout.TextField("Nom", newLevelName);
            if (GUILayout.Button("Créer le niveau") && LevelBuilder.CreateLevel(newLevelName.Trim(), settings))
            {
                step = Step.Room;
                MarkDirty();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scène ouverte", EditorStyles.boldLabel);
            if (GUILayout.Button("Compléter le socle (gestionnaire de joueurs, points d'apparition, réglages)"))
            {
                LevelBuilder.EnsureInputManager(settings);
                LevelBuilder.EnsureSpawnPoints(settings);
                PoolTableBuilder.EnsureConfigAssetsExist();
                MarkDirty();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Réglages de l'outil", EditorStyles.boldLabel);
            Editor.CreateCachedEditor(settings, null, ref settingsEditor);
            settingsEditor.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Ambiances et dimensions : plus tard (lots 5 et 6). Décidé : une même salle pourra " +
                                    "recevoir plusieurs ambiances et règles, chargées au lancement.", MessageType.None);
        }

        private void DrawTableStep()
        {
            EditorGUILayout.PropertyField(settingsSo.FindProperty("tablePrefab"), new GUIContent("Modèle de table"));
            GameObject table = LevelBuilder.TableRoot;
            if (table == null && pendingTable == null)
            {
                EditorGUILayout.HelpBox("Pas de table dans la scène. Place le modèle, ou sélectionne un modèle déjà posé puis « Générer la physique ».", MessageType.Info);
                using (new EditorGUI.DisabledScope(settings.tablePrefab == null))
                    if (GUILayout.Button("Placer dans la scène (au centre de la vue, posée au sol)"))
                    {
                        pendingTable = LevelBuilder.PlaceTable(settings.tablePrefab, ViewGroundPoint(), 0f);
                        if (LevelBuilder.Rules != null) pendingTable = null;
                        MarkDirty();
                    }
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null || !Selection.activeGameObject.scene.IsValid()))
                    if (GUILayout.Button("Générer la physique sous le modèle sélectionné"))
                        GeneratePhysics(Selection.activeGameObject);
                return;
            }

            GameObject current = table != null ? table : pendingTable;
            EditorGUILayout.ObjectField("Table", current, typeof(GameObject), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"↺ {settings.tableRotationStep:0}°")) RotateTable(current, -settings.tableRotationStep);
                if (GUILayout.Button($"↻ {settings.tableRotationStep:0}°")) RotateTable(current, settings.tableRotationStep);
                if (GUILayout.Button("Poser au sol")) DropToGround(current);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physique", EditorStyles.boldLabel);
            if (table == null)
            {
                EditorGUILayout.HelpBox("Ce modèle n'a pas de physique (pas de PoolMatchRules).", MessageType.Warning);
                if (GUILayout.Button("Générer la physique")) GeneratePhysics(pendingTable);
            }
            else
            {
                EditorGUILayout.LabelField("✓ présente");
                EditorGUILayout.HelpBox("Si elle a été générée, aligne l'objet PoolPhysics sur le tapis du modèle à l'aide du gabarit " +
                                        "(déplacer / tourner seulement, jamais d'échelle : les dimensions se règlent dans " +
                                        "Tools > Pool > Select Custom Table Settings).", MessageType.None);
                if (GUILayout.Button("Sélectionner PoolPhysics") && LevelBuilder.Surface != null)
                    Selection.activeTransform = LevelBuilder.Surface.transform.parent;
            }
            bool template = EditorGUILayout.Toggle("Afficher le gabarit du tapis", showFeltTemplate);
            if (template != showFeltTemplate) { showFeltTemplate = template; SceneView.RepaintAll(); }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Queues", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(settingsSo.FindProperty("cuePrefab"), new GUIContent("Prefab de queue"));
            int usable = LevelBuilder.FindAll<Cue>().Count(c => LevelBuilder.MissingCueParts(c).Count == 0);
            EditorGUILayout.LabelField($"{usable} queue(s) utilisable(s) sur {settings.cueCount}");
            using (new EditorGUI.DisabledScope(usable >= settings.cueCount || settings.cuePrefab == null))
                if (GUILayout.Button("Placer les queues manquantes au bout de la table"))
                {
                    LevelBuilder.PlaceMissingCues(settings);
                    MarkDirty();
                }
        }

        private void GeneratePhysics(GameObject model)
        {
            if (model == null) return;
            PoolTableBuilder.AttachPhysicsTo(model, createCues: false);
            pendingTable = null;
            MarkDirty();
        }

        private static void RotateTable(GameObject table, float degrees)
        {
            Undo.RecordObject(table.transform, "Tourner la table");
            Vector3 pivot = LevelBuilder.TryGetBounds(table, out Bounds b) ? b.center : table.transform.position;
            table.transform.RotateAround(pivot, Vector3.up, degrees);
        }

        // Rests the model's lowest point on whatever is below it (itself ignored).
        private static void DropToGround(GameObject table)
        {
            if (!LevelBuilder.TryGetBounds(table, out Bounds b)) return;
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(b.center + Vector3.up * b.extents.y, Vector3.down, 50f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(table.transform)) continue;
                if (hit.point.y <= b.min.y + 0.5f && hit.point.y > best) best = hit.point.y;
            }
            if (float.IsNegativeInfinity(best)) return;
            Undo.RecordObject(table.transform, "Poser la table au sol");
            table.transform.position += Vector3.up * (best - b.min.y);
        }

        // Ground under the Scene view's center, snapped to 0.25 m.
        private static Vector3 ViewGroundPoint()
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 p = view != null ? view.pivot : Vector3.zero;
            p.x = Mathf.Round(p.x * 4f) / 4f;
            p.z = Mathf.Round(p.z * 4f) / 4f;
            p.y = LevelBuilder.TryFindGround(p, out RaycastHit ground, 5f, 20f) ? ground.point.y : 0f;
            return p;
        }

        private void DrawPlayersStep()
        {
            EditorGUILayout.PropertyField(settingsSo.FindProperty("playerPrefab"), new GUIContent("Prefab joueur"));
            if (GUILayout.Button("Configurer le PlayerInputManager (écran partagé, 2 joueurs)"))
            {
                settingsSo.ApplyModifiedProperties();
                LevelBuilder.EnsureInputManager(settings);
                MarkDirty();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Points d'apparition", EditorStyles.boldLabel);
            Transform[] points = LevelBuilder.SpawnPoints;
            for (int i = 0; i < points.Length; i++)
                EditorGUILayout.ObjectField($"J{i + 1}", points[i], typeof(Transform), true);
            if (GUILayout.Button(points.Count(p => p != null) >= 2 ? "Recréer les points manquants" : "Créer les points (de part et d'autre de la table)"))
            {
                LevelBuilder.EnsureSpawnPoints(settings);
                MarkDirty();
            }
            snapSpawnsToGround = EditorGUILayout.Toggle("Coller au sol en déplaçant", snapSpawnsToGround);
            EditorGUILayout.HelpBox("Dans la vue Scène : flèches pour déplacer, cercle pour orienter. " +
                                    "La silhouette passe au rouge si le point est dans la table.", MessageType.None);
        }

        private void DrawPowersStep()
        {
            int points = LevelBuilder.FindAll<PoolPowerSpawnPoint>().Count;
            EditorGUILayout.LabelField($"{points} point(s) de caisse de pouvoir");

            PoolMatchRules rules = LevelBuilder.Rules;
            bool hasManagers = LevelBuilder.FindAll<PoolPowerCrateManager>().Count > 0 && LevelBuilder.FindAll<PoolPowerBallRotator>().Count > 0;
            using (new EditorGUI.DisabledScope(rules == null || hasManagers))
                if (GUILayout.Button("Ajouter le système de pouvoirs à la table"))
                {
                    PoolTableBuilder.AddPowerSpawnSystemTo(rules.gameObject);
                    MarkDirty();
                }

            EditorGUILayout.Space();
            Color old = GUI.backgroundColor;
            if (painting) GUI.backgroundColor = new Color(0.4f, 1f, 0.6f);
            if (GUILayout.Button(painting ? "Pinceau actif — cliquer pour arrêter" : "Peindre des points", GUILayout.Height(28)))
            {
                painting = !painting;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = old;
            EditorGUILayout.HelpBox("Clic dans la vue Scène : ajoute un point sur la surface visée (sur la table ou dans la salle). " +
                                    "Maj + clic : retire le point le plus proche. Échap : arrête le pinceau.", MessageType.None);

            GameObject room = GameObject.Find(LevelBuilder.RoomPowerPointsName);
            using (new EditorGUI.DisabledScope(room == null))
                if (GUILayout.Button("Retirer tous les points de la salle (hors table)"))
                {
                    Undo.DestroyObjectImmediate(room);
                    MarkDirty();
                }
        }

        private void DrawCheckStep()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Relancer")) MarkDirty();
                using (new EditorGUI.DisabledScope(!checks.Any(c => c.Fix != null && c.Status != CheckStatus.Ok)))
                    if (GUILayout.Button("Tout corriger")) FixAll();
            }
            EditorGUILayout.Space();

            foreach (LevelCheck check in checks.OrderByDescending(c => c.Status))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(StatusIcon(check.Status), GUILayout.Width(20), GUILayout.Height(18));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(check.Title, EditorStyles.boldLabel);
                        if (!string.IsNullOrEmpty(check.Detail))
                            EditorGUILayout.LabelField(check.Detail, EditorStyles.wordWrappedMiniLabel);
                    }
                    if (check.Target != null && GUILayout.Button("Voir", GUILayout.Width(50)))
                    {
                        Selection.activeObject = check.Target;
                        EditorGUIUtility.PingObject(check.Target);
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                    if (check.Fix != null && check.Status != CheckStatus.Ok && GUILayout.Button(check.FixLabel ?? "Corriger", GUILayout.Width(150)))
                    {
                        check.Fix();
                        MarkDirty();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void FixAll()
        {
            // One fix can create what another one checks (e.g. the input
            // manager before its spawn points): rerun between passes.
            for (int pass = 0; pass < 3; pass++)
            {
                var fixable = LevelValidator.Run(settings).Where(c => c.Fix != null && c.Status != CheckStatus.Ok && !c.ManualOnly).ToList();
                if (fixable.Count == 0) break;
                fixable.ForEach(c => c.Fix());
            }
            MarkDirty();
            GUIUtility.ExitGUI();
        }

        private void DrawSummaryBar()
        {
            int ok = checks.Count(c => c.Status == CheckStatus.Ok);
            int warnings = checks.Count(c => c.Status == CheckStatus.Warning);
            int errors = checks.Count(c => c.Status == CheckStatus.Error);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Vérification : {ok} ✓   {warnings} ⚠   {errors} ✗", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (step != Step.Check && GUILayout.Button("Détails", EditorStyles.toolbarButton)) step = Step.Check;
            }
        }

        private static GUIContent StatusIcon(CheckStatus status) => EditorGUIUtility.IconContent(status switch
        {
            CheckStatus.Ok => "TestPassed",
            CheckStatus.Warning => "console.warnicon.sml",
            _ => "console.erroricon.sml",
        });

        // ---------- Scene view ----------

        private void OnSceneGUI(SceneView view)
        {
            if (EditorApplication.isPlaying) return;
            switch (step)
            {
                case Step.Room: RoomSceneGUI(); break;
                case Step.Table: if (showFeltTemplate) DrawFeltTemplate(); break;
                case Step.Players: DrawSpawnHandles(); break;
                case Step.Powers: DrawPowerPoints(); if (painting) PaintPowerPoints(); break;
            }
        }

        // The physics outline to line up with the model's felt: play area,
        // rail band, and pocket mouths.
        private static void DrawFeltTemplate()
        {
            PoolTableSurface surface = LevelBuilder.Surface;
            if (surface == null) return;
            Vector3[] felt = LevelBuilder.FeltCorners(surface);
            Handles.color = new Color(1f, 0.85f, 0.1f, 0.15f);
            Handles.DrawAAConvexPolygon(felt);
            Handles.color = new Color(1f, 0.85f, 0.1f, 1f);
            Handles.DrawAAPolyLine(3f, felt[0], felt[1], felt[2], felt[3], felt[0]);

            PoolTableAssetSettings table = PoolTableBuilder.TableAssetSettings;
            Vector3[] rails = LevelBuilder.FeltCorners(surface, table.railThickness);
            Handles.color = new Color(1f, 0.5f, 0.1f, 0.8f);
            Handles.DrawAAPolyLine(2f, rails[0], rails[1], rails[2], rails[3], rails[0]);

            Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            foreach (PoolPocket pocket in LevelBuilder.FindAll<PoolPocket>())
            {
                float radius = pocket.TryGetComponent(out SphereCollider sphere)
                    ? sphere.radius * pocket.transform.lossyScale.x : table.pocketRadius;
                Handles.DrawWireDisc(pocket.transform.position, Vector3.up, radius, 2f);
            }
            Handles.Label(felt[3], "Gabarit du tapis (physique)", EditorStyles.whiteBoldLabel);
        }

        private void DrawSpawnHandles()
        {
            Transform[] points = LevelBuilder.SpawnPoints;
            bool tableKnown = LevelBuilder.TryGetTableBounds(out Bounds table);
            table.Expand(new Vector3(0.6f, 0f, 0.6f));

            for (int i = 0; i < points.Length; i++)
            {
                Transform p = points[i];
                if (p == null) continue;
                Vector3 pos = p.position;
                bool inTable = tableKnown && pos.x > table.min.x && pos.x < table.max.x && pos.z > table.min.z && pos.z < table.max.z;
                Color color = inTable ? Color.red : (i == 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.55f, 0.2f));

                // Player silhouette: feet, head ring, sides, facing arrow.
                Handles.color = color;
                const float radius = 0.3f, height = 1.8f;
                Handles.DrawWireDisc(pos, Vector3.up, radius, 2f);
                Handles.DrawWireDisc(pos + Vector3.up * height, Vector3.up, radius, 2f);
                Handles.DrawLine(pos + p.right * radius, pos + p.right * radius + Vector3.up * height, 2f);
                Handles.DrawLine(pos - p.right * radius, pos - p.right * radius + Vector3.up * height, 2f);
                Vector3 fwd = LevelBuilder.Flat(p.forward);
                Handles.DrawAAPolyLine(4f, pos + Vector3.up * 0.05f, pos + Vector3.up * 0.05f + fwd * 0.8f);
                Handles.Label(pos + Vector3.up * (height + 0.2f), $"J{i + 1}{(inTable ? " — dans la table !" : "")}", EditorStyles.whiteBoldLabel);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(pos, Quaternion.identity);
                Quaternion turned = Handles.Disc(p.rotation, pos + Vector3.up * 0.05f, Vector3.up, 0.6f, false, 15f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(p, "Déplacer le point d'apparition");
                    if (snapSpawnsToGround && LevelBuilder.TryFindGround(moved, out RaycastHit ground, 1f, 5f))
                        moved.y = ground.point.y;
                    p.position = moved;
                    p.rotation = Quaternion.Euler(0f, turned.eulerAngles.y, 0f);
                    MarkDirty();
                }
            }
        }

        private static void DrawPowerPoints()
        {
            Handles.color = Color.cyan;
            foreach (PoolPowerSpawnPoint point in LevelBuilder.FindAll<PoolPowerSpawnPoint>())
            {
                Vector3 pos = point.transform.position;
                Handles.DrawWireDisc(pos, Vector3.up, 0.12f, 2f);
                Handles.DrawLine(pos, pos + Vector3.up * 0.25f, 2f);
            }
        }

        private void PaintPowerPoints()
        {
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);   // clicks paint instead of selecting

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                painting = false;
                e.Use();
                Repaint();
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Physics.SyncTransforms();
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            bool erasing = e.shift;
            Handles.color = erasing ? Color.red : new Color(0.3f, 1f, 1f);
            Handles.DrawWireDisc(hit.point, hit.normal, erasing ? 0.3f : 0.12f, 2f);
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();

            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;
            if (erasing)
            {
                PoolPowerSpawnPoint nearest = LevelBuilder.FindAll<PoolPowerSpawnPoint>()
                    .OrderBy(p => (p.transform.position - hit.point).sqrMagnitude).FirstOrDefault();
                if (nearest != null && (nearest.transform.position - hit.point).sqrMagnitude < 0.3f * 0.3f)
                    Undo.DestroyObjectImmediate(nearest.gameObject);
            }
            else
                LevelBuilder.AddPowerPoint(hit.point + hit.normal * 0.02f, LevelBuilder.TablePowerFolder(hit.transform));
            e.Use();
            MarkDirty();
        }
    }
}
#endif
