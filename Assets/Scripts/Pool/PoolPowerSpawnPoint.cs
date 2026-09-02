using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A candidate location for PoolPowerCrateManager to spawn/respawn a
    // crate at — plain position marker, same idea as PoolPocket being a
    // marked location rather than doing anything on its own. Auto-scattered
    // across the table by PoolTableBuilder, but freely movable/addable by
    // hand afterward (e.g. once the environment-exploration TODO item adds
    // spawn points off the table too).
    public class PoolPowerSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.08f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.2f);
        }
    }
}
