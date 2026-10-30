#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // Tools > Pool > Play Anywhere > Ajouter la pose allongée au joueur: adds
    // to the player's Animator Controller a full-body layer (weight 0,
    // driven from code by LocalPoolAimController) whose "Prone" state loops
    // EverydayMotionPack's male_idle_on_floor: the lying stance while
    // aiming. Empty is the default state and writes nothing, so at weight 0
    // nothing changes. Run again to rebuild.
    public static class PlayAnywherePoseTools
    {
        private const string PlayerPrefab = "Assets/Prefabs/PlayerLocal/New.prefab";
        private const string ProneClipFile = "@male_idle_on_floor.FBX";

        [MenuItem("Tools/Pool/Play Anywhere/Ajouter la pose allongée au joueur", priority = 70)]
        private static void AddProneLayer()
        {
            AnimatorController controller = FindController(out string error);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Pose allongée", error, "OK");
                return;
            }
            AnimationClip clip = LoadClip();
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Pose allongée", $"Animation {ProneClipFile} introuvable (pack EverydayMotionPack).", "OK");
                return;
            }

            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = layers.Length - 1; i >= 0; i--)
                if (layers[i].name == LocalPoolAimController.ProneLayerName) controller.RemoveLayer(i);

            var machine = new AnimatorStateMachine { name = LocalPoolAimController.ProneLayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            AnimatorState empty = machine.AddState("Empty", new Vector3(300f, 0f, 0f));
            empty.writeDefaultValues = false;
            machine.defaultState = empty;
            AnimatorState prone = machine.AddState(LocalPoolAimController.ProneStateName, new Vector3(300f, 80f, 0f));
            prone.motion = clip;
            prone.writeDefaultValues = false;

            // Last: over every other layer (combat ones included), whole body.
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = LocalPoolAimController.ProneLayerName,
                stateMachine = machine,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
            });
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayAnywhere] Calque « {LocalPoolAimController.ProneLayerName} » ajouté à {AssetDatabase.GetAssetPath(controller)} (état {LocalPoolAimController.ProneStateName} : {clip.name}).", controller);
        }

        private static AnimatorController FindController(out string error)
        {
            error = null;
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (player == null) { error = $"Prefab du joueur introuvable : {PlayerPrefab}."; return null; }
            Animator animator = player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController != null && a.avatar != null && a.avatar.isHuman);
            if (animator == null) { error = "Aucun Animator humanoïde avec un contrôleur sur le prefab du joueur."; return null; }
            RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
            if (runtime is AnimatorOverrideController overrideController) runtime = overrideController.runtimeAnimatorController;
            var controller = runtime as AnimatorController;
            if (controller == null) error = $"Le contrôleur « {runtime.name} » n'est pas un Animator Controller modifiable.";
            return controller;
        }

        private static AnimationClip LoadClip()
        {
            string guid = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(ProneClipFile).TrimStart('@'))
                .FirstOrDefault(g => AssetDatabase.GUIDToAssetPath(g).EndsWith("/" + ProneClipFile));
            if (guid == null) return null;
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }
    }
}
#endif
