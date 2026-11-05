#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // Tools > Pool > Préparer les animations du Shooter: adds to the player's
    // Animator Controller, at weight 0 and driven from code:
    // - "Course": the sprint without a weapon (LocalPlayerLocomotion);
    // - "Shooter Corps": walking and running with the weapon, whole body
    //   (LocalShooterWeapon);
    // - "Shooter Tir": the hold, ready and firing clips, top half only, so the
    //   arms keep the weapon the same way whatever the legs do.
    // Below the R&D layers: a blow taken still shows over them. Shared
    // controller, but weight 0 changes nothing. Made by itself when missing
    // (EnsureOnLoad); run the menu again to rebuild. The
    // Everyday Motion Pack's clips are only referenced.
    public static class ShooterAnimationTools
    {
        private const string MoveFolder = "Assets/Animations/EverydayMotionPack/Motion/02_Move";
        private const string IdleFile = "Assets/Animations/EverydayMotionPack/Motion/01_Idle/@male_idle_strafing.FBX";
        private const string ReadyFile = "Assets/Animations/EverydayMotionPack/Motion/05_Act/@male_act_strafing_ready.FBX";
        private const string ShootFile = "Assets/Animations/EverydayMotionPack/Motion/05_Act/@male_act_strafing.FBX";

        // File suffix, and where it goes on a blend tree's circle.
        private static readonly (string suffix, Vector2 direction)[] Directions =
        {
            ("front", new Vector2(0f, 1f)),
            ("back", new Vector2(0f, -1f)),
            ("left", new Vector2(-1f, 0f)),
            ("right", new Vector2(1f, 0f)),
            ("front_left", new Vector2(-1f, 1f).normalized),
            ("front_right", new Vector2(1f, 1f).normalized),
            ("back_left", new Vector2(-1f, -1f).normalized),
            ("back_right", new Vector2(1f, -1f).normalized),
        };

        // After each compile: the layers and the cue prefab are made if they're
        // missing, without waiting for someone to think of the menu (log of
        // 04/11: played without them). Delayed: assets can't be saved during
        // the reload itself.
        [InitializeOnLoadMethod]
        private static void EnsureOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                // The powers and their tokens too: same reason.
                if (AssetDatabase.LoadAssetAtPath<ShooterGrapplePower>("Assets/Powers/ShooterGrapplePower.asset") == null)
                {
                    PoolTableBuilder.EnsureShooterPowerAssets();
                    PowerTokenGenerator.Generate(onlyMissing: true);
                }
                AnimatorController controller = RnDCombatAnimationTools.FindController(out _);
                if (controller == null) return;
                var settings = AssetDatabase.LoadAssetAtPath<ShooterMiniSettings>("Assets/Resources/ShooterMiniSettings.asset");
                // By its last state: a layer from an earlier version is rebuilt too.
                bool layers = controller.layers.Any(l => l.name == LocalShooterWeapon.FireLayer
                    && l.stateMachine.states.Any(s => s.state.name == LocalShooterWeapon.HoldState));
                if (!layers || settings == null || settings.cuePrefab == null) PoolTableBuilder.EnsureShooterMiniSettingsAsset();
            };
        }

        [MenuItem("Tools/Pool/Préparer les animations du Shooter", priority = 70)]
        private static void BuildMenu()
        {
            string problem = Build();
            EditorUtility.DisplayDialog("Animations du Shooter", problem ?? "Calques créés dans le contrôleur du joueur.", "OK");
        }

        // Builds (or rebuilds) the layers. The problem met, null if none.
        internal static string Build()
        {
            AnimatorController controller = RnDCombatAnimationTools.FindController(out string error);
            if (controller == null) return error;

            var missing = new List<string>();
            AnimationClip Clip(string path)
            {
                AnimationClip c = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(a => !a.name.StartsWith("__preview__"));
                if (c == null) missing.Add(path);
                return c;
            }
            AnimationClip idle = Clip(IdleFile), ready = Clip(ReadyFile), shoot = Clip(ShootFile);
            var walk = new List<(AnimationClip clip, Vector2 at)>();
            var jog = new List<(AnimationClip clip, Vector2 at)>();
            var run = new List<(AnimationClip clip, Vector2 at)>();
            foreach ((string suffix, Vector2 direction) in Directions)
            {
                walk.Add((Clip($"{MoveFolder}/@male_move_walk_strafing_{suffix}.FBX"), direction));
                jog.Add((Clip($"{MoveFolder}/@male_move_run_jogging_strafing_{suffix}.FBX"), direction * 2f));
                run.Add((Clip($"{MoveFolder}/@male_move_run_{suffix}.FBX"), direction));
            }
            if (missing.Count > 0) return $"Animations introuvables :\n{string.Join("\n", missing)}";

            string[] ours = { LocalPlayerLocomotion.RunLayer, LocalShooterWeapon.BodyLayer, LocalShooterWeapon.FireLayer };
            RemoveLayers(controller, ours);
            AddFloat(controller, LocalPlayerLocomotion.RunXParameter);
            AddFloat(controller, LocalPlayerLocomotion.RunYParameter);
            AddFloat(controller, LocalShooterWeapon.MoveXParameter);
            AddFloat(controller, LocalShooterWeapon.MoveYParameter);

            AddLayer(controller, LocalPlayerLocomotion.RunLayer, null,
                (LocalPlayerLocomotion.RunState, Tree(controller, LocalPlayerLocomotion.RunState, LocalPlayerLocomotion.RunXParameter, LocalPlayerLocomotion.RunYParameter, run)));

            var armed = new List<(AnimationClip clip, Vector2 at)> { (idle, Vector2.zero) };
            armed.AddRange(walk);
            armed.AddRange(jog);
            AddLayer(controller, LocalShooterWeapon.BodyLayer, null,
                (LocalShooterWeapon.MoveState, Tree(controller, LocalShooterWeapon.MoveState, LocalShooterWeapon.MoveXParameter, LocalShooterWeapon.MoveYParameter, armed)));

            AvatarMask upper = RnDCombatAnimationTools.EnsureMask(RnDCombatAnimationTools.Mask.UpperBody);
            AddLayer(controller, LocalShooterWeapon.FireLayer, upper,
                (LocalShooterWeapon.HoldState, idle), (LocalShooterWeapon.ReadyState, ready), (LocalShooterWeapon.ShootState, shoot));

            MoveBelowRnD(controller, ours);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Shooter] Calques créés dans {AssetDatabase.GetAssetPath(controller)} : {string.Join(", ", ours)}.");
            return null;
        }

        private static void AddFloat(AnimatorController controller, string name)
        {
            if (controller.parameters.All(p => p.name != name))
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        // The trees are sub-assets of their own: they'd stay in the file.
        private static void RemoveLayers(AnimatorController controller, string[] names)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                if (!names.Contains(layers[i].name)) continue;
                foreach (ChildAnimatorState child in layers[i].stateMachine.states)
                    if (child.state.motion is BlendTree tree) AssetDatabase.RemoveObjectFromAsset(tree);
                controller.RemoveLayer(i);
            }
        }

        // Thresholds are the positions given: walking at 1, running at 2.
        private static BlendTree Tree(AnimatorController controller, string name, string x, string y, List<(AnimationClip clip, Vector2 at)> motions)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = x,
                blendParameterY = y,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach ((AnimationClip clip, Vector2 at) in motions) tree.AddChild(clip, at);
            return tree;
        }

        // One layer: Empty (default) plus a state per motion, no transitions.
        // Empty and the states without Write Defaults, as the R&D layers.
        private static void AddLayer(AnimatorController controller, string layerName, AvatarMask mask, params (string state, Motion motion)[] states)
        {
            var machine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);

            AnimatorState empty = machine.AddState("Empty", new Vector3(300f, 0f, 0f));
            empty.writeDefaultValues = false;
            machine.defaultState = empty;

            float y = 80f;
            foreach ((string stateName, Motion motion) in states)
            {
                AnimatorState s = machine.AddState(stateName, new Vector3(300f, y, 0f));
                y += 80f;
                s.motion = motion;
                s.writeDefaultValues = false;
            }

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                stateMachine = machine,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
            });
        }

        // Added last, so above everything: put back under the first R&D layer.
        private static void MoveBelowRnD(AnimatorController controller, string[] ours)
        {
            List<AnimatorControllerLayer> layers = controller.layers.ToList();
            List<AnimatorControllerLayer> moved = layers.Where(l => ours.Contains(l.name)).ToList();
            layers.RemoveAll(l => ours.Contains(l.name));
            int at = layers.FindIndex(l => l.name == LocalHitGauge.DazedUpperLayer);
            layers.InsertRange(at >= 0 ? at : layers.Count, moved);
            controller.layers = layers.ToArray();
        }
    }
}
#endif
