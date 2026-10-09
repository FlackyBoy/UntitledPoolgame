using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Core
{
    // One playable level as the menu and the loading screen show it.
    [Serializable]
    public class LevelEntry
    {
        [Tooltip("Nom affiché (carte du menu, carte postale du chargement).")]
        public string displayName = "Le bar";
        [Tooltip("Scène Unity du niveau : glisser la scène ici ou la choisir avec le rond. Elle doit être dans la liste des scènes du build (bouton proposé sinon).")]
        [SceneName] public string sceneName = "BarSplitscreen";
        [Tooltip("Image du niveau (carte du menu et carte postale). Vide = dessin de remplacement.")]
        public Texture2D picture;
        [Tooltip("Phrase courte sous le nom, sur la carte du menu.")]
        public string menuLine = "Néons, billard, bagarre";
        [Tooltip("Le twist du décor, écrit sur la carte postale pendant le chargement.")]
        [TextArea(2, 4)] public string twist = "Le twist : les néons clignotent quand la partie s'emballe… et le barman lance des objets.";
        [Tooltip("Pas encore jouable : la carte porte le tampon « Bientôt ».")]
        public bool comingSoon;
    }

    // How the game goes from the main menu to a level: the menu's own scene,
    // the levels on offer (with their pictures and texts), and the player
    // actions the key bindings screen edits when nobody has joined yet (in
    // the menu scene). Assets/Resources/GameFlowSettings, created by Tools >
    // Pool > Ensure Config Assets Exist. Tooltips in French: read by the
    // designer in the Inspector.
    [CreateAssetMenu(fileName = "GameFlowSettings", menuName = "Pool/Game Flow Settings")]
    public class GameFlowSettings : ScriptableObject
    {
        [Tooltip("Scène du menu principal (créée par Tools > Pool > Create Main Menu Scene).")]
        [SceneName] public string menuScene = "MainMenu";

        [Tooltip("Niveaux proposés dans le menu, dans l'ordre d'affichage.")]
        public List<LevelEntry> levels = new List<LevelEntry>
        {
            new LevelEntry(),
            new LevelEntry { displayName = "La prison", sceneName = "", menuLine = "Lumières qui sautent… et un gardien",
                             twist = "Le twist : la lumière saute, et quelqu'un vous traque dans le noir.", comingSoon = true },
            new LevelEntry { displayName = "Station spatiale", sceneName = "", menuLine = "Billes en apesanteur",
                             twist = "Le twist : les billes flottent quand la gravité se coupe.", comingSoon = true },
        };

        [Tooltip("Actions des joueurs (InputSystem_Actions_Local) : ce que l'écran des touches modifie quand aucun joueur n'a encore rejoint.")]
        public InputActionAsset playerActions;

        private static GameFlowSettings instance;
        public static GameFlowSettings Instance =>
            instance != null ? instance : instance = Pool.PoolSettingsLoader.LoadOrDefault<GameFlowSettings>("GameFlowSettings");
    }
}
