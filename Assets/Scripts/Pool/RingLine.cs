using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A flat circle drawn with a LineRenderer — the ring around a pocket
    // being picked (PoolPocket), the preview of where a table power lands
    // (LocalPocketPointer) and the burning rim of a table hole (TableHole).
    public static class RingLine
    {
        public const int Segments = 40;

        // One shared unlit, colour-tinted material (the aim preview lines'
        // Sprites/Default) for every ring.
        private static Material material;

        public static LineRenderer Create(Transform parent, string name, float width, Color color)
        {
            var ringObject = new GameObject(name);
            ringObject.transform.SetParent(parent, worldPositionStays: false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.widthMultiplier = width;
            ring.startColor = ring.endColor = color;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            ring.sharedMaterial = material;
            return ring;
        }

        // Horizontal circle of the given radius around center.
        public static void Set(LineRenderer ring, Vector3 center, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }
    }
}
