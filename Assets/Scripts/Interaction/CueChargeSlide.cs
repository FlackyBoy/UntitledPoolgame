using RootMotion.FinalIK;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Slides the cue's mesh along its axis (back while charging, forward for
    // extra reach) so the cue moves through hands that stay put: the grip
    // points aren't children of the mesh, and FBBIK follows them.
    public class CueChargeSlide : MonoBehaviour
    {
        // The visual-only child. It must not carry the grip points, or the
        // hands would slide with it.
        [SerializeField] private Transform cueMesh;

        // The holder's aim controller, found from the cue's parent (a fixed
        // reference broke when another player held the cue).
        private Transform lastParent;
        private LocalPoolAimController holderAimController;

        // Slide direction when the tip axis isn't detected (else -TipAxis).
        [SerializeField] private Vector3 slideAxis = Vector3.forward;

        [SerializeField] private float slideDistance = 0.15f;

        // Local axis toward the tip, used when the tip axis isn't detected.
        // Only the mesh moves, never the held cue (that drags the hands).
        [SerializeField] private Vector3 reachAxis = Vector3.forward;

        // Finds the tip axis from the mesh (longest side, tip = thinner end)
        // instead of the three axis fields: the model's Z isn't its length.
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
        }

        private bool detectedOk;

        private bool TryDetectTipAxis(out Vector3 axisInCueSpace)
        {
            axisInCueSpace = Vector3.forward;
            MeshFilter filter = cueMesh != null ? cueMesh.GetComponentInChildren<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            // Bounds work without Read/Write, so they're enough here.
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

            // Tip distance from the origin (mid-cue), so the tip is placed at
            // the ball, not the pivot.
            Vector3 tipInMesh = center;
            tipInMesh[axis] = sign > 0f ? mesh.bounds.max[axis] : mesh.bounds.min[axis];
            Vector3 tipInCue = transform.InverseTransformPoint(filter.transform.TransformPoint(tipInMesh));
            TipDistance = Mathf.Max(0f, Vector3.Dot(tipInCue, axisInCueSpace));

            // The other end (the butt), same axis — negative when it's behind
            // the origin. Used by LocalCueHolder to know the cue's extent.
            Vector3 buttInMesh = center;
            buttInMesh[axis] = sign > 0f ? mesh.bounds.min[axis] : mesh.bounds.max[axis];
            Vector3 buttInCue = transform.InverseTransformPoint(filter.transform.TransformPoint(buttInMesh));
            ButtDistance = Vector3.Dot(buttInCue, axisInCueSpace);

            // How far the mesh can slide before its butt passes the rear hand
            // (with Butt Margin to spare).
            if (grips.Length > 0)
            {
                float rearGrip = float.MaxValue;
                foreach (InteractionTarget grip in grips)
                    rearGrip = Mathf.Min(rearGrip, Vector3.Dot(transform.InverseTransformPoint(grip.transform.position), axisInCueSpace));
                MaxMeshReach = Mathf.Max(0f, rearGrip - ButtDistance - buttMargin);
            }
            return true;
        }

        [Tooltip("Longueur de queue (mètres) qui doit toujours dépasser derrière la main arrière quand la queue glisse dans les mains pour atteindre une bille lointaine.")]
        [SerializeField] private float buttMargin = 0.1f;

        // Mesh slide allowed before the butt passes the rear hand; caps the
        // playable reach. MaxValue if unknown.
        public float MaxMeshReach { get; private set; } = float.MaxValue;

        // Signed distance from the cue root's origin to its butt along
        // TipAxis (usually negative), or 0 if the mesh couldn't be analysed.
        public float ButtDistance { get; private set; }

        // The held cue is a child of its holder, so the aim controller is up
        // the hierarchy (looked up again when the parent changes). None = no
        // slide. Also read by the aim trace.
        public LocalPoolAimController FollowedAimController => HolderAimController();
        public float CurrentMeshOffset => cueMesh != null ? Vector3.Dot(cueMesh.localPosition - meshRestLocalPosition, TipAxis) : 0f;

        private LocalPoolAimController HolderAimController()
        {
            if (transform.parent != lastParent)
            {
                lastParent = transform.parent;
                holderAimController = lastParent != null ? lastParent.GetComponentInParent<LocalPoolAimController>() : null;
            }
            return holderAimController;
        }

        // Distance from the cue root's origin to its tip along TipAxis, or 0
        // if the mesh couldn't be analysed.
        public float TipDistance { get; private set; }

        private void LateUpdate()
        {
            if (cueMesh == null) return;

            LocalPoolAimController aim = HolderAimController();
            bool aiming = aim != null && aim.IsAiming;
            float fraction = aiming ? aim.ChargeFraction : 0f;
            // The hands already moved forward by AimHandShift: the mesh slides
            // for the rest.
            float reach = aiming ? aim.AimMeshReach : 0f;

            Vector3 slideDirection = detectedOk ? -TipAxis : slideAxis.normalized;
            cueMesh.localPosition = meshRestLocalPosition + TipAxis * reach + slideDirection * (fraction * slideDistance);
        }
    }
}
