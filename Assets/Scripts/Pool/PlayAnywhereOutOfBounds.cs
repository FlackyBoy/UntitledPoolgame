using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: a zone out of the course (behind the bar, the stage...).
    // A ball whose centre enters it costs a penalty and goes back to where
    // it last stood still. Its collider is a trigger, the balls go
    // through it; PlayAnywhereRuleSet tests the two balls against every zone.
    [AddComponentMenu("Pool/Play Anywhere/Zone hors limites")]
    [RequireComponent(typeof(Collider))]
    public class PlayAnywhereOutOfBounds : MonoBehaviour
    {
        private static readonly List<PlayAnywhereOutOfBounds> active = new List<PlayAnywhereOutOfBounds>();
        public static IReadOnlyList<PlayAnywhereOutOfBounds> Active => active;

        private Collider zone;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void Awake()
        {
            zone = GetComponent<Collider>();
            zone.isTrigger = true;
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        // ClosestPoint is the point itself when it's inside (box, sphere,
        // capsule, convex mesh).
        public bool Contains(Vector3 point) => zone.enabled && (zone.ClosestPoint(point) - point).sqrMagnitude < 1e-6f;

        public static bool AnyContains(Vector3 point)
        {
            foreach (PlayAnywhereOutOfBounds z in active)
                if (z.Contains(point)) return true;
            return false;
        }
    }
}
