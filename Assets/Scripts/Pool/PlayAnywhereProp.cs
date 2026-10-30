using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // A crate object placed in two goes (the pipe: its entry, then its
    // exit). FirstPart is placed and left there; SecondPart, hidden until
    // then, is held next and placed within MaxPartDistance of it.
    public interface IPlayAnywhereTwoPart
    {
        Transform FirstPart { get; }
        Transform SecondPart { get; }
        float MaxPartDistance { get; }
    }

    // Told when its object is placed (the destroyer acts then).
    public interface IPlayAnywherePlaced
    {
        void OnPlaced(PlayAnywhereRuleSet rules, PlayAnywhereProp prop);
    }

    // Play Anywhere: an object of the crate. Picked up like any object
    // (Interact), it can't be thrown or dropped: a ghost shows where it will
    // stand, on the ground the player looks at; the mouse wheel or the
    // d-pad (SwitchPower, Previous / Next without it) turn it, Attack or
    // Interact place it there. Placed, it stays put (kinematic)
    // for the rest of the hole. Let go any other way (knocked down), it goes
    // back to the crate. Made by PlayAnywhereRuleSet.Spawn.
    public class PlayAnywhereProp : MonoBehaviour, ILocalHeldOverride
    {
        private enum State { InCrate, Held, Placed }

        private PlayAnywhereRuleSet rules;
        private PlayAnywhereSettings settings;
        private LocalGrabbable grabbable;
        // Its solid colliders: one box at its size, or the prefab's own
        // (Use Own Colliders: a ramp must keep its slope).
        private Collider[] solids;
        private State state;
        private Vector3 crateSlot;
        private Quaternion crateRotation;
        // Pivot height above the bottom of the box (upright, scaled).
        private float bottomOffset;
        // Box half size in world units (upright), for the overlap test.
        private Vector3 halfExtents;
        private Vector3 boxCenterOffset;
        private float footprintRadius;

        private LocalPlayerHandController holder;
        private int holderIndex = -1;
        private Transform holderView;
        private InputAction placeAction, interactAction, rotateAction, rotateLeftAction, rotateRightAction;
        private int heldSinceFrame;
        private float yawOffset;

        private GameObject ghost;
        private Renderer[] ghostRenderers;
        private bool ghostValid;
        private bool hasGhostPose;
        private Vector3 ghostPosition;
        private Quaternion ghostRotation;
        // Diagnostics: the ghost's last state, logged when it changes.
        private string ghostTrace;

        // Two-part objects: whether the first part is down, and where both
        // parts sit in the object (put back if it returns to the crate).
        private IPlayAnywhereTwoPart twoPart;
        private bool firstPlaced;
        private Vector3 firstRestPosition, secondRestPosition;
        private Quaternion firstRestRotation, secondRestRotation;

        // Given by the debug menu, out of the round: taking and placing it
        // don't count, and let go unplaced it's gone (it has no crate slot).
        private bool free;

        private static Material validMaterial, invalidMaterial;
        private static readonly RaycastHit[] rayHits = new RaycastHit[16];
        private static readonly Collider[] overlaps = new Collider[16];

        public bool IsPlaced => state == State.Placed;
        public bool IsTaken => state != State.InCrate;
        public int Owner => holderIndex;
        public bool IsFree => free;
        public LocalGrabbable Grabbable => grabbable;
        public void MarkFree() => free = true;

        public bool OverridesRelease => state == State.Held;
        public bool CanBePickedBy(int playerIndex) => state == State.InCrate && !free && rules != null && rules.CanTakeProp(playerIndex);

        // A copy of the choice's prefab on the crate's slot: its colliders
        // become one box (picked up, placed and hit by the balls as one)
        // unless it keeps its own, a kinematic body, a LocalGrabbable after
        // this (which reads it).
        public static PlayAnywhereProp Spawn(PlayAnywhereRuleSet rules, PlayAnywhereSettings settings, PlayAnywherePropChoice choice, Vector3 slot, Quaternion rotation)
        {
            GameObject go = Instantiate(choice.prefab, slot, Quaternion.identity);
            go.name = choice.prefab.name + " (Play Anywhere)";
            go.transform.localScale = choice.prefab.transform.localScale * choice.scale;
            // Two parts: only the first one counts until it's placed.
            var twoPart = go.GetComponent<IPlayAnywhereTwoPart>();
            if (twoPart != null && (twoPart.FirstPart == null || twoPart.SecondPart == null)) twoPart = null;
            if (twoPart != null) twoPart.SecondPart.gameObject.SetActive(false);

            // Measured upright, before any collider goes.
            bool any = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
            foreach (LocalGrabbable g in go.GetComponentsInChildren<LocalGrabbable>(true)) DestroyImmediate(g);
            // Its own colliders only with one on the root (LocalGrabbable
            // needs it there).
            bool keep = choice.useOwnColliders && go.GetComponent<Collider>() != null;
            if (!keep)
            {
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
                Vector3 scale = go.transform.lossyScale;
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3((b.center.x - slot.x) / scale.x, (b.center.y - slot.y) / scale.y, (b.center.z - slot.z) / scale.z);
                box.size = new Vector3(b.size.x / scale.x, b.size.y / scale.y, b.size.z / scale.z);
            }
            var solidList = new System.Collections.Generic.List<Collider>();
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) solidList.Add(c);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.isKinematic = true;

            var prop = go.AddComponent<PlayAnywhereProp>();
            prop.rules = rules;
            prop.settings = settings;
            prop.solids = solidList.ToArray();
            prop.bottomOffset = slot.y - b.min.y;
            prop.halfExtents = b.extents;
            prop.boxCenterOffset = b.center - slot;
            prop.footprintRadius = Mathf.Max(b.extents.x, b.extents.z);
            prop.grabbable = go.AddComponent<LocalGrabbable>();
            if (twoPart != null)
            {
                prop.twoPart = twoPart;
                prop.firstRestPosition = twoPart.FirstPart.localPosition;
                prop.firstRestRotation = twoPart.FirstPart.localRotation;
                prop.secondRestPosition = twoPart.SecondPart.localPosition;
                prop.secondRestRotation = twoPart.SecondPart.localRotation;
            }

            // Resting on the crate, the bottom on the slot.
            prop.crateSlot = slot + Vector3.up * prop.bottomOffset;
            prop.crateRotation = rotation;
            prop.ReturnToCrate();
            return prop;
        }

        // Diagnostics: where it is and whether anything can show it.
        public string Describe()
        {
            int shown = 0, total = 0;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                total++;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (shown == 0) b = r.bounds; else b.Encapsulate(r.bounds);
                shown++;
            }
            return $"{name} à {transform.position}, taille {b.size}, {shown}/{total} renderers visibles, actif {gameObject.activeInHierarchy}, échelle {transform.lossyScale}";
        }

        private void ReturnToCrate()
        {
            if (firstPlaced) ResetParts();
            state = State.InCrate;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(crateSlot, crateRotation);
            Freeze();
        }

        // Kinematic and solid: on the crate, and once placed. Discrete
        // first, the release left it continuous (not for kinematic bodies).
        private void Freeze()
        {
            Rigidbody rb = grabbable.Body;
            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = true;
                rb.position = transform.position;
                rb.rotation = transform.rotation;
            }
            SetSolid(true);
            IgnorePlayers();
        }

        // Players walk through it (PlayAnywhereRuleSet.IgnorePlayers).
        public void IgnorePlayers()
        {
            if (rules != null && solids != null) rules.IgnorePlayers(solids);
        }

        private void SetSolid(bool solid)
        {
            foreach (Collider c in solids)
                if (c != null) c.enabled = solid;
        }

        private void Update()
        {
            if (state == State.Placed || rules == null) return;

            if (state == State.InCrate)
            {
                if (grabbable.IsHeld) TakeHolder();
                return;
            }

            // Let go another way than placing (knocked down): back in the crate.
            if (!grabbable.IsHeld || holder == null || holder.HeldObject != grabbable)
            {
                if (settings.debugLogs) Debug.Log($"[PlayAnywhere] {name} lâché sans être posé : {(free ? "retiré (donné par le menu debug)" : "retour dans la caisse")}.", this);
                int was = holderIndex;
                ClearHolder();
                if (grabbable.IsHeld) grabbable.MarkExternallyHeld(false);
                if (free)
                {
                    Destroy(gameObject);
                    return;
                }
                ReturnToCrate();
                rules.OnPropReturned(this, was);
                return;
            }

            // Still reaching / lifting: no ghost yet, and the pickup press
            // mustn't place it.
            if (holder.HandsMoving || Time.frameCount <= heldSinceFrame)
            {
                SetGhostVisible(false);
                TraceGhost("pas d'aperçu : les mains bougent encore (prise en cours)");
                return;
            }

            yawOffset += ReadTurn();

            UpdateGhost();
            bool pressed = (placeAction != null && placeAction.WasPressedThisFrame())
                        || (interactAction != null && interactAction.WasPressedThisFrame());
            if (pressed && hasGhostPose && ghostValid) Place();
        }

        // Degrees to turn this frame: a wheel notch is a fixed step, a held
        // d-pad turns at Rotate Speed.
        private float ReadTurn()
        {
            if (rotateAction != null)
            {
                float value = rotateAction.ReadValue<float>();
                if (Mathf.Approximately(value, 0f)) return 0f;
                bool wheel = rotateAction.activeControl != null && rotateAction.activeControl.device is Mouse;
                return wheel ? Mathf.Sign(value) * settings.rotateStep : Mathf.Sign(value) * settings.rotateSpeed * Time.deltaTime;
            }
            float turn = (rotateRightAction != null && rotateRightAction.IsPressed() ? 1f : 0f)
                       - (rotateLeftAction != null && rotateLeftAction.IsPressed() ? 1f : 0f);
            return turn * settings.rotateSpeed * Time.deltaTime;
        }

        // Who picked it up: the hand controller holding it.
        private void TakeHolder()
        {
            LocalPlayerHandController[] hands = rules.Hands;
            for (int p = 0; p < hands.Length; p++)
            {
                if (hands[p] == null || hands[p].HeldObject != grabbable) continue;
                holder = hands[p];
                // Alone, the same hands place J1's object then J2's.
                holderIndex = free ? p : rules.PlacerFor(p);
                break;
            }
            if (holder == null) return;

            state = State.Held;
            heldSinceFrame = Time.frameCount;
            ghostTrace = null;
            // The hands switch off the root's colliders only.
            SetSolid(false);
            yawOffset = 0f;
            Camera cam = holder.GetComponentInChildren<Camera>(true);
            holderView = cam != null ? cam.transform : holder.transform;
            PlayerInput input = holder.GetComponent<PlayerInput>();
            InputActionMap map = input != null ? input.actions.FindActionMap("Player") : null;
            placeAction = map?.FindAction("Attack");
            interactAction = map?.FindAction("Interact");
            // The players' map has SwitchPower (wheel, d-pad); an older one
            // only Previous / Next.
            rotateAction = map?.FindAction("SwitchPower");
            rotateLeftAction = rotateAction == null ? map?.FindAction("Previous") : null;
            rotateRightAction = rotateAction == null ? map?.FindAction("Next") : null;
            if (!free) rules.OnPropTaken(this, holderIndex);
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] J{holderIndex + 1} prend {name}.", this);
        }

        private void ClearHolder()
        {
            holder = null;
            holderIndex = -1;
            SetGhostVisible(false);
        }

        // On the first static ground the player looks at, within reach,
        // turned with the view (plus the player's own turn).
        private void UpdateGhost()
        {
            hasGhostPose = false;
            Ray ray = new Ray(holderView.position, holderView.forward);
            int count = Physics.RaycastNonAlloc(ray, rayHits, settings.placeRange, ~0, QueryTriggerInteraction.Ignore);
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                // Bodies (the player's own ragdoll, balls, props) and the
                // players' capsules aren't ground.
                Collider c = rayHits[i].collider;
                if (c.attachedRigidbody != null || c is CharacterController) continue;
                if (best < 0 || rayHits[i].distance < rayHits[best].distance) best = i;
            }
            if (best < 0)
            {
                SetGhostVisible(false);
                TraceGhost($"pas d'aperçu : aucun sol à moins de {settings.placeRange:0.#} m dans la direction du regard ({count} collider(s) touché(s), tous des corps ou des joueurs ; caméra {holderView.name})");
                return;
            }

            RaycastHit hit = rayHits[best];
            float yaw = holderView.eulerAngles.y + yawOffset;
            ghostRotation = Quaternion.Euler(0f, yaw, 0f);
            ghostPosition = hit.point + Vector3.up * bottomOffset;
            hasGhostPose = true;
            ghostValid = Vector3.Angle(hit.normal, Vector3.up) <= settings.maxPlaceSlope
                      && rules.IsClearOfCourse(hit.point, footprintRadius + settings.noBuildRadius)
                      && !Overlaps(hit.collider);
            // The second part: not too far from the first.
            bool tooFar = firstPlaced && FlatDistance(hit.point, twoPart.FirstPart.position) > twoPart.MaxPartDistance;
            if (tooFar) ghostValid = false;

            EnsureGhost();
            ghost.transform.SetPositionAndRotation(ghostPosition, ghostRotation);
            Material m = ghostValid ? validMaterial : invalidMaterial;
            foreach (Renderer r in ghostRenderers)
            {
                if (r.sharedMaterial == m) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
            }
            SetGhostVisible(true);
            // No distance in the line: it would change every frame.
            TraceGhost($"aperçu sur {hit.collider.name}, {(ghostValid ? "posable" : tooFar ? $"trop loin de la première partie (plus de {twoPart.MaxPartDistance:0.#} m)" : "pas posable")} (caméra {holderView.name}, aperçu actif {ghost.activeInHierarchy})");
        }

        private void TraceGhost(string state)
        {
            if (!settings.debugLogs || state == ghostTrace) return;
            ghostTrace = state;
            Debug.Log($"[PlayAnywhere] J{holderIndex + 1}, {name} : {state}.", this);
        }

        // Inside another object of the décor, or a placed prop. Slightly
        // shrunk and lifted, or the ground under it would count.
        private bool Overlaps(Collider ground)
        {
            Vector3 center = ghostPosition + ghostRotation * boxCenterOffset + Vector3.up * 0.02f;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents * 0.95f, overlaps, ghostRotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i];
                Rigidbody body = c.attachedRigidbody;
                // Players walk through objects: one standing there is no obstacle.
                if (c == ground || body == grabbable.Body || c is CharacterController) continue;
                if (body == null || rules.IsPlacedProp(body)) return true;
            }
            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private void Place()
        {
            if (twoPart != null && !firstPlaced)
            {
                PlaceFirstPart();
                return;
            }
            LocalPlayerHandController by = holder;
            int player = holderIndex;
            by.ReleaseInPlace();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(ghostPosition, ghostRotation);
            Freeze();
            state = State.Placed;
            ClearHolder();
            holderIndex = player;
            if (ghost != null) Destroy(ghost);
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] J{player + 1} pose {name} à {ghostPosition}.", this);
            if (free) rules.OnFreePropPlaced(this);
            else rules.OnPropPlaced(this, player);
            GetComponent<IPlayAnywherePlaced>()?.OnPlaced(rules, this);
        }

        // The first part stays where the ghost was, out of the object; the
        // second shows and is held next (its own ghost, size, collider).
        private void PlaceFirstPart()
        {
            Transform first = twoPart.FirstPart;
            Vector3 scale = transform.lossyScale;
            Vector3 position = ghostPosition + ghostRotation * Vector3.Scale(first.localPosition, scale);
            Quaternion rotation = ghostRotation * first.localRotation;
            first.SetParent(null, true);
            first.SetPositionAndRotation(position, rotation);
            firstPlaced = true;

            Transform second = twoPart.SecondPart;
            second.localPosition = Vector3.zero;
            second.localRotation = Quaternion.identity;
            second.gameObject.SetActive(true);
            MeasureShown();
            if (ghost != null) Destroy(ghost);
            ghost = null;
            yawOffset = 0f;
            ghostTrace = null;
            // The same press mustn't place the second part too.
            heldSinceFrame = Time.frameCount;
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] J{holderIndex + 1} pose la première partie de {name} à {position} : pose maintenant la seconde, à {twoPart.MaxPartDistance:0.#} m au plus.", this);
        }

        // Back to one object: the first part in it again, the second hidden.
        private void ResetParts()
        {
            Transform first = twoPart.FirstPart;
            if (first != null)
            {
                first.SetParent(transform, false);
                first.localPosition = firstRestPosition;
                first.localRotation = firstRestRotation;
            }
            Transform second = twoPart.SecondPart;
            second.localPosition = secondRestPosition;
            second.localRotation = secondRestRotation;
            second.gameObject.SetActive(false);
            firstPlaced = false;
            MeasureShown();
            if (ghost != null) Destroy(ghost);
            ghost = null;
        }

        // Size and pivot height from the meshes shown now, in the object's
        // own space (it may be turned in hand); the root box follows.
        private void MeasureShown()
        {
            bool any = false;
            Bounds local = default;
            foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null) continue;
                Bounds mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = transform.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
                }
            }
            if (!any) return;
            Vector3 scale = transform.lossyScale;
            bottomOffset = -local.min.y * scale.y;
            halfExtents = Vector3.Scale(local.extents, scale);
            boxCenterOffset = Vector3.Scale(local.center, scale);
            footprintRadius = Mathf.Max(halfExtents.x, halfExtents.z);
            if (solids.Length > 0 && solids[0] is BoxCollider box)
            {
                box.center = local.center;
                box.size = local.size;
            }
        }

        // Within radius, on the ground (either part of a two-part object).
        public bool Near(Vector3 center, float radius) =>
            FlatDistance(transform.position, center) <= radius
            || (firstPlaced && twoPart.FirstPart != null && FlatDistance(twoPart.FirstPart.position, center) <= radius);

        // A copy of the meshes, translucent, no colliders or shadows.
        private void EnsureGhost()
        {
            if (ghost != null) return;
            if (validMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                validMaterial = new Material(shader) { color = settings.ghostValidColor, renderQueue = 3000 };
                invalidMaterial = new Material(shader) { color = settings.ghostInvalidColor, renderQueue = 3000 };
            }
            ghost = new GameObject(name + " (aperçu)");
            Transform root = transform;
            Vector3 rootScale = root.lossyScale;
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();
            ghostRenderers = new Renderer[filters.Length];
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter f = filters[i];
                var part = new GameObject(f.name);
                part.transform.SetParent(ghost.transform, false);
                part.transform.localPosition = root.InverseTransformPoint(f.transform.position);
                part.transform.localRotation = Quaternion.Inverse(root.rotation) * f.transform.rotation;
                Vector3 s = f.transform.lossyScale;
                part.transform.localScale = new Vector3(s.x / rootScale.x, s.y / rootScale.y, s.z / rootScale.z);
                part.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                var r = part.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                int subMeshes = f.sharedMesh != null ? Mathf.Max(1, f.sharedMesh.subMeshCount) : 1;
                var mats = new Material[subMeshes];
                for (int k = 0; k < subMeshes; k++) mats[k] = validMaterial;
                r.sharedMaterials = mats;
                ghostRenderers[i] = r;
            }
            ghost.transform.localScale = rootScale;

            // The destroyer shows its reach: a ring on the ground.
            if (TryGetComponent(out PlayAnywhereDestroyer _)) AddRadiusRing(ghost.transform, settings.destroyRadius / Mathf.Max(0.01f, rootScale.x));
        }

        private static void AddRadiusRing(Transform parent, float radius)
        {
            const int Segments = 48;
            var ring = new GameObject("Rayon");
            ring.transform.SetParent(parent, false);
            var line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = 0.03f;
            line.sharedMaterial = invalidMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.02f, Mathf.Sin(a) * radius));
            }
        }

        private void SetGhostVisible(bool visible)
        {
            if (ghost != null && ghost.activeSelf != visible) ghost.SetActive(visible);
        }

        // Not taken by the end of the round, or the hole is over.
        public void Remove()
        {
            if (state == State.Held && holder != null) holder.ReleaseInPlace();
            ClearHolder();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (ghost != null) Destroy(ghost);
            // The first part, left on the ground, goes with it.
            if (firstPlaced && twoPart.FirstPart != null) Destroy(twoPart.FirstPart.gameObject);
        }
    }
}
