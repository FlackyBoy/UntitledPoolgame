using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Marks a piece placed by the Level Maker (wall module, tile, prop) so
    // its tools can find it again: clicking a wall module to turn it into a
    // door, removing a painted prop. No behaviour.
    public class RoomModule : MonoBehaviour
    {
        public RoomModuleKind kind;
        public int segment = -1;
        public int cell = -1;
    }
}
