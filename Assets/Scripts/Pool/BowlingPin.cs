using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // One pin of Bowling Pool, on the Poolball layer: PhysicsLayerSetup keeps
    // the players and the cue out of it, as with the balls. Knocked over,
    // potted or off the table, BowlingPoolRuleSet counts it down.
    public class BowlingPin : MonoBehaviour
    {
        // Side view of a pin: height, then radius, both from 0 to 1. The flat
        // base is narrower than the belly: it can wobble and go over.
        private static readonly Vector2[] Profile =
        {
            new Vector2(0f, 0f), new Vector2(0f, 0.55f), new Vector2(0.04f, 0.72f), new Vector2(0.14f, 0.92f),
            new Vector2(0.26f, 1f), new Vector2(0.38f, 0.94f), new Vector2(0.5f, 0.66f), new Vector2(0.6f, 0.44f),
            new Vector2(0.63f, 0.4f), new Vector2(0.66f, 0.38f), new Vector2(0.7f, 0.38f), new Vector2(0.73f, 0.4f),
            new Vector2(0.8f, 0.5f), new Vector2(0.88f, 0.52f), new Vector2(0.95f, 0.4f), new Vector2(0.985f, 0.22f),
            new Vector2(1f, 0f),
        };

        // The two red collars on the neck: the slices of Profile they cover.
        private const int CollarSliceA = 7;
        private const int CollarSliceB = 10;

        private Rigidbody rb;

        public Rigidbody Rigidbody => rb;
        public Vector3 RackSpot { get; set; }

        public bool IsMoving(float maxSpeed, float maxSpin) =>
            rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed || rb.angularVelocity.sqrMagnitude > maxSpin * maxSpin;

        public bool IsTippedOver(float cosMaxTilt) => Vector3.Dot(transform.up, Vector3.up) < cosMaxTilt;

        // Upright on its spot, asleep: a pin wobbling from being placed
        // could fall by itself. A hit wakes it up.
        public void StandAt(Vector3 spot)
        {
            gameObject.SetActive(true);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = spot;
            rb.rotation = Quaternion.identity;
            transform.SetPositionAndRotation(spot, Quaternion.identity);
            rb.Sleep();
        }

        // The felt has no holes: a pin doesn't drop into a pocket, it goes away
        // as soon as it reaches one. Inactive = out of the rack until it's redone.
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PoolPocket pocket) && !pocket.IsClosed) gameObject.SetActive(false);
        }

        public static BowlingPin Create(Transform parent, Mesh mesh, Mesh colliderMesh, Material[] materials, PhysicsMaterial surface, float mass, float centerHeight, BowlingPoolSettings settings)
        {
            var pin = new GameObject("BowlingPin").AddComponent<BowlingPin>();
            pin.gameObject.layer = LayerMask.NameToLayer("Poolball");
            pin.transform.SetParent(parent, false);
            pin.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            pin.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;

            var meshCollider = pin.gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.sharedMesh = colliderMesh;
            meshCollider.sharedMaterial = surface;

            pin.rb = pin.gameObject.AddComponent<Rigidbody>();
            pin.rb.mass = mass;
            pin.rb.centerOfMass = Vector3.up * centerHeight;
            pin.rb.linearDamping = settings.pinLinearDamping;
            pin.rb.angularDamping = settings.pinAngularDamping;
            // The cue ball is fast and the pin thin: as with the balls, it would
            // go through without this.
            pin.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            pin.rb.interpolation = RigidbodyInterpolation.Interpolate;
            return pin;
        }

        // A pin turned around its height: its base at y = 0. Submesh 0 is the
        // body, 1 the collars.
        public static Mesh BuildMesh(float height, float radius, int sides)
        {
            int rings = Profile.Length;
            var vertices = new Vector3[rings * sides];
            for (int ring = 0; ring < rings; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    float r = Profile[ring].y * radius;
                    vertices[ring * sides + side] = new Vector3(Mathf.Cos(angle) * r, Profile[ring].x * height, Mathf.Sin(angle) * r);
                }
            }

            int collarQuads = sides * 2;
            int bodyQuads = sides * (rings - 1) - collarQuads;
            var body = new int[bodyQuads * 6];
            var collars = new int[collarQuads * 6];
            int bodyAt = 0, collarAt = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                bool collar = ring == CollarSliceA || ring == CollarSliceB;
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int a = ring * sides + side, b = ring * sides + next;
                    int c = (ring + 1) * sides + side, d = (ring + 1) * sides + next;
                    int[] target = collar ? collars : body;
                    int at = collar ? collarAt : bodyAt;
                    target[at] = a; target[at + 1] = c; target[at + 2] = d;
                    target[at + 3] = a; target[at + 4] = d; target[at + 5] = b;
                    if (collar) collarAt += 6;
                    else bodyAt += 6;
                }
            }

            var mesh = new Mesh { name = "BowlingPin", vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(collars, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
