using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Base for a power aimed at a point of the table (TableHolePower), the
    // way PocketTargetPower is for a pocket: pressing "use power" starts a
    // pick (LocalPoolPowerController + LocalPocketPointer: the white pointer
    // on the felt, a ring previewing the area), Interact confirms, and
    // PoolMatchRules.TryActivatePower(player, point) passes the point on.
    public abstract class TablePointPower : PoolPower
    {
        // Fallback when activated without a pick (the frozen online
        // PoolPowerController): a random point of the felt.
        public sealed override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return;
            float margin = Margin(match.ActivatingPowerLevel);
            Vector3 local = new Vector3(
                Random.Range(-surface.HalfLength + margin, surface.HalfLength - margin), 0f,
                Random.Range(-surface.HalfWidth + margin, surface.HalfWidth - margin));
            Vector3 point = surface.transform.position + surface.transform.rotation * local;
            Activate(match, activatingPlayer, ClampToTable(point, margin));
        }

        public abstract void Activate(PoolMatchRules match, int activatingPlayer, Vector3 point);

        // How far from the cushions and pockets the point is kept.
        public abstract float Margin(int level);

        // Radius of the preview ring around the pointed spot.
        public abstract float PreviewRadius(int level);

        // Inside the felt (Margin from the cushions), out of the pockets, at
        // the felt's height.
        public static Vector3 ClampToTable(Vector3 point, float margin)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return point;
            Vector3 clamped = PoolPocket.AvoidAllPockets(surface.ClampToPlayArea(point, margin), margin);
            clamped.y = surface.FeltTopY;
            return clamped;
        }
    }
}
