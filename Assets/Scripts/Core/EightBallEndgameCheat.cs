using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Test-only cheat: hold C+W together to skip straight to "the current
    // player's group is cleared, next legal shot is the 8" — deactivates
    // every Solid/Stripe ball and assigns the current player's group, so the
    // 8-ball win condition and call-shot flow can be tested without playing
    // out a full rack first. Same idea/pattern as SplitScreenCheatSpawner's
    // C+P. Add this component to any GameObject in a scene with a
    // PoolMatchRules (it finds it automatically).
    public class EightBallEndgameCheat : MonoBehaviour
    {
        private bool comboWasActive;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Input System key controls are named by QWERTY PHYSICAL position,
            // not by the letter printed on the key — on an AZERTY board (see
            // the Move action's own ZQSD note elsewhere in this project),
            // the key actually labeled "W" sits where QWERTY's Z is, so it's
            // reported as zKey, not wKey.
            bool comboActive = keyboard.cKey.isPressed && keyboard.zKey.isPressed;

            if (comboActive && !comboWasActive)
            {
                if (PoolMatchRules.Instance != null)
                    PoolMatchRules.Instance.DebugForceEightBallEndgame();
                else
                    Debug.LogWarning("[EightBallEndgameCheat] No PoolMatchRules found in the scene.");
            }

            comboWasActive = comboActive;
        }
    }
}
