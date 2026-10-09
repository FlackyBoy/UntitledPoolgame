using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Everything the loading screen (LevelLoader, variant B « Le retour des
    // billes ») shows: texts, tips, the silly status lines, timings.
    // Assets/Resources/LoadingScreenSettings, created by Tools > Pool >
    // Ensure Config Assets Exist. The level picture and twist come from
    // GameFlowSettings. Tooltips in French: read by the designer.
    [CreateAssetMenu(fileName = "LoadingScreenSettings", menuName = "Pool/Loading Screen Settings")]
    public class LoadingScreenSettings : ScriptableObject
    {
        [Header("Textes")]
        [Tooltip("Tampon sur la carte postale.")]
        public string stampText = "EN ROUTE";
        [Tooltip("Titre du cube des astuces.")]
        public string tipTitle = "Astuce";
        [Tooltip("Quand tout est chargé.")]
        public string readyText = "Tout est prêt !";
        [Tooltip("{0} = numéro du joueur, {1} = touche à presser (A, Entrée…).")]
        public string pressFormat = "J{0} : appuie sur {1}";
        [Tooltip("{0} = numéro du joueur.")]
        public string readyFormat = "J{0} : prêt !";
        [Tooltip("Quand tout le monde est prêt, juste avant la partie.")]
        public string goText = "C'est parti !";
        [Tooltip("Étiquette du haut. {0} = mode, {1} = nombre de joueurs.")]
        public string contextFormat = "{0} · {1}";
        public string onePlayerText = "1 joueur";
        [Tooltip("{0} = nombre de joueurs.")]
        public string playersFormat = "{0} joueurs";

        [Header("Astuces (une au hasard, puis à tour de rôle)")]
        [TextArea(1, 3)]
        public string[] tips =
        {
            "Relâche le tir quand la jauge tremble : c'est la puissance maximale.",
            "Trop loin de la bille ? Ton personnage s'allonge sur la table… dans la limite du raisonnable.",
            "Un pouvoir en stock ? F / B pour le lancer pendant ton tour.",
            "Un coup de queue chargé envoie l'adversaire au tapis. Le billard, c'est aussi un sport de contact.",
            "La bille destructrice épargne la 8. Pas tes amis.",
        };

        [Header("Messages pendant le chargement (à tour de rôle)")]
        public string[] statusMessages =
        {
            "On cire les queues…", "On aligne les billes…", "On allume les néons…", "On cache la craie…",
            "On réveille le barman…", "On compte les billes… il en manque une.", "On remet la 8 au milieu…",
        };

        [Header("Déroulé")]
        [Tooltip("Chaque joueur appuie sur sa touche avant que la partie démarre. Décoché : la partie démarre dès que c'est chargé.")]
        public bool waitForPlayers = true;
        [Tooltip("Durée minimale (secondes) de l'écran, même si le niveau charge plus vite (sinon il clignote).")]
        public float minimumDuration = 2.5f;
        [Tooltip("Secondes entre deux astuces.")]
        public float tipInterval = 3.6f;
        [Tooltip("Secondes entre deux messages.")]
        public float statusInterval = 1.7f;
        [Tooltip("Durée (secondes) de « C'est parti ! » avant que la partie démarre.")]
        public float goDuration = 1.1f;

        private static LoadingScreenSettings instance;
        public static LoadingScreenSettings Instance =>
            instance != null ? instance : instance = Pool.PoolSettingsLoader.LoadOrDefault<LoadingScreenSettings>("LoadingScreenSettings");
    }
}
