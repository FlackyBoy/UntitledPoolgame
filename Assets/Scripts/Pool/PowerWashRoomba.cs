using System.Collections;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Power Wash Pool, Roomba power (RoombaPower): a little robot vacuum on
    // the felt that washes in its owner's colour. It drives straight until a
    // cushion or a pocket is in the way, turns on the spot towards a new
    // heading (rather towards the middle of the table), and drives on. A
    // kinematic body: it pushes the balls it runs into. A child of the grime
    // (PowerWashDirt), so it goes with it (Rejouer, another mode); gone at
    // the end of its time.
    public class PowerWashRoomba : MonoBehaviour
    {
        private int owner;
        private float radius, speed, turnSpeed, washWidth, lifeLeft;
        private Rigidbody body;
        private Vector3 position, lastWashed;
        private float heading, targetHeading;
        private bool turning;
        private Transform visual;
        // One per part (renderer.material): destroyed with the robot.
        private readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();

        // Grows in, shrinks out; never from 0 (a convex collider can't be
        // scaled to nothing).
        private const float PopTime = 0.2f, PopMin = 0.05f;

        public static PowerWashRoomba Spawn(PowerWashDirt dirt, int owner, Vector3 point, Color colour,
                                            float radius, float speed, float turnSpeed, float washWidth, float duration)
        {
            var go = new GameObject($"Roomba J{owner + 1}");
            go.transform.SetParent(dirt.transform, true);
            var roomba = go.AddComponent<PowerWashRoomba>();
            roomba.Init(owner, point, colour, radius, speed, turnSpeed, washWidth, duration);
            return roomba;
        }

        private void Init(int player, Vector3 point, Color colour, float r, float driveSpeed, float turnDegrees, float width, float duration)
        {
            owner = player;
            radius = r;
            speed = driveSpeed;
            turnSpeed = turnDegrees;
            washWidth = width;
            lifeLeft = duration;

            PoolTableSurface surface = PoolTableSurface.Instance;
            position = surface != null ? TablePointPower.ClampToTable(point, r + 0.01f) : point;
            lastWashed = position;
            transform.position = position;
            // First heading: towards the middle, give or take.
            Vector3 toMiddle = surface != null ? surface.transform.position - position : Vector3.forward;
            heading = Mathf.Atan2(toMiddle.x, toMiddle.z) * Mathf.Rad2Deg + Random.Range(-60f, 60f);
            transform.rotation = Quaternion.Euler(0f, heading, 0f);

            body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            BuildLook(colour);
            StartCoroutine(Pop(PopMin, 1f));
        }

        // Primitives only: a dark disc, a light top, a band in the owner's
        // colour and a bumper at the front to show where it's going.
        private void BuildLook(Color colour)
        {
            visual = new GameObject("Look").transform;
            visual.SetParent(transform, false);
            float h = radius * 0.45f;
            Part("Body", PrimitiveType.Cylinder, new Vector3(0f, h * 0.5f, 0f), new Vector3(radius * 2f, h * 0.5f, radius * 2f), new Color(0.12f, 0.12f, 0.14f), collider: true);
            Part("Band", PrimitiveType.Cylinder, new Vector3(0f, h * 0.55f, 0f), new Vector3(radius * 2.04f, h * 0.12f, radius * 2.04f), colour, collider: false);
            Part("Top", PrimitiveType.Cylinder, new Vector3(0f, h, 0f), new Vector3(radius * 1.6f, h * 0.06f, radius * 1.6f), new Color(0.75f, 0.76f, 0.8f), collider: false);
            Part("Button", PrimitiveType.Cylinder, new Vector3(0f, h * 1.06f, radius * 0.45f), new Vector3(radius * 0.35f, h * 0.06f, radius * 0.35f), colour, collider: false);
            Part("Bumper", PrimitiveType.Cube, new Vector3(0f, h * 0.45f, radius * 0.92f), new Vector3(radius * 1.3f, h * 0.5f, radius * 0.2f), new Color(0.3f, 0.3f, 0.33f), collider: false);
        }

        private void Part(string name, PrimitiveType type, Vector3 at, Vector3 scale, Color colour, bool collider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(visual, false);
            part.transform.localPosition = at;
            part.transform.localScale = scale;
            // The body pushes the balls with a convex cylinder (the
            // primitive's capsule would be a ball at this height).
            Collider own = part.GetComponent<Collider>();
            if (own != null) Destroy(own);
            if (collider)
            {
                var hull = part.AddComponent<MeshCollider>();
                hull.sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
                hull.convex = true;
            }
            var meshRenderer = part.GetComponent<Renderer>();
            meshRenderer.material.color = colour;
            materials.Add(meshRenderer.material);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private IEnumerator Pop(float from, float to)
        {
            for (float t = 0f; t < PopTime; t += Time.deltaTime)
            {
                visual.localScale = Vector3.one * Mathf.SmoothStep(from, to, t / PopTime);
                yield return null;
            }
            visual.localScale = Vector3.one * to;
            if (to <= PopMin) Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            PoolMatchRules match = PoolMatchRules.Instance;
            if (lifeLeft <= 0f || match == null || match.GameOver) return;
            float dt = Time.fixedDeltaTime;
            lifeLeft -= dt;
            if (lifeLeft <= 0f)
            {
                // Out of the way of the balls while it shrinks.
                foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
                StartCoroutine(Pop(1f, PopMin));
                return;
            }

            if (turning)
            {
                heading = Mathf.MoveTowardsAngle(heading, targetHeading, turnSpeed * dt);
                if (Mathf.Abs(Mathf.DeltaAngle(heading, targetHeading)) < 0.5f) turning = false;
            }
            else
            {
                Vector3 forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                Vector3 next = position + forward * speed * dt;
                if (Blocked(next)) StartTurn();
                else position = next;
            }
            body.MovePosition(position);
            body.MoveRotation(Quaternion.Euler(0f, heading, 0f));
        }

        private void Update()
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (dirt == null || lifeLeft <= 0f) return;
            Vector3 now = transform.position;
            dirt.Wash(lastWashed, now, radius * washWidth, owner);
            lastWashed = now;
        }

        private void OnDestroy()
        {
            foreach (Material m in materials) Destroy(m);
        }

        // Out of the felt (radius from the cushions) or into a pocket.
        private bool Blocked(Vector3 point)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return false;
            Vector3 clamped = TablePointPower.ClampToTable(point, radius + 0.005f);
            return new Vector2(clamped.x - point.x, clamped.z - point.z).sqrMagnitude > 1e-8f;
        }

        // A new heading, rather towards the middle of the table, turned to on
        // the spot.
        private void StartTurn()
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 toMiddle = surface != null ? surface.transform.position - position : -transform.forward;
            float middle = Mathf.Atan2(toMiddle.x, toMiddle.z) * Mathf.Rad2Deg;
            targetHeading = middle + Random.Range(-75f, 75f);
            turning = true;
        }
    }
}
