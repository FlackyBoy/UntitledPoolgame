using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Marker: "this Grabbable (online) or LocalGrabbable (offline split-screen)
    // is a pool cue" — checked by PoolAimController/LocalPoolAimController
    // before allowing the player to enter aim mode. Not tied to a specific
    // Grabbable variant so the same marker works in both modes.
    public class Cue : MonoBehaviour
    {
    }
}
