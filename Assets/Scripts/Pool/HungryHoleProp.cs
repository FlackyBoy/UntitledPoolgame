using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Hungry Hole: something to eat on the felt (a crate, a tree, an apple…).
    // Any prefab, scaled at spawn from its real bounds so it never comes in
    // huge: its footprint to the entry's size, its height capped. Its own
    // colliders and bodies are stripped for one upright capsule the size of
    // its footprint, an obstacle the balls bounce off. A hole bigger than it
    // eats it (HungryHoleCue) and grows with its size; nobody owns it.
    public class HungryHoleProp : MonoBehaviour
    {
        private static readonly List<HungryHoleProp> all = new List<HungryHoleProp>();
        public static IReadOnlyList<HungryHoleProp> All => all;

        // Footprint radius, metres (room kept around it).
        public float Radius { get; private set; }
        // What must fit in the hole, and the collision's radius: smaller than
        // the footprint for a tree whose foliage spreads above its foot.
        public float FitRadius { get; private set; }
        // Same, in normal-ball radii (what the growth is worked out from).
        public float Size { get; private set; }
        public Collider Body { get; private set; }
        public bool IsEaten { get; private set; }
        // Footprint centre on the felt (the pivot may be elsewhere).
        public Vector3 Center => transform.position - pivotOffset;
        private Vector3 pivotOffset;

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        // size and maxHeight in normal-ball radii (unit = that radius).
        public static HungryHoleProp Spawn(GameObject prefab, Vector3 feltPoint, float size, float maxHeight, float unit, float fit = 1f)
        {
            // Measured unturned: the bounds of a turned box are wider than it is.
            GameObject instance = Instantiate(prefab, feltPoint, Quaternion.identity);
            instance.name = $"{prefab.name} (Hungry Hole)";
            // Inactive parts too (LODs): any collider left would block the hole.
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }

            // Scaled so its footprint is size, its height at most maxHeight.
            if (!TryGetBounds(instance, out Bounds bounds))
            {
                Destroy(instance);
                return null;
            }
            float footprint = Mathf.Max(bounds.extents.x, bounds.extents.z, 1e-4f);
            float scale = Mathf.Min(size * unit / footprint, maxHeight * unit / Mathf.Max(bounds.size.y, 1e-4f));
            instance.transform.localScale *= scale;
            TryGetBounds(instance, out bounds);
            // Standing on the felt, centred on the point.
            instance.transform.position += new Vector3(feltPoint.x - bounds.center.x, feltPoint.y - bounds.min.y, feltPoint.z - bounds.center.z);

            var prop = instance.AddComponent<HungryHoleProp>();
            prop.Radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            prop.Size = prop.Radius / unit;
            prop.FitRadius = prop.Radius * Mathf.Clamp(fit, 0.1f, 1f);
            // Turned at random about its own centre, now that it's measured.
            instance.transform.RotateAround(feltPoint, Vector3.up, Random.Range(0f, 360f));
            prop.pivotOffset = instance.transform.position - feltPoint;

            // The capsule on a child at world scale, so it's set in metres.
            var bodyObject = new GameObject("HungryHolePropBody");
            bodyObject.transform.SetParent(instance.transform, false);
            bodyObject.transform.position = new Vector3(feltPoint.x, feltPoint.y + bounds.size.y * 0.5f, feltPoint.z);
            Vector3 lossy = instance.transform.lossyScale;
            bodyObject.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
            var capsule = bodyObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = prop.FitRadius;
            capsule.height = Mathf.Max(bounds.size.y, prop.FitRadius * 2f);
            prop.Body = capsule;
            return prop;
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            // The far LODs are all enabled too, and coarser (wider): only the
            // first one counts.
            var skipped = new HashSet<Renderer>();
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>())
            {
                LOD[] lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    foreach (Renderer r in lods[i].renderers)
                        if (r != null) skipped.Add(r);
                if (lods.Length > 0)
                    foreach (Renderer r in lods[0].renderers)
                        if (r != null) skipped.Remove(r);
            }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || skipped.Contains(r)) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        // Slides to the hole's centre (following it as it moves) while
        // shrinking and sinking, then goes.
        // Springs up out of the felt to its size (a respawn).
        public void PopIn(float time)
        {
            if (time > 0f) StartCoroutine(PopRoutine(time));
        }

        private IEnumerator PopRoutine(float time)
        {
            Vector3 full = transform.localScale;
            Vector3 foot = Center;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float x = t / time;
                // Overshoots a little before settling (back ease).
                float s = 1f + 2.2f * Mathf.Pow(x - 1f, 3f) + 1.2f * Mathf.Pow(x - 1f, 2f);
                transform.localScale = full * Mathf.Max(0.01f, s);
                transform.position = foot + pivotOffset * Mathf.Max(0.01f, s);
                yield return null;
            }
            transform.localScale = full;
            transform.position = foot + pivotOffset;
        }

        // spin: degrees per second at the end of the fall (it speeds up).
        public void Eat(Transform hole, float time, float spin = 0f)
        {
            if (IsEaten) return;
            IsEaten = true;
            if (Body != null) Body.enabled = false;
            StartCoroutine(EatRoutine(hole, Mathf.Max(0.01f, time), spin));
        }

        private IEnumerator EatRoutine(Transform hole, float time, float spin)
        {
            float turned = 0f;
            Vector3 fromOffset = Center - hole.position;
            fromOffset.y = 0f;
            Vector3 fromScale = transform.localScale;
            Quaternion startRotation = transform.rotation;
            float feltY = Center.y;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                // Its centre relative to the hole: from where it was to the middle.
                Vector3 center = hole.position + fromOffset * (1f - Mathf.Min(1f, k / 0.4f));
                center.y = feltY - Radius * 2f * k * k;
                float shrink = 1f - k * 0.9f;
                transform.localScale = fromScale * shrink;
                // Swirls down the hole, faster and faster.
                turned += spin * k * Time.deltaTime;
                Quaternion swirl = Quaternion.Euler(0f, turned, 0f);
                transform.rotation = swirl * startRotation;
                transform.position = center + swirl * pivotOffset * shrink;
                yield return null;
            }
            Destroy(gameObject);
        }

        public static void ClearAll()
        {
            // Off at once (out of All), destroyed at the end of the frame.
            for (int i = all.Count - 1; i >= 0; i--)
            {
                GameObject prop = all[i].gameObject;
                prop.SetActive(false);
                Destroy(prop);
            }
        }
    }
}
