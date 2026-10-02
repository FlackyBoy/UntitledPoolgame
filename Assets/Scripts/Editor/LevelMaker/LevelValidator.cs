#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Core;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;
using Object = UnityEngine.Object;

namespace UntitledPoolGame.PoolEditor
{
    public enum CheckStatus { Ok, Warning, Error }

    public class LevelCheck
    {
        public string Title;
        public CheckStatus Status;
        public string Detail;
        public string FixLabel;
        public Action Fix;
        public Object Target;
        public bool ManualOnly;   // left out of "Tout corriger" (destructive)
    }

    // Everything a level needs to be playable, each with a fix when one is
    // safe to apply automatically. The list mirrors what went wrong while
    // setting up scenes by hand (see docs/content/level.md, section 6).
    public static class LevelValidator
    {
        private static readonly string[] RequiredLayers = { "Poolball", "PlayerAnimated", "Ragdoll", "Cuestick" };

        public static List<LevelCheck> Run(LevelMakerSettings settings)
        {
            var checks = new List<LevelCheck>();
            CheckScene(checks);
            CheckTable(checks);
            CheckCues(checks, settings);
            CheckPlayers(checks, settings);
            CheckPowers(checks);
            CheckAmbience(checks, settings);
            CheckProject(checks);
            return checks;
        }

        private static void CheckAmbience(List<LevelCheck> checks, LevelMakerSettings settings)
        {
            AmbienceController controller = AmbienceTools.Controller;
            if (controller == null)
            {
                Add(checks, CheckStatus.Warning, "Pas d'ambiance", "Éclairage par défaut, rien de précalculé : étape ⑥ Ambiance.");
                return;
            }
            if (!AmbienceTools.RenderAssets().All(AmbienceTools.ApvEnabled))
                Add(checks, CheckStatus.Warning, "APV désactivé dans un asset URP", "Les scénarios d'éclairage des ambiances ne seront pas utilisés.",
                    "Activer", AmbienceTools.EnableApv);
            if (controller.active == null)
                Add(checks, CheckStatus.Warning, "Aucune ambiance active", "Choisis-en une à l'étape ⑥ Ambiance.", target: controller);
            var unbaked = controller.presets.Where(p => p != null && !AmbienceTools.IsBaked(p)).ToList();
            if (unbaked.Count > 0)
            {
                Add(checks, CheckStatus.Warning, $"{unbaked.Count} ambiance(s) non précalculée(s)", string.Join(", ", unbaked.Select(p => p.name)),
                    "Précalculer", () => AmbienceTools.Bake(controller, controller.presets, settings), controller);
                checks[checks.Count - 1].ManualOnly = true;   // a long bake: never part of "Tout corriger"
            }
            else if (controller.presets.Count > 0)
                Add(checks, CheckStatus.Ok, $"{controller.presets.Count} ambiance(s) précalculée(s)", target: controller);
        }

        private static void Add(List<LevelCheck> checks, CheckStatus status, string title, string detail = null,
            string fixLabel = null, Action fix = null, Object target = null)
        {
            checks.Add(new LevelCheck { Status = status, Title = title, Detail = detail, FixLabel = fixLabel, Fix = fix, Target = target });
        }

