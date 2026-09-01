using Unity.Netcode;
using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Generic pickup/carry/drop for any physical object. Server-authoritative
    // (pickup/drop go through ServerRpcs) so every client agrees on who's
    // holding what. While held: kinematic + collider disabled (it's parented to
    // the holder, NGO replicates the parent + ownership automatically — an
    // active collider would otherwise shove anything it gets teleported into).
    // While free: normal physics, owned by the server for consistent behaviour.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NetworkObject))]
    public class Grabbable : NetworkBehaviour
    {
        [SerializeField] private Vector3 holdLocalPosition = new Vector3(0.3f, -0.3f, 0.6f);
        [SerializeField] private Vector3 holdLocalEulerAngles = new Vector3(70f, 0f, 0f);

        public Vector3 HoldLocalPosition => holdLocalPosition;
        public Quaternion HoldLocalRotation => Quaternion.Euler(holdLocalEulerAngles);

        private readonly NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false);
        public bool IsHeld => isHeld.Value;

        private Rigidbody rb;
        private Collider col;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            isHeld.OnValueChanged += OnHeldChanged;
            ApplyHeldState(isHeld.Value);
        }

        public override void OnNetworkDespawn()
        {
            isHeld.OnValueChanged -= OnHeldChanged;
        }

        private void OnHeldChanged(bool previous, bool current) => ApplyHeldState(current);

        // Runs on every client (including the server) whenever isHeld changes —
        // not just wherever the ServerRpc happened to execute — so kinematic and
        // collider state can't end up out of sync on non-host clients.
        private void ApplyHeldState(bool held)
        {
            rb.isKinematic = held;
            col.enabled = !held;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPickUpServerRpc(NetworkObjectReference holderRef)
        {
            if (isHeld.Value) return;
            if (!holderRef.TryGet(out NetworkObject holder)) return;

            NetworkObject.ChangeOwnership(holder.OwnerClientId);
            NetworkObject.TrySetParent(holder);
            transform.SetLocalPositionAndRotation(holdLocalPosition, Quaternion.Euler(holdLocalEulerAngles));

            isHeld.Value = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestDropServerRpc()
        {
            if (!isHeld.Value) return;

            NetworkObject.TryRemoveParent();

            // The collider was off while held, so wherever it currently sits may
            // be embedded in something solid (table, floor, the holder's own
            // capsule) — nudge it up a little before physics resumes so the
            // depenetration response has room to push it out cleanly instead of
            // potentially punching it through thin geometry.
            transform.position += Vector3.up * 0.1f;

            isHeld.Value = false;
            NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
        }
    }
}
