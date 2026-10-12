#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.PoolEditor
{
    // Part of Tools > Pool > R&D > Préparer le joueur de R&D (RnDArenaTools):
    // adds the R&D combat layers to the R&D player's Animator Controller,
    // all at weight 0
    // and driven from code (CrossFade + layer weight, no transitions):
    // - "R&D Sonné (haut)" / "R&D Sonné (jambes)": the Stunned loop, upper
    //   body and legs separately (LocalHitGauge: the legs only while
    //   standing still, so walking keeps its legs);
    // - "R&D Combat" (CombatSettings.Animation Layer): guard, punches, and
    //   the held guard (Block, Blocked), upper body without the head
    //   (LocalUnarmedMelee, Animated and Block);
    // - "R&D Coup reçu": GetHit reactions, light and heavy, from the front,
    //   back, left and right (LocalHitGauge), upper body with the head.
    // In that order, so a blow taken overrides a punch being thrown, and a
    // punch overrides the stunned sway. The controller is shared with the
    // normal player and the dummies: at weight 0 the layers change nothing.
    // Run again to rebuild them (removed and recreated). The PROTOFACTOR
    // clips are only referenced, never modified (third-party pack).
    public static class RnDCombatAnimationTools
    {
        private const string VariantPath = RnDArenaTools.VariantPath;
        private const string ClipFolder = "Assets/PROTOFACTOR/UltimateAnimationCollection/Animations/Combat Bare Fists Animset/FBX Motions";
        private const string MaskFolder = "Assets/Animations/Combat";
        private const string SpeedParameter = "PunchSpeed";

        private enum Mask { UpperBody, UpperBodyAndHead, Legs }

        // Builds (or rebuilds) the layers. False, with a dialog, if the
        // controller or a clip is missing.
        public static bool BuildLayers()
        {
            AnimatorController controller = FindController(out string error);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Animations de coups", error, "OK");
                return false;
            }

            var missing = new List<string>();
            AnimationClip Clip(string file)
            {
                AnimationClip c = LoadClip(file);
                if (c == null) missing.Add(file);
                return c;
            }
            AnimationClip guard = Clip("Humanoid@IdleCombat.fbx");
            AnimationClip left = Clip("Humanoid@Jab1.fbx");
            AnimationClip right = Clip("Humanoid@RightHand1.fbx");
            AnimationClip stunned = Clip("Humanoid@Stunned.fbx");
            AnimationClip block = Clip("Humanoid@IdleBlockHigh.fbx");
            AnimationClip blocked = Clip("Humanoid@BlockedHigh.fbx");
            var hits = new List<(string state, AnimationClip clip)>();
            foreach (string weight in new[] { "Light", "Heavy" })
                foreach (string side in new[] { "Front", "Back", "Left", "Right" })
                    hits.Add(($"Hit{weight}{side}", Clip($"Humanoid@GetHit{side}{weight}Combat.fbx")));
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog("Animations de coups", $"Animations introuvables dans {ClipFolder} :\n{string.Join("\n", missing)}", "OK");
                return false;
            }

            string combatLayer = CombatSettings.Instance.animationLayer;
            string[] ours = { LocalHitGauge.DazedUpperLayer, LocalHitGauge.DazedLegsLayer, combatLayer, LocalHitGauge.HitLayer };

            // Rebuilt from scratch every time: idempotent, and a hand-edited
            // layer can't drift from what the code expects.
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = layers.Length - 1; i >= 0; i--)
                if (ours.Contains(layers[i].name)) controller.RemoveLayer(i);

            if (!controller.parameters.Any(p => p.name == SpeedParameter))
                controller.AddParameter(new AnimatorControllerParameter { name = SpeedParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AddLayer(controller, LocalHitGauge.DazedUpperLayer, EnsureMask(Mask.UpperBodyAndHead), new[] { ("Stunned", stunned) }, null);
            AddLayer(controller, LocalHitGauge.DazedLegsLayer, EnsureMask(Mask.Legs), new[] { ("Stunned", stunned) }, null);
            AddLayer(controller, combatLayer, EnsureMask(Mask.UpperBody), new[] { ("Guard", guard), ("PunchL", left), ("PunchR", right), ("Block", block), ("Blocked", blocked) }, new[] { "PunchL", "PunchR" });
            AddLayer(controller, LocalHitGauge.HitLayer, EnsureMask(Mask.UpperBodyAndHead), hits.ToArray(), null);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[R&D] Calques créés dans {AssetDatabase.GetAssetPath(controller)} : {string.Join(", ", ours)} " +
                      $"(garde {guard.name}, poings {left.name} / {right.name}, blocage {block.name} / {blocked.name}, sonné {stunned.name}, {hits.Count} réactions aux coups). Masques dans {MaskFolder}.", controller);
            return true;
        }

        // One layer: Empty (default) plus a state per clip, no transitions.
        // States in withSpeed play at the PunchSpeed parameter.
        private static void AddLayer(AnimatorController controller, string layerName, AvatarMask mask,
            (string state, AnimationClip clip)[] states, string[] withSpeed)
        {
            var machine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);

            // Empty, without Write Defaults: writes nothing, so the layers
            // below show through even if the weight isn't exactly 0.
            AnimatorState empty = machine.AddState("Empty", new Vector3(300f, 0f, 0f));
            empty.writeDefaultValues = false;
            machine.defaultState = empty;

            float y = 80f;
            foreach ((string stateName, AnimationClip clip) in states)
            {
                AnimatorState s = machine.AddState(stateName, new Vector3(300f, y, 0f));
                y += 80f;
                s.motion = clip;
                s.writeDefaultValues = false;
                if (withSpeed != null && withSpeed.Contains(stateName))
                {
                    s.speedParameter = SpeedParameter;
                    s.speedParameterActive = true;
                }
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

        // The controller of the variant's character Animator (the one
        // LocalCharacterAnimationController drives).
        private static AnimatorController FindController(out string error)
        {
            error = null;
            GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            if (variant == null) { error = $"Variante du joueur introuvable : {VariantPath}."; return null; }

            Animator animator = null;
            var animControl = variant.GetComponent<LocalCharacterAnimationController>();
            if (animControl != null)
                animator = new SerializedObject(animControl).FindProperty("animator")?.objectReferenceValue as Animator;
            if (animator == null)
                animator = variant.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController != null);
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                error = "Aucun Animator avec un contrôleur d'animation sur la variante du joueur.";
                return null;
            }

            RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
            if (runtime is AnimatorOverrideController overrideController) runtime = overrideController.runtimeAnimatorController;
            var controller = runtime as AnimatorController;
            if (controller == null) error = $"Le contrôleur « {runtime.name} » n'est pas un Animator Controller modifiable.";
            return controller;
        }

        private static AnimationClip LoadClip(string file)
        {
            string path = $"{ClipFolder}/{file}";
            if (!File.Exists(path))
            {
                // Pack moved: look it up by name.
                string guid = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(file))
                    .FirstOrDefault(g => AssetDatabase.GUIDToAssetPath(g).EndsWith("/" + file));
                if (guid == null) return null;
                path = AssetDatabase.GUIDToAssetPath(guid);
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        // - UpperBody (punches): spine, arms, fingers. Not the head (it keeps
        //   facing where the player looks), nor the legs and root (the walk
        //   keeps them).
        // - UpperBodyAndHead (stunned sway, blows taken): the same plus the
        //   head — a blow to the face has to move it.
        // - Legs (stunned sway while standing still): legs and feet IK.
        private static AvatarMask EnsureMask(Mask kind)
        {
            string path = kind switch
            {
                Mask.UpperBody => $"{MaskFolder}/RnD_UpperBody.mask",
                Mask.UpperBodyAndHead => $"{MaskFolder}/RnD_UpperBodyHead.mask",
                _ => $"{MaskFolder}/RnD_Legs.mask",
            };
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            bool created = mask == null;
            if (created)
            {
                EnsureFolder(MaskFolder);
                mask = new AvatarMask();
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body
                             || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                             || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                bool legs = part == AvatarMaskBodyPart.LeftLeg || part == AvatarMaskBodyPart.RightLeg
                            || part == AvatarMaskBodyPart.LeftFootIK || part == AvatarMaskBodyPart.RightFootIK;
                bool on = kind switch
                {
                    Mask.UpperBody => upper,
                    Mask.UpperBodyAndHead => upper || part == AvatarMaskBodyPart.Head,
                    _ => legs,
                };
                mask.SetHumanoidBodyPartActive(part, on);
            }
            if (created) AssetDatabase.CreateAsset(mask, path);
            else EditorUtility.SetDirty(mask);
            return mask;
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
