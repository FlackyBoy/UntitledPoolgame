using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Sumo Pool's small square table, made at runtime over the real one: six
    // pockets, rails broken at each, a base and legs. Nothing placed in a scene
    // or prefab, so it fits any level with a table. The balls roll on the real
    // table's felt collider, which is why there's none here, level with it.
    //
    // As on a real table, the middle pockets are on the two sides along its X
    // axis.
    public class SumoPoolTable : MonoBehaviour
    {
        private const float BaseThickness = 0.09f;
        private const float FeltThickness = 0.006f;
        private const float BaseOverhang = 0.012f;
        private const float LegWidth = 0.07f;
        // Behind the rails and the pockets, for a ball thrown over them.
        private const float WallHeight = 0.5f;
        // The hole's disc, just above the felt.
        private const float DiscLift = 0.0015f;
        private const float DefaultLegHeight = 0.8f;

        private readonly List<Object> owned = new List<Object>();

        public float HalfSide { get; private set; }

        // Centered on the real table, at the height of its felt. tableRoot: what
        // the search for the floor under it must not stop on. ballRadius: the
        // players' balls.
        public static SumoPoolTable Build(PoolTableSurface surface, Transform tableRoot, SumoPoolSettings s, float ballRadius)
        {
            GameObject root = new GameObject("Sumo Pool Table");
            Vector3 center = surface.transform.position;
            center.y = surface.FeltTopY;
            root.transform.SetPositionAndRotation(center, surface.transform.rotation);
            SumoPoolTable table = root.AddComponent<SumoPoolTable>();
            float scale = surface.TableScale;
            table.HalfSide = Mathf.Min(s.tableSide * scale, surface.HalfWidth * 2f) * 0.5f;
            table.Create(surface.gameObject.layer, tableRoot, s, scale, ballRadius);
            return table;
        }

        private void Create(int layer, Transform tableRoot, SumoPoolSettings s, float scale, float ballRadius)
        {
            float half = HalfSide;
            float rail = s.railWidth * scale;
            float railHeight = s.railHeight * scale;
            float hole = s.holeRadius * scale;
            float outer = half + rail;

            Material felt = Solid(s.feltColor, "Sumo Pool Felt");
            Material wood = Solid(s.railColor, "Sumo Pool Rail");
            Material black = Solid(Color.black, "Sumo Pool Hole");
            PhysicsMaterial bounce = new PhysicsMaterial("Sumo Pool Rail")
            {
                bounciness = s.railBounciness,
                dynamicFriction = 0.15f,
                staticFriction = 0.15f,
                // As the table's rails: wins over the balls' Average.
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };
            owned.Add(bounce);

            float thickFelt = FeltThickness * scale;
            float thickBase = BaseThickness * scale;
            float baseSize = (outer + BaseOverhang * scale) * 2f;
            // Before the shapes exist: their colliders only go at the end of the
            // frame, and the ray starts right under the base.
            float legHeight = LegHeight(tableRoot, thickFelt + thickBase, scale);
            Shape(PrimitiveType.Cube, felt, "Felt", new Vector3(0f, -thickFelt * 0.5f, 0f), new Vector3(outer * 2f, thickFelt, outer * 2f));
            Shape(PrimitiveType.Cube, wood, "Base", new Vector3(0f, -thickFelt - thickBase * 0.5f, 0f), new Vector3(baseSize, thickBase, baseSize));
            CreateLegs(wood, legHeight, outer, thickFelt + thickBase, scale);

            // Rails stop where a pocket starts.
            float end = half - hole;
            float railY = railHeight * 0.5f;
            float railMiddle = half + rail * 0.5f;
            float wallMiddle = outer + rail * 0.5f;
            float wallHeight = WallHeight * scale;
            float wallY = wallHeight * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                // No middle pocket on these two.
                CreateRail(wood, bounce, layer, new Vector3(side * railMiddle, railY, 0f), new Vector3(rail, railHeight, end * 2f));
                // Either side of the middle pocket on these.
                float length = end - hole;
                float middle = hole + length * 0.5f;
                CreateRail(wood, bounce, layer, new Vector3(-middle, railY, side * railMiddle), new Vector3(length, railHeight, rail));
                CreateRail(wood, bounce, layer, new Vector3(middle, railY, side * railMiddle), new Vector3(length, railHeight, rail));

                CreateWall(bounce, layer, new Vector3(side * wallMiddle, wallY, 0f), new Vector3(rail, wallHeight, outer * 2f));
                CreateWall(bounce, layer, new Vector3(0f, wallY, side * wallMiddle), new Vector3(outer * 2f, wallHeight, rail));

                CreatePocket(black, layer, new Vector3(side * half, 0f, -half), hole, ballRadius, scale);
                CreatePocket(black, layer, new Vector3(side * half, 0f, half), hole, ballRadius, scale);
                CreatePocket(black, layer, new Vector3(0f, 0f, side * half), hole, ballRadius, scale);
            }
        }

        private void CreateRail(Material material, PhysicsMaterial bounce, int layer, Vector3 position, Vector3 size)
        {
            GameObject rail = Shape(PrimitiveType.Cube, material, "Rail", position, size, solid: true);
            rail.GetComponent<Collider>().sharedMaterial = bounce;
            rail.layer = layer;
        }

        private void CreateWall(PhysicsMaterial bounce, int layer, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject("Wall");
            wall.transform.SetParent(transform, false);
            wall.transform.localPosition = position;
            wall.layer = layer;
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = bounce;
        }

        // A sphere trigger at felt height, sized so a ball sitting on the felt
        // touches it as its centre crosses the edge of the hole: with the
        // hole's radius, balls went in well before the edge.
        private void CreatePocket(Material black, int layer, Vector3 position, float hole, float ballRadius, float scale)
        {
            Shape(PrimitiveType.Cylinder, black, "Hole", position + Vector3.up * (DiscLift * scale), new Vector3(hole * 2f, DiscLift * scale, hole * 2f));

            GameObject pocket = new GameObject("Pocket");
            pocket.transform.SetParent(transform, false);
            pocket.transform.localPosition = position;
            pocket.layer = layer;
            SphereCollider trigger = pocket.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Sqrt(hole * hole + ballRadius * ballRadius) - ballRadius;
            pocket.AddComponent<PoolPocket>();
        }

        private void CreateLegs(Material material, float height, float outer, float top, float scale)
        {
            float width = LegWidth * scale;
            float inset = outer - width * 0.5f;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Shape(PrimitiveType.Cube, material, "Leg", new Vector3(x * inset, -top - height * 0.5f, z * inset), new Vector3(width, height, width));
        }

        // Down to the floor, whatever the table's height in this level.
        private float LegHeight(Transform tableRoot, float top, float scale)
        {
            Vector3 from = transform.position + Vector3.down * top;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 5f * scale, ~0, QueryTriggerInteraction.Ignore))
                if (hit.distance < nearest && !hit.transform.IsChildOf(tableRoot)) nearest = hit.distance;
            return nearest < float.MaxValue ? nearest : DefaultLegHeight * scale;
        }

        private Material Solid(Color color, string name)
        {
            Material material = ShooterPowerFx.Solid(color, name);
            owned.Add(material);
            return material;
        }

        private GameObject Shape(PrimitiveType type, Material material, string name, Vector3 position, Vector3 size, bool solid = false)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            if (!solid) Destroy(shape.GetComponent<Collider>());
            shape.transform.SetParent(transform, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = size;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }

        private void OnDestroy()
        {
            foreach (Object thing in owned)
                if (thing != null) Destroy(thing);
        }
    }
}
