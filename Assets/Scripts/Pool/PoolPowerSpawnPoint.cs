using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A spot where a crate can spawn. Scattered on the table by
    // PoolTableBuilder, can be moved or added by hand.
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
