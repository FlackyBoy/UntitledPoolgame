using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // What the Shooter's powers show, made at runtime as the shots are.
    public static class ShooterPowerFx
    {
        private const string LitShader = "Universal Render Pipeline/Lit";

        public static Material Solid(Color color, string name)
        {
            var material = new Material(Shader.Find(LitShader)) { name = name };
            material.SetColor("_BaseColor", color);
            return material;
        }

        // See-through, for the ice.
        public static Material Glass(Color color, string name)
        {
            Material material = Solid(color, name);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        // A shape with nothing physical about it. Its material goes with it.
        public static GameObject Shape(PrimitiveType type, Material material, string name)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Object.Destroy(shape.GetComponent<Collider>());
            shape.GetComponent<Renderer>().sharedMaterial = material;
            shape.AddComponent<OwnedMaterial>().material = material;
            return shape;
        }

        public static GameObject Effect(GameObject prefab, Vector3 position, float scale, float lifetime)
        {
            if (prefab == null) return null;
            GameObject effect = Object.Instantiate(prefab, position, prefab.transform.rotation);
            effect.transform.localScale = prefab.transform.localScale * scale;
            if (lifetime > 0f) Object.Destroy(effect, lifetime);
            return effect;
        }

        private class OwnedMaterial : MonoBehaviour
        {
            public Material material;
            private void OnDestroy() { if (material != null) Destroy(material); }
        }
    }
}
