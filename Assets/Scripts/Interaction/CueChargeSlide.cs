using RootMotion.FinalIK;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Slides the cue's visual mesh back along its own axis as the shot
    // charges, so the cue itself appears to move through a hand that stays
    // put (the grip point — an InteractionTarget, sibling of CueMesh, not a
    // child of it — never moves, since it's what FBBIK's hand effector
    // tracks). Deliberately not using the Animator's own "ShootCharge"
    // blend tree parameter (see LocalCharacterAnimationController) — that
    // one assumes the cue is a child of an animated hand bone, which
    // conflicts with the InteractionSystem-based two-hand hold this project
    // settled on (2026-09-23).
    public class CueChargeSlide : MonoBehaviour
    {
        // The separated visual-only child (CueMesh) — must NOT be the same
        // object as the one carrying LocalGrabbable/InteractionObject/the
        // grip points, or sliding it would drag the grip point (and so the
        // hand) along with it instead of leaving it fixed.
        [SerializeField] private Transform cueMesh;

        // This specific cue's owner — one cue per player, so a direct
        // reference rather than a runtime lookup.
        [SerializeField] private LocalPoolAimController aimController;

        // Which local axis of the cue points along its own length (toward
        // the tip) — tune in Play Mode (charge a shot, see which way it
        // slides) same as every other axis/offset guessed blind today.
        [SerializeField] private Vector3 slideAxis = Vector3.forward;

        [SerializeField] private float slideDistance = 0.15f;

        // Which local axis points along the cue toward its tip, i.e. toward
        // the ball (same axis as LocalPoolAimController.Aim Hold Axis) — the
        // mesh extends this way by AimReachExtra when the body had to stand
        // further back to clear the table, and pulls back the opposite way
        // (Slide Axis, above) while charging. Only the mesh moves, never the
        // held cue itself: moving that drags the hands, and the body with them.
        [SerializeField] private Vector3 reachAxis = Vector3.forward;

        // Works out the tip axis from the mesh itself (longest bounding-box
        // axis; the tip is the thinner end) instead of trusting Reach Axis /
        // Slide Axis / Aim Hold Axis — the cue's own Z turned out not to be
        // its length axis (the model has its own orientation). Uncheck to
        // fall back to those three fields.
        [SerializeField] private bool autoDetectTipAxis = true;

        private Vector3 meshRestLocalPosition;

        // Tip-ward direction of the cue, in THIS object's local space (the
        // cue root — same space LocalPoolAimController works in).
        public Vector3 TipAxis { get; private set; } = Vector3.forward;

        private void Awake()
        {
            if (cueMesh != null) meshRestLocalPosition = cueMesh.localPosition;
            Vector3 detected = Vector3.forward;
            detectedOk = autoDetectTipAxis && TryDetectTipAxis(out detected);
            TipAxis = detectedOk ? detected : reachAxis.normalized;
            // TEMP diagnostic (2026-09-24) — remove once the tip detection is trusted.
            MeshFilter dbgFilter = cueMesh != null ? cueMesh.GetComponentInChildren<MeshFilter>() : null;
            SkinnedMeshRenderer dbgSkinned = cueMesh != null ? cueMesh.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            Mesh dbgMesh = dbgFilter != null ? dbgFilter.sharedMesh : null;
            Debug.Log($"[CueChargeSlide] cueMesh={(cueMesh != null ? cueMesh.name : "NULL")} meshFilter={(dbgFilter != null ? dbgFilter.name : "NONE")} " +
                $"skinned={(dbgSkinned != null)} mesh={(dbgMesh != null ? dbgMesh.name : "NONE")} readable={(dbgMesh != null && dbgMesh.isReadable)} " +
                $"boundsSize={(dbgMesh != null ? dbgMesh.bounds.size.ToString() : "-")} autoDetect={autoDetectTipAxis} detectedOk={detectedOk} " +
                $"TipAxis(cue space)={TipAxis} TipDistance={TipDistance:F3}", this);
        }

        private bool detectedOk;

        private bool TryDetectTipAxis(out Vector3 axisInCueSpace)
        {
            axisInCueSpace = Vector3.forward;
            MeshFilter filter = cueMesh != null ? cueMesh.GetComponentInChildren<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            // mesh.bounds is available even when the mesh isn't Read/Write
            // enabled (its vertices aren't) — so the axis and the tip
            // distance come from the bounds alone.
            Vector3 size = mesh.bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            Vector3 center = mesh.bounds.center;
            if (size[axis] <= 0f) return false;

            Vector3 plusInMesh = Vector3.zero;
            plusInMesh[axis] = 1f;
            Vector3 plusInCue = transform.InverseTransformDirection(filter.transform.TransformDirection(plusInMesh)).normalized;
            Vector3 centerInCue = transform.InverseTransformPoint(filter.transform.TransformPoint(center));

            // Which end is the tip: the hands hold the butt, so it's the end
            // FARTHER from the grip points (InteractionTargets on this cue).
            float sign;
            InteractionTarget[] grips = GetComponentsInChildren<InteractionTarget>();
            if (grips.Length > 0)
            {
                Vector3 gripAverage = Vector3.zero;
                foreach (InteractionTarget grip in grips) gripAverage += transform.InverseTransformPoint(grip.transform.position);
                gripAverage /= grips.Length;
                sign = Vector3.Dot(gripAverage - centerInCue, plusInCue) > 0f ? -1f : 1f;
            }
            else if (mesh.isReadable)
            {
                // No grips to go by: the thinner end is the tip (average
                // distance from the length axis over each end's 15%).
                float min = mesh.bounds.min[axis];
                float length = size[axis];
                float sumLow = 0f, sumHigh = 0f;
                int countLow = 0, countHigh = 0;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    float t = (vertex[axis] - min) / length;
                    if (t > 0.15f && t < 0.85f) continue;

                    Vector3 offset = vertex - center;
                    offset[axis] = 0f;
                    if (t <= 0.15f) { sumLow += offset.magnitude; countLow++; }
                    else { sumHigh += offset.magnitude; countHigh++; }
                }
                if (countLow == 0 || countHigh == 0) return false;
                sign = (sumHigh / countHigh) < (sumLow / countLow) ? 1f : -1f;
            }
            else
            {
                return false;
            }

            Vector3 meshLocal = Vector3.zero;
            meshLocal[axis] = sign;

            axisInCueSpace = transform.InverseTransformDirection(filter.transform.TransformDirection(meshLocal)).normalized;

            // How far the tip end sits from this object's origin, along that
            // axis — so the body can be placed with the TIP (not the pivot,
            // which is mid-cue) at the ball.
            Vector3 tipInMesh = center;
            tipInMesh[axis] = sign > 0f ? mesh.bounds.max[axis] : mesh.bounds.min[axis];
            Vector3 tipInCue = transform.InverseTransformPoint(filter.transform.TransformPoint(tipInMesh));
            TipDistance = Mathf.Max(0f, Vector3.Dot(tipInCue, axisInCueSpace));
            return true;
        }

        // Distance from the cue root's origin to its tip along TipAxis, or 0
        // if the mesh couldn't be analysed.
        public float TipDistance { get; private set; }

        private void LateUpdate()
        {
            if (cueMesh == null) return;

            bool aiming = aimController != null && aimController.IsAiming;
            float fraction = aiming ? aimController.ChargeFraction : 0f;
            // The hands already moved forward by AimHandShift (the held cue
            // itself moves, see LocalPoolAimController.ApplyHandShift), so the
            // mesh only slides through them for what's left.
            float reach = aiming ? aimController.AimMeshReach : 0f;

            Vector3 slideDirection = detectedOk ? -TipAxis : slideAxis.normalized;
            cueMesh.localPosition = meshRestLocalPosition + TipAxis * reach + slideDirection * (fraction * slideDistance);
        }
    }
}