        private static void CheckScene(List<LevelCheck> checks)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Add(checks, CheckStatus.Warning, "Scène non enregistrée", "Enregistre-la pour pouvoir l'ajouter au build.");
                return;
            }
            if (LevelBuilder.InBuildSettings(scene.path))
                Add(checks, CheckStatus.Ok, "Scène dans le build");
            else
                Add(checks, CheckStatus.Warning, "Scène absente du build", scene.path,
                    "Ajouter", () => LevelBuilder.AddToBuildSettings(scene.path));
        }

        private static void CheckTable(List<LevelCheck> checks)
        {
            List<PoolMatchRules> rules = LevelBuilder.FindAll<PoolMatchRules>();
            if (rules.Count == 0)
            {
                Add(checks, CheckStatus.Error, "Aucune table", "Place une table à l'étape ③ Table.");
                return;
            }
            if (rules.Count > 1)
                Add(checks, CheckStatus.Error, $"{rules.Count} tables (PoolMatchRules)", "Une seule table par niveau.", target: rules[1]);
            else
                Add(checks, CheckStatus.Ok, "Une table", target: rules[0]);

            List<PoolTableSurface> surfaces = LevelBuilder.FindAll<PoolTableSurface>();
            if (surfaces.Count != 1)
                Add(checks, CheckStatus.Error, $"{surfaces.Count} tapis (PoolTableSurface)", "Il en faut exactement un : régénère la physique de la table.",
                    target: surfaces.FirstOrDefault());
            else
                CheckPhysicsScale(checks, surfaces[0].transform.parent);

            int pockets = LevelBuilder.FindAll<PoolPocket>().Count;
            Add(checks, pockets == 6 ? CheckStatus.Ok : CheckStatus.Error, $"{pockets} poches sur 6");

            CheckBalls(checks);
        }

        // Unity spheres can't take a non-uniform scale: under a scaled
        // PoolPhysics every ball and pocket collider is wrong.
        private static void CheckPhysicsScale(List<LevelCheck> checks, Transform physicsRoot)
        {
            if (physicsRoot == null) return;
            Vector3 s = physicsRoot.lossyScale;
            if (Mathf.Abs(s.x - 1f) < 0.01f && Mathf.Abs(s.y - 1f) < 0.01f && Mathf.Abs(s.z - 1f) < 0.01f)
            {
                Add(checks, CheckStatus.Ok, "Échelle de la physique = 1", target: physicsRoot);
                return;
            }
            Add(checks, CheckStatus.Error, $"Échelle de la physique = {s.x:0.###} × {s.y:0.###} × {s.z:0.###}",
                "La physique de la table doit rester à l'échelle 1 (les sphères des billes et des poches se déforment sinon).",
                "Remettre à 1", () =>
                {
                    Vector3 p = physicsRoot.parent != null ? physicsRoot.parent.lossyScale : Vector3.one;
                    Undo.RecordObject(physicsRoot, "Échelle de la physique");
                    physicsRoot.localScale = new Vector3(1f / p.x, 1f / p.y, 1f / p.z);
                }, physicsRoot);
        }

        private static void CheckBalls(List<LevelCheck> checks)
        {
            List<PoolBall> balls = LevelBuilder.FindAll<PoolBall>();
            int cueBalls = balls.Count(b => b.IsCueBall);
            var numbers = balls.Where(b => !b.IsCueBall).Select(b => b.Number).ToList();
            var missing = Enumerable.Range(1, 15).Where(n => !numbers.Contains(n)).ToList();
            var doubled = numbers.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            if (balls.Count == 16 && cueBalls == 1 && missing.Count == 0 && doubled.Count == 0)
                Add(checks, CheckStatus.Ok, "16 billes dont 1 blanche");
            else
            {
                string detail = $"{balls.Count} billes, {cueBalls} blanche(s)";
                if (missing.Count > 0) detail += $" ; manquantes : {string.Join(", ", missing)}";
                if (doubled.Count > 0) detail += $" ; en double : {string.Join(", ", doubled)}";
                Add(checks, CheckStatus.Error, "Jeu de billes incomplet", detail);
            }

            var withoutPower = balls.Where(b => !b.IsCueBall && b.GetComponent<PowerBall>() == null).ToList();
            if (withoutPower.Count > 0)
                Add(checks, CheckStatus.Warning, $"{withoutPower.Count} bille(s) sans PowerBall", "Elles ne recevront jamais de pouvoir.",
                    "Ajouter", () => withoutPower.ForEach(b => Undo.AddComponent<PowerBall>(b.gameObject)), withoutPower[0]);

            int ballLayer = LayerMask.NameToLayer("Poolball");
            var wrongLayer = ballLayer < 0 ? new List<PoolBall>() : balls.Where(b => b.gameObject.layer != ballLayer).ToList();
            if (wrongLayer.Count > 0)
                Add(checks, CheckStatus.Warning, $"{wrongLayer.Count} bille(s) hors de la couche Poolball",
                    "Les joueurs et les queues les percutent (PhysicsLayerSetup se base sur cette couche).",
                    "Corriger", () => wrongLayer.ForEach(b =>
                    {
                        Undo.RecordObject(b.gameObject, "Couche des billes");
                        b.gameObject.layer = ballLayer;
                    }), wrongLayer[0]);
        }

        private static void CheckCues(List<LevelCheck> checks, LevelMakerSettings settings)
        {
            List<Cue> cues = LevelBuilder.FindAll<Cue>();
            int usable = 0;
            foreach (Cue cue in cues)
            {
                List<string> missing = LevelBuilder.MissingCueParts(cue);
                if (missing.Count == 0) { usable++; continue; }
                Cue broken = cue;
                Add(checks, CheckStatus.Error, $"Queue « {cue.name} » inutilisable", "Manque : " + string.Join(", ", missing),
                    settings.cuePrefab != null ? "Remplacer par le prefab" : null,
                    settings.cuePrefab != null ? () => ReplaceCue(broken, settings.cuePrefab) : (Action)null, cue);
            }

            if (usable >= settings.cueCount)
                Add(checks, CheckStatus.Ok, $"{usable} queue(s) utilisable(s)");
            else
                Add(checks, CheckStatus.Warning, $"{usable} queue(s) utilisable(s) sur {settings.cueCount}",
                    settings.cuePrefab == null ? "Aucun prefab de queue dans les réglages du Level Maker." : null,
                    settings.cuePrefab != null ? "Placer" : null,
                    settings.cuePrefab != null ? () => LevelBuilder.PlaceMissingCues(settings) : (Action)null);
        }

        private static void ReplaceCue(Cue cue, GameObject prefab)
        {
            Transform t = cue.transform;
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(replacement, "Remplacer la queue");
            replacement.transform.SetParent(t.parent, false);
            replacement.transform.SetPositionAndRotation(t.position, t.rotation);
            replacement.name = t.name;
            Undo.DestroyObjectImmediate(t.gameObject);
        }

        private static void CheckPlayers(List<LevelCheck> checks, LevelMakerSettings settings)
        {
            List<PlayerInputManager> managers = LevelBuilder.FindAll<PlayerInputManager>();
            Action ensure = () => LevelBuilder.EnsureInputManager(settings);
            if (managers.Count == 0)
                Add(checks, CheckStatus.Error, "Aucun PlayerInputManager", "Personne ne peut rejoindre la partie.", "Créer", ensure);
            else
            {
                if (managers.Count > 1)
                    Add(checks, CheckStatus.Error, $"{managers.Count} PlayerInputManager", "Un seul par niveau.", target: managers[1]);

                PlayerInputManager manager = managers[0];
                var so = new SerializedObject(manager);
                var prefab = so.FindProperty("m_PlayerPrefab").objectReferenceValue as GameObject;
                if (prefab == null)
                    Add(checks, CheckStatus.Error, "Pas de prefab joueur", null,
                        settings.playerPrefab != null ? "Assigner" : null, settings.playerPrefab != null ? ensure : null, manager);
                else if (prefab.GetComponentInChildren<LocalFpsPlayerController>(true) == null)
                    Add(checks, CheckStatus.Error, $"Le prefab joueur « {prefab.name} » n'est pas un joueur local",
                        "Il lui manque LocalFpsPlayerController.", target: manager);
                else
                    CheckPlayerPrefab(checks, prefab);

                bool split = so.FindProperty("m_SplitScreen").boolValue;
                int max = so.FindProperty("m_MaxPlayerCount").intValue;
                if (split && max == 2)
                    Add(checks, CheckStatus.Ok, "Écran partagé, 2 joueurs max", target: manager);
                else
                    Add(checks, CheckStatus.Warning, "Réglages du PlayerInputManager",
                        $"Écran partagé : {(split ? "oui" : "non")}, joueurs max : {(max < 0 ? "illimité" : max.ToString())}.",
                        "Régler", ensure, manager);
            }

            CheckSpawnPoints(checks, settings);

            var placed = LevelBuilder.FindAll<LocalFpsPlayerController>();
            foreach (LocalFpsPlayerController player in placed)
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(player.gameObject) ?? player.gameObject;
                Add(checks, CheckStatus.Warning, $"Joueur « {root.name} » posé dans la scène",
                    "Les joueurs sont créés par le PlayerInputManager : celui-ci s'ajoute en plus.",
                    "Retirer", () => Undo.DestroyObjectImmediate(root), root);
                checks[checks.Count - 1].ManualOnly = true;
            }
        }

        // What a spawned player can't find for itself: its own parts. Scene
        // objects (table, rules, cues) are found at runtime, and the few
        // references a scene used to wire by hand (preview lines, aim look
        // target, cameras) are found or created by the scripts.
        private static void CheckPlayerPrefab(List<LevelCheck> checks, GameObject prefab)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            PlayerInput input = prefab.GetComponent<PlayerInput>();
            if (input == null) errors.Add("PlayerInput");
            else if (input.actions == null) errors.Add("actions du PlayerInput");
            if (prefab.GetComponent<LocalPlayerHandController>() == null) errors.Add("LocalPlayerHandController");
            if (prefab.GetComponent<LocalPoolAimController>() == null) errors.Add("LocalPoolAimController");
            if (prefab.GetComponentInChildren<Camera>(true) == null) errors.Add("caméra");
            if (prefab.GetComponentInChildren<RootMotion.FinalIK.FullBodyBipedIK>(true) == null)
                errors.Add("FullBodyBipedIK (prise, coups)");
            if (prefab.GetComponentInChildren<RootMotion.FinalIK.LookAtIK>(true) == null)
                warnings.Add("LookAtIK (la tête ne suit pas la bille)");
            if (prefab.GetComponentInChildren<LocalPlayerRagdollController>(true) == null)
                warnings.Add("LocalPlayerRagdollController (pas de ragdoll)");
            LocalCueHolder holder = prefab.GetComponent<LocalCueHolder>();
            if (holder != null && !holder.enabled)
                warnings.Add("LocalCueHolder désactivé (ancienne prise de queue, à câbler par scène)");

            if (errors.Count > 0)
                Add(checks, CheckStatus.Error, $"Prefab joueur « {prefab.name} » incomplet", "Manque : " + string.Join(", ", errors), target: prefab);
            if (warnings.Count > 0)
                Add(checks, CheckStatus.Warning, $"Prefab joueur « {prefab.name} »", string.Join(" ; ", warnings), target: prefab);
            if (errors.Count == 0 && warnings.Count == 0)
                Add(checks, CheckStatus.Ok, $"Prefab joueur « {prefab.name} » complet",
                    "Aperçu de trajectoire, cible du regard et caméras sont retrouvés ou créés au lancement s'ils sont vides.", target: prefab);
        }

        private static void CheckSpawnPoints(List<LevelCheck> checks, LevelMakerSettings settings)
        {
            Transform[] points = LevelBuilder.SpawnPoints;
            int valid = points.Count(p => p != null);
            if (valid < 2)
            {
                Add(checks, CheckStatus.Error, $"{valid} point(s) d'apparition sur 2",
                    "Les joueurs apparaissent à la position du prefab.", "Créer", () => LevelBuilder.EnsureSpawnPoints(settings));
                return;
            }

            bool tableKnown = LevelBuilder.TryGetTableBounds(out Bounds table);
            table.Expand(new Vector3(0.6f, 0f, 0.6f));
            bool allGood = true;
            foreach (Transform p in points)
            {
                if (p == null) continue;
                Vector3 pos = p.position;
                if (tableKnown && pos.x > table.min.x && pos.x < table.max.x && pos.z > table.min.z && pos.z < table.max.z)
                {
                    allGood = false;
                    Add(checks, CheckStatus.Error, $"« {p.name} » est dans la table", "Déplace-le à l'étape ④ Joueurs.", target: p);
                }
                else if (!LevelBuilder.TryFindGround(pos, out _, 0.5f, 3f))
                {
                    allGood = false;
                    Add(checks, CheckStatus.Warning, $"Pas de sol sous « {p.name} »", "Le joueur tombera dans le vide.", target: p);
                }
            }
            if (allGood) Add(checks, CheckStatus.Ok, $"{valid} points d'apparition");
        }

        private static void CheckPowers(List<LevelCheck> checks)
        {
            PoolMatchRules rules = LevelBuilder.Rules;
            Action addSystem = rules != null ? () => PoolTableBuilder.AddPowerSpawnSystemTo(rules.gameObject) : (Action)null;
            string label = rules != null ? "Ajouter" : null;

            int points = LevelBuilder.FindAll<PoolPowerSpawnPoint>().Count;
            int managers = LevelBuilder.FindAll<PoolPowerCrateManager>().Count;
            int rotators = LevelBuilder.FindAll<PoolPowerBallRotator>().Count;

            if (points == 0)
                Add(checks, CheckStatus.Warning, "Aucun point de caisse de pouvoir", "Peins-en à l'étape ⑤ Pouvoirs.", label, addSystem);
            else
                Add(checks, CheckStatus.Ok, $"{points} points de caisse de pouvoir");

            string counts = $"PoolPowerCrateManager : {managers}, PoolPowerBallRotator : {rotators} (il en faut un de chaque).";
            if (managers == 1 && rotators == 1)
                Add(checks, CheckStatus.Ok, "Gestionnaires de pouvoirs");
            else if (managers > 1 || rotators > 1)
            {
                // Two crate managers each spawn their own set of crates.
                Add(checks, CheckStatus.Error, "Gestionnaires de pouvoirs en double",
                    counts + " Deux gestionnaires font apparaître deux fois les caisses. La correction retire les doublons, " +
                    "dans le prefab de la table s'ils en viennent (non annulable).",
                    "Retirer les doublons", () =>
                    {
                        RemoveDuplicates<PoolPowerCrateManager>();
                        RemoveDuplicates<PoolPowerBallRotator>();
                    }, LevelBuilder.FindAll<PoolPowerCrateManager>().FirstOrDefault());
                checks[checks.Count - 1].ManualOnly = true;
            }
            else
                Add(checks, CheckStatus.Warning, "Gestionnaires de pouvoirs", counts, label, addSystem);
        }

        // Keeps the first T in the scene. Extras that come from a prefab are
        // removed in the prefab asset itself: removing them on the instance
        // would only hide them in this scene.
        private static void RemoveDuplicates<T>() where T : Component
        {
            var prefabPaths = new HashSet<string>();
            foreach (T extra in LevelBuilder.FindAll<T>().Skip(1))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(extra) && !PrefabUtility.IsAddedComponentOverride(extra))
                    prefabPaths.Add(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(extra)));
                else
                    Undo.DestroyObjectImmediate(extra);
            }
            foreach (string path in prefabPaths)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                foreach (T extra in contents.GetComponentsInChildren<T>(true).Skip(1))
                    Object.DestroyImmediate(extra);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.Log($"[Level Maker] Doublons de {typeof(T).Name} retirés du prefab {path}.");
            }
        }

        private static void CheckProject(List<LevelCheck> checks)
        {
            var missingAssets = new List<string>();
            if (Resources.Load<PoolPhysicsSettings>("PoolPhysicsSettings") == null) missingAssets.Add("PoolPhysicsSettings");
            if (Resources.Load<PoolScreenJuiceSettings>("PoolScreenJuiceSettings") == null) missingAssets.Add("PoolScreenJuiceSettings");
            if (Resources.Load<PoolPotEffectSettings>("PoolPotEffectSettings") == null) missingAssets.Add("PoolPotEffectSettings");
            if (Resources.Load<PoolPowerSpawnSettings>("PoolPowerSpawnSettings") == null) missingAssets.Add("PoolPowerSpawnSettings");
            if (missingAssets.Count == 0)
                Add(checks, CheckStatus.Ok, "Fichiers de réglages dans Resources");
            else
                Add(checks, CheckStatus.Warning, "Fichiers de réglages manquants", string.Join(", ", missingAssets),
                    "Créer", PoolTableBuilder.EnsureConfigAssetsExist);

            var missingLayers = RequiredLayers.Where(l => LayerMask.NameToLayer(l) < 0).ToList();
            if (missingLayers.Count == 0)
                Add(checks, CheckStatus.Ok, "Couches physiques");
            else
                Add(checks, CheckStatus.Error, "Couches manquantes : " + string.Join(", ", missingLayers),
                    "À créer dans Project Settings > Tags and Layers.");
        }
    }
}
#endif
