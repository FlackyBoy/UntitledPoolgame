using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Direction signs on the sides of a Play Anywhere course, worked out from
    // its path (PlayAnywhereHole), so moving a point moves them:
    // - along the stretches, every Sign Spacing metres, one side then the
    //   other: nearly parallel to the course, turned a little towards whoever
    //   is coming, the chevrons pointing onwards;
    // - at each turn of at least Sign Turn Angle, on the outside of the bend,
    //   facing whoever arrives (like a road's bend sign), the chevrons
    //   pointing into the new direction.
    // A sign stands just outside the corridor. A wall there brings it back
    // against the wall, or sends it to the other side; a piece of furniture or
    // another stretch of the corridor there rules that side out. Over a drop
    // (a course on the counter), its post reaches down to the ground.
    //
    // Signs placed in the scene from the hole's Inspector (a child RootName of
    // the hole) replace this: they're used as they are, edited by hand.
    public static class PlayAnywhereCourseSigns
    {
        public const string RootName = "Panneaux";

        public struct Placement
        {
            public Vector3 position;     // foot of the sign, at the course's height over a drop
            public float drop;           // how far the post goes down from there to the ground
            public Quaternion rotation;  // +Z is what the board faces
            public Vector3 pointing;     // where the chevrons point
        }

        private const float WallProbeHeight = 0.5f;    // above the course, over skirting and low lips
        private const float WallClearance = 0.12f;     // board and post stay this far from a wall
        private const float MinWallInset = 0.6f;       // of the half width: closer to the middle, this side is given up
        private const float GroundProbeHeight = 1f;    // above the course: where the search for the ground starts
        private const float MaxGroundRise = 0.3f;      // ground higher than the course than this is furniture, not ground
        private const float MaxGroundDrop = 5f;        // no ground within this below the course: no sign
        private const float CupClearance = 1f;         // no sign this close to the cup: the flag says it all

        private static readonly RaycastHit[] hits = new RaycastHit[16];

        // Fills placements for this hole. Also called in the editor, for the
        // preview in the Scene view.
        public static void Compute(PlayAnywhereHole hole, PlayAnywhereSettings settings, List<Placement> placements)
        {
            placements.Clear();
            if (hole.PathWidth <= 0f || settings.signSpacing <= 0f) return;

            int count = hole.NodeCount;
            var nodes = new Vector3[count];
            var widths = new float[count];
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                hole.GetNode(i, out nodes[i], out widths[i]);
                if (i > 0) total += Flat(nodes[i] - nodes[i - 1]).magnitude;
            }

            // Bends first: signs along the stretches keep off them.
            var isBend = new bool[count];
            for (int i = 1; i < count - 1; i++)
                isBend[i] = AddBendSign(hole, settings, nodes, widths, i, placements);

            // Half a spacing in: a short course still gets its sign.
            float next = Mathf.Min(settings.signSpacing, total) * 0.5f;
            float walked = 0f;
            float side = 1f;
            for (int i = 1; i < count; i++)
            {
                Vector3 along = Flat(nodes[i] - nodes[i - 1]);
                float length = along.magnitude;
                if (length < 1e-3f) continue;
                along /= length;

                for (; next <= walked + length; next += settings.signSpacing)
                {
                    float into = next - walked;
                    float keepOff = settings.signSpacing * 0.4f;
                    bool nearBend = (isBend[i - 1] && into < keepOff) || (isBend[i] && length - into < keepOff);
                    bool nearCup = i == count - 1 && length - into < CupClearance;
                    if (nearBend || nearCup) continue;

                    float t = into / length;
                    Vector3 centre = Vector3.Lerp(nodes[i - 1], nodes[i], t);
                    float half = Mathf.Lerp(widths[i - 1], widths[i], t) * 0.5f;
                    if (AddSideSign(hole, settings, centre, along, half, side, placements)
                        || AddSideSign(hole, settings, centre, along, half, -side, placements))
                        side = -side;
                }
                walked += length;
            }
        }

        // The signs of this hole, made once when it's first shown. Null when
        // there are none.
        public static Transform Build(PlayAnywhereHole hole, PlayAnywhereSettings settings)
        {
            if (settings.signPrefab == null) return null;
            var placements = new List<Placement>();
            Compute(hole, settings, placements);
            if (placements.Count == 0) return null;

            var root = new GameObject(RootName).transform;
            foreach (Placement p in placements)
            {
                PlayAnywhereSign sign = Object.Instantiate(settings.signPrefab, p.position, p.rotation, root);
                sign.transform.localScale *= settings.signSize;
                sign.Configure(Flipped(p), p.drop);
            }
            // Parented last, keeping its place and size: it goes with the
            // hole, whatever the hole's scale.
            root.SetParent(hole.transform, true);
            if (settings.debugLogs)
                Debug.Log($"[PlayAnywhere] Parcours '{hole.name}' : {placements.Count} panneau(x) posé(s).", hole);
            return root;
        }

        // The first fixed surface going down from this point.
        public static bool GroundBelow(Vector3 from, out RaycastHit ground) =>
            NearestFixed(from, Vector3.down, MaxGroundDrop, out ground);

        // The chevrons are made to point along the board's +X.
        public static bool Flipped(in Placement p) => Vector3.Dot(p.rotation * Vector3.right, p.pointing) < 0f;

        private static bool AddBendSign(PlayAnywhereHole hole, PlayAnywhereSettings settings, Vector3[] nodes, float[] widths, int index, List<Placement> placements)
        {
            Vector3 comingIn = Flat(nodes[index] - nodes[index - 1]).normalized;
            Vector3 goingOut = Flat(nodes[index + 1] - nodes[index]).normalized;
            if (comingIn == Vector3.zero || goingOut == Vector3.zero) return false;
            if (Vector3.Angle(comingIn, goingOut) < settings.signTurnAngle) return false;

            Vector3 outside = (comingIn - goingOut).normalized;
            return AddSign(hole, settings, nodes[index], outside, widths[index] * 0.5f, -comingIn, goingOut, placements);
        }

        private static bool AddSideSign(PlayAnywhereHole hole, PlayAnywhereSettings settings, Vector3 centre, Vector3 along, float half, float side, List<Placement> placements)
        {
            Vector3 outward = Vector3.Cross(Vector3.up, along) * side;
            float facing = settings.signFacingAngle * Mathf.Deg2Rad;
            Vector3 face = -outward * Mathf.Cos(facing) - along * Mathf.Sin(facing);
            return AddSign(hole, settings, centre, outward, half, face, along, placements);
        }

        private static bool AddSign(PlayAnywhereHole hole, PlayAnywhereSettings settings, Vector3 centre, Vector3 outward, float half, Vector3 face, Vector3 pointing, List<Placement> placements)
        {
            // A wall in the way: against it, if that's still towards the edge.
            float distance = half + settings.signSideMargin;
            if (NearestFixed(centre + Vector3.up * WallProbeHeight, outward, distance + WallClearance, out RaycastHit wall))
            {
                float against = wall.distance - WallClearance;
                if (against < half * MinWallInset) return false;
                distance = Mathf.Min(distance, against);
            }
            Vector3 spot = centre + outward * distance;

            // Past this stretch's edge but in another stretch of the corridor.
            if (distance >= half && hole.InPlayArea(spot)) return false;

            Vector3 from = spot + Vector3.up * GroundProbeHeight;
            if (!NearestFixed(from, Vector3.down, GroundProbeHeight + MaxGroundDrop, out RaycastHit ground)) return false;
            if (ground.point.y > centre.y + MaxGroundRise) return false;

            spot.y = Mathf.Max(ground.point.y, centre.y);
            placements.Add(new Placement
            {
                position = spot,
                drop = spot.y - ground.point.y,
                rotation = Quaternion.LookRotation(face, Vector3.up),
                pointing = pointing,
            });
            return true;
        }

        // The nearest thing that stays put: not a ball, a body or a player's
        // capsule, which are only there for now.
        private static bool NearestFixed(Vector3 from, Vector3 direction, float length, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            int count = Physics.RaycastNonAlloc(from, direction, hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider.attachedRigidbody != null || hit.collider is CharacterController) continue;
                if (found && hit.distance >= nearest.distance) continue;
                nearest = hit;
                found = true;
            }
            return found;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
