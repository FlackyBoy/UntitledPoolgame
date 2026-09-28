using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Keeps the players' bodies and the cue out of the balls' physics.
    // Set in code because the Layer Collision Matrix checkboxes wouldn't
    // toggle in the editor (2026-09-25); Physics.IgnoreLayerCollision holds
    // for the whole session, whatever scene is loaded, and doesn't need
    // anything added to a scene. Shots aren't affected: LocalPoolAimController
    // applies the strike as a force, not through the cue's collider.
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
