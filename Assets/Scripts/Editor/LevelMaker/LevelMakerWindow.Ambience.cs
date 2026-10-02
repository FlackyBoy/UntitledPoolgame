#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // ⑥ Ambiance: the level's moods (AmbiencePreset) and their baked
    // lighting scenarios (Adaptive Probe Volumes), see AmbienceTools.
    public partial class LevelMakerWindow
    {
        private AmbiencePreset ambienceToAdd;

        private void DrawAmbienceStep()
        {
            EditorGUILayout.LabelField("Projet", EditorStyles.boldLabel);
            var assets = AmbienceTools.RenderAssets();
            bool apvReady = assets.Count > 0 && assets.All(AmbienceTools.ApvEnabled);
            foreach (UniversalRenderPipelineAsset asset in assets)
                EditorGUILayout.LabelField($"  {asset.name}", AmbienceTools.ApvEnabled(asset) ? "✓ APV + scénarios" : "✗ APV désactivé");
            if (!apvReady)
            {
                EditorGUILayout.HelpBox("Les ambiances précalculent leur éclairage indirect dans des Adaptive Probe Volumes, un scénario par ambiance. " +
                                        "Il faut activer APV et les scénarios d'éclairage dans les assets URP.", MessageType.Warning);
                if (GUILayout.Button("Activer APV et les scénarios d'éclairage")) { AmbienceTools.EnableApv(); MarkDirty(); }
            }
            else if (GUILayout.Button("Désactiver APV (retour aux Light Probe Groups)")
                     && EditorUtility.DisplayDialog("Désactiver APV",
                         "Remettre les assets URP sur les Light Probe Groups et couper les scénarios d'éclairage ? " +
                         "Les ambiances s'appliqueront toujours (ciel, lumières, post-process, brouillard) mais sans éclairage précalculé. " +
                         "Les données déjà précalculées restent sur le disque.", "Désactiver", "Annuler"))
            {
                AmbienceTools.DisableApv();
                MarkDirty();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scène", EditorStyles.boldLabel);
            AmbienceController controller = AmbienceTools.Controller;
            ProbeVolumeBakingSet set = AmbienceTools.BakingSet();
            EditorGUILayout.LabelField("  Contrôleur d'ambiance", controller != null ? "✓" : "—");
            EditorGUILayout.LabelField("  Volume de sondes global", AmbienceTools.GlobalProbeVolume != null ? "✓" : "—");
            EditorGUILayout.LabelField("  Baking set", set != null ? set.name : "—");
            if (GUILayout.Button(controller == null ? "Préparer la scène" : "Mettre à jour la préparation"))
            {
                AmbienceTools.SetUpScene(settings);
                MarkDirty();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.HelpBox("Prépare : contrôleur d'ambiance (lumière principale, volume global), volume de sondes global, " +
                                    "réglages d'éclairage (indirect seulement), baking set de la scène avec un scénario par ambiance, " +
                                    "et sol / murs posés qui prennent leur éclairage indirect des sondes.", MessageType.None);
            if (controller == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ambiances du niveau", EditorStyles.boldLabel);
            for (int i = 0; i < controller.presets.Count; i++)
            {
                AmbiencePreset preset = controller.presets[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    bool isActive = preset != null && preset == controller.active;
                    EditorGUILayout.LabelField(isActive ? "▶" : "", GUILayout.Width(14));
                    EditorGUILayout.ObjectField(preset, typeof(AmbiencePreset), false);
                    if (preset != null)
                    {
                        GUILayout.Label(AmbienceTools.IsBaked(preset) ? "précalculée" : "à précalculer", EditorStyles.miniLabel, GUILayout.Width(80));
                        using (new EditorGUI.DisabledScope(AmbienceTools.IsBaking))
                        {
                            if (GUILayout.Button("Appliquer", GUILayout.Width(70))) { AmbienceTools.ApplyInEditor(controller, preset); MarkDirty(); }
                            if (GUILayout.Button("Précalculer", GUILayout.Width(80))) AmbienceTools.Bake(controller, new[] { preset }, settings);
                        }
                    }
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        string name = preset != null ? preset.name : "cette ambiance";
                        if (preset == null || EditorUtility.DisplayDialog("Retirer l'ambiance",
                                $"Retirer « {name} » du niveau ? Son scénario d'éclairage et ses données précalculées seront supprimés " +
                                "du baking set (l'asset d'ambiance, lui, est gardé).", "Retirer", "Annuler"))
                        {
                            if (preset == null) controller.presets.RemoveAt(i);
                            else AmbienceTools.RemoveAmbience(controller, preset);
                            MarkDirty();
                        }
                        GUIUtility.ExitGUI();
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                ambienceToAdd = (AmbiencePreset)EditorGUILayout.ObjectField("Ajouter", ambienceToAdd, typeof(AmbiencePreset), false);
                using (new EditorGUI.DisabledScope(ambienceToAdd == null || controller.presets.Contains(ambienceToAdd)))
                    if (GUILayout.Button("+", GUILayout.Width(26)))
                    {
                        AddAmbience(controller, ambienceToAdd);
                        ambienceToAdd = null;
                    }
            }
            if (GUILayout.Button("Ajouter les ambiances de départ (Bar néon, Prison sombre, Jour neutre)"))
                foreach (AmbiencePreset preset in AmbienceTools.CreateDefaultPresets()) AddAmbience(controller, preset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Précalcul de l'éclairage", EditorStyles.boldLabel);
            if (AmbienceTools.IsBaking)
            {
                EditorGUILayout.HelpBox("Précalcul en cours (une ambiance après l'autre, progression en bas à droite de Unity)…", MessageType.Info);
                if (GUILayout.Button("Annuler le précalcul")) AmbienceTools.CancelBake();
                Repaint();
            }
            else
            {
                using (new EditorGUI.DisabledScope(!apvReady || controller.presets.Count == 0))
                    if (GUILayout.Button("Précalculer toutes les ambiances", GUILayout.Height(28)))
                        AmbienceTools.Bake(controller, controller.presets, settings);
                EditorGUILayout.HelpBox("Chaque ambiance est appliquée puis son éclairage indirect est précalculé dans son scénario ; " +
                                        "l'ambiance active est remise à la fin. À relancer après avoir modifié la salle ou une ambiance. " +
                                        "En jeu, AmbienceController.Apply / BlendTo change d'ambiance (dimensions, lot 6).", MessageType.None);
            }
        }

        private void AddAmbience(AmbienceController controller, AmbiencePreset preset)
        {
            if (preset == null || controller.presets.Contains(preset)) return;
            Undo.RecordObject(controller, "Ambiance");
            controller.presets.Add(preset);
            if (controller.active == null) controller.active = preset;
            AmbienceTools.BakingSet()?.TryAddScenario(preset.Scenario);
            EditorUtility.SetDirty(controller);
            MarkDirty();
        }
    }
}
#endif
