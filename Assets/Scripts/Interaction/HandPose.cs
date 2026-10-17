using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Procedural hand shapes applied after FBBIK (OnPostUpdate): fist,
    // pointing finger, thumbs up. From bone positions only, so any humanoid
    // rig works whatever its axes.
    public static class HandPose
    {
        // along: wrist to middle finger root. palm: out of the palm, toward
        // where the fingers curl. False if the hand or index is missing.
        // On rigs with only index and thumb (the player), the index stands in
        // for the middle finger and the thumb side for the palm's width.
        public static bool Measure(Animator animator, bool right, out Vector3 along, out Vector3 palm)
        {
            along = palm = Vector3.zero;
            Transform hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform index = animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform middle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform little = animator.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            Transform thumb = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            if (hand == null || index == null) return false;

            along = ((middle != null ? middle : index).position - hand.position).normalized;
            Vector3 across;
            if (little != null) across = index.position - little.position;
            else if (thumb != null)
            {
                across = thumb.position - hand.position;
                across -= along * Vector3.Dot(across, along);
            }
            else return false;
            palm = (right ? Vector3.Cross(along, across) : Vector3.Cross(across, along)).normalized;
            return palm.sqrMagnitude > 0.5f;
        }

        // Which finger bones the rig has (diagnostic, once per hand).
        public static string Describe(Animator animator, bool right)
        {
            string Has(HumanBodyBones b) => animator.GetBoneTransform(b) != null ? "oui" : "non";
            return right
                ? $"main {Has(HumanBodyBones.RightHand)}, pouce {Has(HumanBodyBones.RightThumbProximal)}, index {Has(HumanBodyBones.RightIndexProximal)}, majeur {Has(HumanBodyBones.RightMiddleProximal)}, annulaire {Has(HumanBodyBones.RightRingProximal)}, auriculaire {Has(HumanBodyBones.RightLittleProximal)}"
                : $"main {Has(HumanBodyBones.LeftHand)}, pouce {Has(HumanBodyBones.LeftThumbProximal)}, index {Has(HumanBodyBones.LeftIndexProximal)}, majeur {Has(HumanBodyBones.LeftMiddleProximal)}, annulaire {Has(HumanBodyBones.LeftRingProximal)}, auriculaire {Has(HumanBodyBones.LeftLittleProximal)}";
        }

        // Turns the hand bone so its fingers point along wantedAlong and its
        // palm faces wantedPalm, by weight (0 = untouched).
        public static void Orient(Animator animator, bool right, Vector3 wantedAlong, Vector3 wantedPalm, float weight)
        {
            if (weight <= 0f || !Measure(animator, right, out Vector3 along, out Vector3 palm)) return;
            Transform hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Vector3.OrthoNormalize(ref wantedAlong, ref wantedPalm);
            Quaternion delta = Quaternion.LookRotation(wantedAlong, wantedPalm) * Quaternion.Inverse(Quaternion.LookRotation(along, palm));
            hand.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * hand.rotation;
        }

        // Curls each finger by its own angle (degrees, toward the palm;
        // negative opens it up). Measured once, before any finger moves.
        public static void Curl(Animator animator, bool right, float index, float middle, float ring, float little, float thumb)
        {
            if (!Measure(animator, right, out _, out Vector3 palm)) return;
            if (right)
            {
                CurlFinger(animator, palm, index, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal);
                CurlFinger(animator, palm, middle, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal);
                CurlFinger(animator, palm, ring, HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal);
                CurlFinger(animator, palm, little, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal);
                CurlFinger(animator, palm, thumb, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal);
            }
            else
            {
                CurlFinger(animator, palm, index, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal);
                CurlFinger(animator, palm, middle, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal);
                CurlFinger(animator, palm, ring, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal);
                CurlFinger(animator, palm, little, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal);
                CurlFinger(animator, palm, thumb, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal);
            }
        }

        // Aims the thumb along wanted (thumbs up: straight up) by weight,
        // whatever its rest pose.
        public static void AimThumb(Animator animator, bool right, Vector3 wanted, float weight)
        {
            if (weight <= 0f) return;
            Transform root = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            Transform tip = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);
            if (root == null || tip == null) return;
            if (tip.childCount > 0) tip = tip.GetChild(0);
            Vector3 dir = tip.position - root.position;
            if (dir.sqrMagnitude < 1e-8f) return;
            Quaternion turn = Quaternion.FromToRotation(dir, wanted);
            root.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * root.rotation;
        }

        // Each joint turns its bone toward the palm: about bone × palm.
        private static void CurlFinger(Animator animator, Vector3 palm, float angle, HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
        {
            if (Mathf.Abs(angle) < 0.01f) return;
            Transform b0 = animator.GetBoneTransform(a), b1 = animator.GetBoneTransform(b), b2 = animator.GetBoneTransform(c);
            for (int k = 0; k < 3; k++)
            {
                Transform bone = k == 0 ? b0 : k == 1 ? b1 : b2;
                if (bone == null) continue;
                Transform prev = k == 1 ? b0 : k == 2 ? b1 : null;
                Transform next = k == 0 ? b1 : k == 1 ? b2 : bone.childCount > 0 ? bone.GetChild(0) : null;
                Vector3 dir = next != null ? next.position - bone.position
                    : prev != null ? bone.position - prev.position : Vector3.zero;
                if (dir.sqrMagnitude < 1e-8f) continue;
                Vector3 axis = Vector3.Cross(dir.normalized, palm);
                if (axis.sqrMagnitude < 1e-6f) continue;
                bone.rotation = Quaternion.AngleAxis(angle, axis.normalized) * bone.rotation;
            }
        }
    }
}
