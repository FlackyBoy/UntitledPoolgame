using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // What sets a ring of arrows: the hole for its flag, the crate for itself.
    // Read every frame, so changes show live.
    public interface IPlayAnywhereArrowLayout
    {
        int ArrowCount { get; }
        float ArrowSize { get; }
        Vector3 ArrowOffset { get; }
        float ArrowDistance { get; }
        float ArrowTilt { get; }
        float ArrowSpin { get; }
    }

    // Play Anywhere: floating neon arrows above something to find (the flag, the
    // crate), in a ring, all pointing at it: they turn slowly, bounce towards it
    // one after the other and blink, so it can be spotted from across the room.
    // A ring because the screen is split: no single arrow could face both
    // players' cameras. Only a picture: no collider. The look is the prefab's,
    // one for the flag and one for the crate, so they're not mistaken for each
    // other: each of its emissive materials lights up in turn, tail to tip like a
    // neon sign. Number, size and place come from the layout, the bounce and the
    // blink from PlayAnywhereSettings.
    public class PlayAnywhereArrowRing : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private GameObject prefab;
        private IPlayAnywhereArrowLayout layout;
        private PlayAnywhereSettings settings;
        private Vector3 basePoint;
        private readonly List<Transform> arrows = new List<Transform>();
        private readonly List<Material> blinks = new List<Material>();
        private readonly List<Color> blinkColours = new List<Color>();
        private readonly Dictionary<Material, Material> blinkCopies = new Dictionary<Material, Material>();

        // Null without a prefab. basePoint: what the ring is above; the layout's
        // offset is from there.
        public static Transform Build(string name, GameObject prefab, IPlayAnywhereArrowLayout layout, Vector3 basePoint, Transform parent, PlayAnywhereSettings settings)
        {
            if (prefab == null) return null;

            var root = new GameObject(name);
            var ring = root.AddComponent<PlayAnywhereArrowRing>();
            ring.prefab = prefab;
            ring.layout = layout;
            ring.settings = settings;
            ring.basePoint = basePoint;
            // Parented last, keeping its place and size: it goes with its
            // parent, whatever the parent's scale.
            root.transform.SetParent(parent, true);
            ring.Animate();
            return root.transform;
        }

        // An arrow of the ring at this time: its +X points at the target, its
        // face looks out and a little down, towards the players.
        private static void GetPose(IPlayAnywhereArrowLayout layout, PlayAnywhereSettings settings, int index, int count, float time, Vector3 target, out Vector3 position, out Quaternion rotation)
        {
            float angle = (index * 360f / count + layout.ArrowSpin * time) * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float tilt = layout.ArrowTilt * Mathf.Deg2Rad;
            Vector3 away = radial * Mathf.Sin(tilt) + Vector3.up * Mathf.Cos(tilt);
            Vector3 face = radial * Mathf.Cos(tilt) - Vector3.up * Mathf.Sin(tilt);

            // One after the other, each hop going back to the same low point.
            float bounce = Mathf.Abs(Mathf.Sin(Mathf.PI * (settings.flagMarkerBounceRate * time + (float)index / count)));
            position = target + away * (layout.ArrowDistance + bounce * settings.flagMarkerBounceHeight);
            rotation = Quaternion.LookRotation(face, Vector3.Cross(face, -away));
        }

        // Where the arrows will float, as outlines, for the Scene view.
        public static void DrawPreviewGizmos(IPlayAnywhereArrowLayout layout, Vector3 basePoint, Color colour)
        {
            if (Application.isPlaying) return;
            var settings = PoolSettingsLoader.LoadOrDefault<PlayAnywhereSettings>("PlayAnywhereSettings");
            Vector3 target = basePoint + layout.ArrowOffset;
            Gizmos.color = colour;
            for (int i = 0; i < layout.ArrowCount; i++)
            {
                GetPose(layout, settings, i, layout.ArrowCount, 0f, target, out Vector3 position, out Quaternion rotation);
                Vector3 along = rotation * Vector3.right * (0.45f * layout.ArrowSize);
                Vector3 across = rotation * Vector3.up * (0.32f * layout.ArrowSize);
                Gizmos.DrawLine(position - along - across, position - along + across);
                Gizmos.DrawLine(position - along + across, position + along);
                Gizmos.DrawLine(position + along, position - along - across);
                Gizmos.DrawLine(position + along, target);
            }
        }

        // The emissive parts of the prefab: one copy of each material, shared by
        // the whole ring, so that a blink is a few colours set per frame.
        private void ShareBlink(GameObject arrow)
        {
            foreach (Renderer r in arrow.GetComponentsInChildren<Renderer>(true))
            {
                Material source = r.sharedMaterial;
                if (source == null || !source.IsKeywordEnabled("_EMISSION")) continue;
                if (!blinkCopies.TryGetValue(source, out Material copy))
                {
                    copy = new Material(source);
                    blinkCopies.Add(source, copy);
                    blinks.Add(copy);
                    blinkColours.Add(source.GetColor(EmissionColor));
                }
                r.sharedMaterial = copy;
            }
        }

        // As many arrows as the layout asks for right now.
        private void MatchCount()
        {
            int wanted = layout.ArrowCount;
            while (arrows.Count < wanted)
            {
                GameObject arrow = Instantiate(prefab, transform);
                arrows.Add(arrow.transform);
                ShareBlink(arrow);
            }
            while (arrows.Count > wanted)
            {
                Destroy(arrows[arrows.Count - 1].gameObject);
                arrows.RemoveAt(arrows.Count - 1);
            }
        }

        private void Update() => Animate();

        // Unscaled: it keeps going through a slow motion.
        private void Animate()
        {
            MatchCount();
            float time = Time.unscaledTime;
            Vector3 target = basePoint + layout.ArrowOffset;
            Vector3 scale = Vector3.one * layout.ArrowSize;
            for (int i = 0; i < arrows.Count; i++)
            {
                GetPose(layout, settings, i, arrows.Count, time, target, out Vector3 position, out Quaternion rotation);
                arrows[i].SetPositionAndRotation(position, rotation);
                arrows[i].localScale = scale;
            }

            // A blink with soft edges rather than a pulse: clearly on, then off,
            // each tube a little after the one before.
            for (int i = 0; i < blinks.Count; i++)
            {
                float wave = Mathf.Sin(2f * Mathf.PI * (settings.flagMarkerBlinkRate * time - (float)i / blinks.Count));
                blinks[i].SetColor(EmissionColor, blinkColours[i] * Mathf.SmoothStep(0f, 1f, wave * 2.5f + 0.5f));
            }
        }

        private void OnDestroy()
        {
            foreach (Material m in blinks) Destroy(m);
        }
    }
}
