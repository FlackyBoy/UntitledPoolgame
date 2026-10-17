using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Keeps the players' bodies and the cue out of the balls' physics. In
    // code because the Layer Collision Matrix wouldn't toggle in the editor.
    // Shots aren't affected, they're applied as a force.
    public static class PhysicsLayerSetup
    {
        private const string BallLayer = "Poolball";
        private static readonly string[] IgnoredAgainstBalls = { "PlayerAnimated", "Ragdoll", "Cuestick" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            int ball = LayerMask.NameToLayer(BallLayer);
            if (ball < 0)
            {
                Debug.LogWarning($"[PhysicsLayerSetup] Layer '{BallLayer}' doesn't exist — nothing ignored.");
                return;
            }

            foreach (string name in IgnoredAgainstBalls)
            {
                int other = LayerMask.NameToLayer(name);
                if (other < 0)
                {
                    Debug.LogWarning($"[PhysicsLayerSetup] Layer '{name}' doesn't exist — skipped.");
                    continue;
                }
                Physics.IgnoreLayerCollision(ball, other, true);
            }
        }
    }
}
