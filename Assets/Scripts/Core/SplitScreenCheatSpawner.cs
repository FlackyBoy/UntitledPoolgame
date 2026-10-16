using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;

namespace UntitledPoolGame.Core
{
    // Test-only cheats for split-screen with a single keyboard:
    // - C+P: force-join another local player, without a second physical
    //   device. It gets a free gamepad if one is connected, otherwise the
    //   keyboard and mouse, taken from their owner (who stays put until Tab
    //   gives them back). Never joined without a device: PlayerInput then
    //   has no InputUser, its actions read every device (it moved with
    //   player 1's keyboard) and it can't be given one later (Invalid user);
    // - Tab: hands the keyboard and mouse to the next player (by index), so
    //   one person can play both sides. The player losing them gets the
    //   gamepad the other one had, if any.
    // The debug menu has the same actions as buttons (JoinPlayer,
    // GiveKeyboardTo). Created automatically in any scene with a
    // PlayerInputManager (it used to be placed by hand in the R&D arena only,
    // so C+P did nothing anywhere else).
    public class SplitScreenCheatSpawner : MonoBehaviour
    {
        private const string KeyboardScheme = "Keyboard&Mouse";
        private const string GamepadScheme = "Gamepad";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        private static void TryCreate()
        {
            if (FindAnyObjectByType<PlayerInputManager>() == null || FindAnyObjectByType<SplitScreenCheatSpawner>() != null) return;
            new GameObject("Triche C+P / Tab (joueurs)").AddComponent<SplitScreenCheatSpawner>();
        }

        [SerializeField] private PlayerInputManager playerInputManager;

        private bool comboWasActive;

        private void Awake()
        {
            if (playerInputManager == null)
                playerInputManager = FindAnyObjectByType<PlayerInputManager>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool comboActive = keyboard.cKey.isPressed && keyboard.pKey.isPressed;
            if (comboActive && !comboWasActive)
                Debug.Log($"[SplitScreenCheat] {JoinPlayer(playerInputManager)}");
            comboWasActive = comboActive;

            if (keyboard.tabKey.wasPressedThisFrame && PlayerInput.all.Count > 1)
                Debug.Log($"[SplitScreenCheat] {GiveKeyboardTo(NextAfter(KeyboardOwner()))}");
        }

        // Joins one more player; returns what happened, for the console or
        // the debug menu.
        public static string JoinPlayer(PlayerInputManager manager = null)
        {
            if (manager == null) manager = PlayerInputManager.instance;
            if (manager == null) return "Pas de PlayerInputManager dans la scène.";
            // -1 = no limit (the bar's setting): the old check compared
            // against it and always refused.
            if (manager.maxPlayerCount >= 0 && manager.playerCount >= manager.maxPlayerCount)
                return $"Déjà {manager.playerCount} joueurs (Max Player Count du PlayerInputManager).";

            Gamepad pad = FreeGamepad();
            if (pad != null)
            {
                PlayerInput withPad = manager.JoinPlayer(-1, -1, GamepadScheme, pad);
                return withPad != null ? $"J{withPad.playerIndex + 1} ajouté, avec la manette {pad.displayName}." : "Le joueur n'a pas pu être ajouté (voir la console).";
            }

            if (Keyboard.current == null) return "Ni manette libre ni clavier : impossible d'ajouter un joueur.";
            // The owner keeps its InputUser, with no device: it stays put.
            PlayerInput owner = KeyboardOwner();
            if (owner != null && owner.user.valid) owner.user.UnpairDevices();
            PlayerInput joined = manager.JoinPlayer(-1, -1, KeyboardScheme, KeyboardAndMouse());
            if (joined == null)
            {
                if (owner != null && owner.user.valid) owner.SwitchCurrentControlScheme(KeyboardScheme, KeyboardAndMouse());
                return "Le joueur n'a pas pu être ajouté (voir la console).";
            }
            return owner != null
                ? $"J{joined.playerIndex + 1} ajouté, avec le clavier (J{owner.playerIndex + 1} attend) : Tab pour changer de joueur."
                : $"J{joined.playerIndex + 1} ajouté, avec le clavier.";
        }

        // Moves the keyboard and mouse to target; its previous owner gets
        // target's gamepad in exchange, if it had one. activate false: from
        // the debug menu, which turns every player's input back on itself
        // when it closes (on now, the menu's clicks would move the player).
        public static string GiveKeyboardTo(PlayerInput target, bool activate = true)
        {
            if (target == null) return "Aucun joueur à qui passer le clavier.";
            if (Keyboard.current == null) return "Pas de clavier.";
            PlayerInput owner = KeyboardOwner();
            if (owner == target) return $"J{target.playerIndex + 1} a déjà le clavier.";
            // Joined without any device (before the fix above): no InputUser,
            // nothing can be paired to it.
            if (!target.user.valid) return $"J{target.playerIndex + 1} n'a pas d'utilisateur Input System : le retirer et le rajouter avec C+P.";

            Gamepad targetPad = null;
            foreach (InputDevice device in target.devices)
                if (device is Gamepad pad) targetPad = pad;

            // Unpaired first: a device can be paired to several users, and
            // both players would then move with the keyboard.
            target.user.UnpairDevices();
            if (owner != null && owner.user.valid)
            {
                owner.user.UnpairDevices();
                if (targetPad != null) owner.SwitchCurrentControlScheme(GamepadScheme, targetPad);
            }

            target.SwitchCurrentControlScheme(KeyboardScheme, KeyboardAndMouse());
            if (activate) target.ActivateInput();
            return owner != null && targetPad != null
                ? $"Clavier à J{target.playerIndex + 1}, manette à J{owner.playerIndex + 1}."
                : $"Clavier à J{target.playerIndex + 1}.";
        }

        // The player the keyboard is paired with, or null.
        public static PlayerInput KeyboardOwner()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return null;
            foreach (PlayerInput player in PlayerInput.all)
                foreach (InputDevice device in player.devices)
                    if (device == keyboard) return player;
            return null;
        }

        // The next player by index after this one (wrapping), or the first.
        private static PlayerInput NextAfter(PlayerInput current)
        {
            PlayerInput first = null, next = null;
            foreach (PlayerInput player in PlayerInput.all)
            {
                if (first == null || player.playerIndex < first.playerIndex) first = player;
                if (current != null && player.playerIndex > current.playerIndex
                    && (next == null || player.playerIndex < next.playerIndex)) next = player;
            }
            return next != null ? next : first;
        }

        private static Gamepad FreeGamepad()
        {
            foreach (Gamepad pad in Gamepad.all)
                if (InputUser.FindUserPairedToDevice(pad) == null) return pad;
            return null;
        }

        private static InputDevice[] KeyboardAndMouse() =>
            Mouse.current != null ? new InputDevice[] { Keyboard.current, Mouse.current } : new InputDevice[] { Keyboard.current };
    }
}
