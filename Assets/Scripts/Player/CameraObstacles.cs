using UnityEngine;

namespace UntitledPoolGame.Player
{
    // What stops a camera: the world's solid things, not the bodies, balls or
    // loose objects moving around it (those would make the view jump).
    public static class CameraObstacles
    {
        private static readonly RaycastHit[] hits = new RaycastHit[16];
        private static int mask;

        // How far a camera of this radius can go from origin along direction
        // (unit vector), up to distance. self: its player, never an obstacle.
        public static float FreeDistance(Vector3 origin, Vector3 direction, float distance, float radius, Transform self)
        {
            if (mask == 0) mask = ~LayerMask.GetMask("Ragdoll", "Player", "PlayerAnimated", "Poolball");

            float free = distance;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance > 0f && hit.distance < free && Blocks(hit.collider, self)) free = hit.distance;
            }
            // A sphere cast skips what it overlaps at the start: a wall right
            // next to the origin (a ball against it) is only seen by a ray.
            count = Physics.RaycastNonAlloc(origin, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance - radius < free && Blocks(hit.collider, self)) free = Mathf.Max(0f, hit.distance - radius);
            }
            return free;
        }

        // The point nearest to 'to' that the camera reaches coming from 'from'.
        public static Vector3 Reach(Vector3 from, Vector3 to, float radius, Transform self)
        {
            Vector3 path = to - from;
            float length = path.magnitude;
            if (radius <= 0f || length < 0.001f) return to;
            Vector3 direction = path / length;
            return from + direction * FreeDistance(from, direction, length, radius, self);
        }

        private static bool Blocks(Collider c, Transform self)
        {
            if (c is CharacterController || c.transform.IsChildOf(self)) return false;
            Rigidbody body = c.attachedRigidbody;
            return body == null || body.isKinematic;
        }
    }
}
