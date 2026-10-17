using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Base of a power: picked up (power ball or crate), kept in stock (two
    // slots, PoolMatchRules.GrantPower) and launched with the power button.
    // One ScriptableObject asset per power, a new power is a subclass and
    // an asset.
    public abstract class PoolPower : ScriptableObject
    {
        [SerializeField] private string powerName = "Power";
        public string PowerName => powerName;

        // Token shown on the table for this power. Empty = the category's
        // prefab, else a tinted cube.
        [Tooltip("Jeton posé sur la table pour ce pouvoir (généré par Tools > Pool > Générer les jetons de pouvoir). Vide = le prefab de la catégorie dans PoolPowerSpawnSettings.")]
        [SerializeField] private GameObject tokenPrefab;
        public GameObject TokenPrefab => tokenPrefab;

        // HUD icon, white on transparent. Empty = the category's icon.
        [Tooltip("Icône du pouvoir dans le HUD (blanche sur fond transparent ; générée par Tools > Pool > Générer les jetons de pouvoir). Vide = l'icône de sa catégorie.")]
        [SerializeField] private Texture2D hudIcon;
        public Texture2D HudIcon => hudIcon;

        public abstract PowerType Type { get; }

        // Only on the holder's own turn (checked in PoolMatchRules.CheckPowerUse,
        // split screen only). Set per power: the category doesn't tell
        // (a Defense reacting to the opponent's shot needs it off).
        [SerializeField] private bool requiresOwnTurn = true;
        public bool RequiresOwnTurn => requiresOwnTurn;

        // For a power usable off-turn: launched during one's own turn it
        // either waits for the opponent's turn or is refused.
        [Tooltip("Pouvoir utilisable hors de son tour (Requires Own Turn décoché) : coché = lancé pendant son propre tour, il attend le début du tour adverse ; décoché = refusé pendant son tour, il ne part que pendant le tour de l'adversaire.")]
        [SerializeField] private bool allowDuringOwnTurn = true;
        public bool AllowDuringOwnTurn => allowDuringOwnTurn;

        // Closing a pocket right before a ball drops in is part of the chaos;
        // a power for which that would be unfair unchecks this.
        [Tooltip("Peut être lancé pendant que les billes roulent (tir en cours). Décoché = refusé jusqu'à l'arrêt des billes.")]
        [SerializeField] private bool allowWhileBallsMoving = true;
        public bool AllowWhileBallsMoving => allowWhileBallsMoving;

        // Who feels it: the opponent for an Attack, the holder otherwise.
        // Drives the HUD announcement (PoolMatchRules.PowerActivated).
        public bool TargetsOpponent => Type == PowerType.Attack;

        // Called the moment the holding player activates it.
        // activatingPlayer is 0 or 1 — PoolMatchRules.CurrentPlayer indexing.
        public abstract void Activate(PoolMatchRules match, int activatingPlayer);
    }
}
