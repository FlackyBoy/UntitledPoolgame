using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Who plays with which device. Replaces PlayerInputManager's "join on
    // button press": while it listens, Unity turns off a lone player's device
    // switching (PlayerInput.isSinglePlayer is false), so picking up a
    // gamepad didn't move P1 and its first button spawned P2.
    // - Alone, the player follows the device last used: keyboard and mouse,
    //   or a free gamepad (hotseat, or picking up a pad mid-game).
    // - A free gamepad's button joins one more player once the player(s)
    //   already have one, during the match (up to Max Player Count, 2 when
    //   unlimited). A player left without a device (C+P cheat) gets it first.
    // - The menu and the loading screen hand out their own devices through
    //   Assign; nobody joins by button after that.
    // Created in any scene with a PlayerInputManager.
    public class PlayerDeviceRouter : MonoBehaviour
    {
        public const string KeyboardScheme = "Keyboard&Mouse";
        public const string GamepadScheme = "Gamepad";
        private const int DefaultMaxPlayers = 2;
        private const float StickThreshold = 0.5f;

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
            if (FindAnyObjectByType<PlayerInputManager>() == null || FindAnyObjectByType<PlayerDeviceRouter>() != null) return;
            new GameObject("Appareils des joueurs").AddComponent<PlayerDeviceRouter>();
        }

        // Set by Assign: the devices were chosen in the menu.
        private static bool joinLocked;

        private PlayerInputManager manager;
        private bool joinOnPress;

        private void Awake()
        {
            joinLocked = false;
            manager = FindAnyObjectByType<PlayerInputManager>();
            // Runs after the scene's OnEnable: the manager already listens,
            // DisableJoining unhooks it.
            joinOnPress = manager != null && manager.joinBehavior == PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed && manager.joiningEnabled;
            if (joinOnPress) manager.DisableJoining();
        }

        private void Update()
        {
            var players = PlayerInput.all;
            // Ours, not Unity's: alone, it would hand P1 a second pad before
            // that pad could join.
            for (int i = 0; i < players.Count; i++) players[i].neverAutoSwitchControlSchemes = true;

            Gamepad pressedPad = null, movedPad = null;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (IsPaired(pad)) continue;
                if (ButtonPressed(pad)) { pressedPad = pad; break; }
                if (movedPad == null && StickMoved(pad)) movedPad = pad;
            }
            bool keyboardUsed = KeyboardUsed();

            if (players.Count == 0)
            {
                if (!CanJoin(0)) return;
                if (pressedPad != null) manager.JoinPlayer(-1, -1, GamepadScheme, pressedPad);
                else if (keyboardUsed) manager.JoinPlayer(-1, -1, KeyboardScheme, KeyboardAndMouse());
                return;
            }

            if (pressedPad != null)
            {
                PlayerInput idle = PlayerWithoutDevice();
                if (idle != null) Give(idle, GamepadScheme, pressedPad);
                else if (CanJoin(players.Count) && !(players.Count == 1 && !HasGamepad(players[0])))
                    manager.JoinPlayer(-1, -1, GamepadScheme, pressedPad);
                else if (players.Count == 1) Give(players[0], GamepadScheme, pressedPad);
                return;
            }

            if (players.Count != 1) return;
            PlayerInput solo = players[0];
            // A stick only takes a pad for a player without one: it must not
            // steal P1 from the pad in use.
            if (movedPad != null && !HasGamepad(solo)) Give(solo, GamepadScheme, movedPad);
            else if (keyboardUsed) Give(solo, KeyboardScheme, KeyboardAndMouse());
        }

        // Pairs the devices with the players in order (a gamepad alone, the
        // keyboard with the mouse): players already there are reused, missing
        // ones joined. No joining by button afterwards.
        public static void Assign(IReadOnlyList<InputDevice> devices)
        {
            joinLocked = true;
            PlayerInputManager manager = PlayerInputManager.instance;
            if (manager != null) manager.DisableJoining();

            var existing = new List<PlayerInput>(PlayerInput.all);
            existing.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
            // A device can be paired to several users: freed first, so it
            // doesn't drive two players.
            foreach (PlayerInput player in existing)
                if (player.user.valid) player.user.UnpairDevices();

            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice device = devices[i];
                string scheme = device is Gamepad ? GamepadScheme : KeyboardScheme;
                InputDevice[] paired = device is Gamepad ? new[] { device } : KeyboardAndMouse();
                if (i < existing.Count)
                {
                    existing[i].SwitchCurrentControlScheme(scheme, paired);
                    existing[i].ActivateInput();
                }
                else if (manager != null)
                    manager.JoinPlayer(-1, -1, scheme, paired);
                else
                    Debug.LogWarning("[Devices] Pas de PlayerInputManager dans le niveau : le joueur " + (i + 1) + " ne peut pas être ajouté.");
            }
        }

        private bool CanJoin(int playerCount)
        {
            if (!joinOnPress || joinLocked || manager == null || PauseMenu.IsPaused || LevelLoader.Busy) return false;
            // Not behind the menu of a level played from the editor.
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null && !rules.MatchStarted) return false;
            int max = manager.maxPlayerCount >= 0 ? manager.maxPlayerCount : DefaultMaxPlayers;
            return playerCount < max;
        }

        private static void Give(PlayerInput player, string scheme, params InputDevice[] devices)
        {
            // Joined without a device: no InputUser, nothing can be paired.
            if (!player.user.valid) return;
            player.SwitchCurrentControlScheme(scheme, devices);
            Debug.Log($"[Devices] J{player.playerIndex + 1} : {devices[0].displayName}.");
        }

        private static PlayerInput PlayerWithoutDevice()
        {
            foreach (PlayerInput player in PlayerInput.all)
                if (player.user.valid && player.devices.Count == 0) return player;
            return null;
        }

        private static bool HasGamepad(PlayerInput player)
        {
            foreach (InputDevice device in player.devices)
                if (device is Gamepad) return true;
            return false;
        }

        private static bool IsPaired(InputDevice device) => InputUser.FindUserPairedToDevice(device) != null;

        private static bool ButtonPressed(Gamepad pad) =>
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
            || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
            || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;

        private static bool StickMoved(Gamepad pad) =>
            pad.leftStick.ReadValue().sqrMagnitude > StickThreshold * StickThreshold
            || pad.rightStick.ReadValue().sqrMagnitude > StickThreshold * StickThreshold
            || pad.dpad.ReadValue().sqrMagnitude > StickThreshold * StickThreshold;

        // A key or a click on a free keyboard / mouse. Not mouse motion: a
        // bumped mouse would take P1 off its pad.
        private static bool KeyboardUsed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsPaired(keyboard)) return false;
            if (keyboard.anyKey.wasPressedThisFrame) return true;
            Mouse mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        }

        public static InputDevice[] KeyboardAndMouse() =>
            Mouse.current != null ? new InputDevice[] { Keyboard.current, Mouse.current } : new InputDevice[] { Keyboard.current };
    }
}
