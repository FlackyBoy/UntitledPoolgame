using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Test cheat: hold C+W to jump to "group cleared, next shot is the 8"
    // (removes the solids and stripes, assigns the group). Put it on any
    // object of a scene with a PoolMatchRules.
    public class EightBallEndgameCheat : MonoBehaviour
    {
        private bool comboWasActive;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Keys are named by their QWERTY position: the W of an AZERTY
            // keyboard is reported as zKey.
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
