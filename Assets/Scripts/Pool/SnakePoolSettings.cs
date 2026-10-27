using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Snake Pool (SnakePoolRuleSet, SnakeChain): the chain of balls behind
    // each cue ball and how it breaks.
    [CreateAssetMenu(fileName = "SnakePoolSettings", menuName = "Pool/Snake Pool Settings")]
    public class SnakePoolSettings : ScriptableObject
    {
        [Header("Chain")]
        [Tooltip("Espace entre deux billes de la chaîne, en rayons de bille (0 = elles se touchent).")]
        [Range(0f, 2f)] public float linkGap = 0.15f;
        [Tooltip("Temps (secondes) que met une bille ramassée pour rejoindre sa place au bout de la chaîne.")]
        [Min(0.01f)] public float joinTime = 0.25f;
        [Tooltip("Hauteur du saut d'une bille qui ressort d'une poche pour rejoindre la chaîne, en rayons de bille.")]
        [Min(0f)] public float pocketHop = 3f;
        [Tooltip("La blanche garde sa vitesse quand elle ramasse une bille (elle la « mange » sans rebondir). Décoché : elle rebondit dessus comme au billard, puis la bille rejoint la chaîne.")]
        public bool keepSpeedOnCollect = true;

        [Header("Break")]
        [Tooltip("Vitesse minimale (m/s) de la blanche adverse pour briser la chaîne en la touchant. Une chaîne qui balaie une blanche arrêtée ne se brise pas.")]
        [Min(0f)] public float minBreakSpeed = 0.15f;
        [Tooltip("Part de la vitesse de la blanche adverse donnée à la bille touchée quand la chaîne se brise.")]
        [Range(0f, 2f)] public float breakKick = 0.7f;
        [Tooltip("Part de leur vitesse que gardent les billes lâchées par la chaîne (brisée, ou blanche rentrée).")]
        [Range(0f, 1f)] public float releasedSpeed = 1f;

        [Header("Finale")]
        [Tooltip("Vitesse (m/s) à laquelle la file s'engouffre dans la poche derrière la blanche, une fois la 8 au bout de la file. Plus lent = plus de temps pour que l'adversaire la brise.")]
        [Min(0.05f)] public float finaleSpeed = 1.2f;

        [Header("Scratch (cue ball potted)")]
        [Tooltip("Blanche rentrée sans la 8 au bout de la file : toute sa chaîne est lâchée sur le tapis. Décoché : la chaîne reste et repart avec la blanche quand elle revient.")]
        public bool scratchDropsChain = true;

        [Header("Debug")]
        [Tooltip("Lignes [SnakePool] dans la console : billes ramassées, chaînes brisées, victoire.")]
        public bool debugLogs = true;
    }
}
