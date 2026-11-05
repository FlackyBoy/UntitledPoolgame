using UnityEngine;

namespace UntitledPoolGame.Core
{
    // A neon tube: its tubes glow in one color and its lamps light the room
    // in the same one. Several lamps along the tube, because URP has no
    // realtime area light and a single point light shines from the middle
    // only. Applied in edit mode too, so the Level Maker and the
    // Inspector show the color right away. Through a property block, so each
    // neon has its own color with a single shared material (never saved).
    [ExecuteAlways]
    public class NeonLight : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Couleur du tube et de la lumière qu'il donne.")]
        [SerializeField] private Color color = new Color(1f, 0.15f, 0.55f);
        [Tooltip("Éclat du tube. Au-dessus de 1, il déborde avec le Bloom du post-process.")]
        [SerializeField, Min(0f)] private float tubeBrightness = 3f;
        [Tooltip("Intensité totale de la lumière donnée à la pièce, répartie entre les lumières le long du tube.")]
        [SerializeField, Min(0f)] private float lightIntensity = 1.5f;
        [Tooltip("Portée de la lumière, en mètres.")]
        [SerializeField, Min(0f)] private float lightRange = 6f;
        [Tooltip("Distance entre les lumières et la surface du néon, en mètres. Plus loin = tache plus douce et moins brûlée sur le mur, sans changer l'éclairage du reste de la pièce.")]
        [SerializeField, Min(0f)] private float lampDistance = 1f;
        [Tooltip("La lumière projette des ombres. Coûteux : chacune des trois lumières du tube en calcule six par caméra, et l'écran partagé a deux caméras.")]
        [SerializeField] private bool castShadows;

        [Tooltip("Ce qui brille (matériau Unlit d'URP).")]
        public Renderer[] tubes;
        [Tooltip("La lumière du milieu du tube.")]
        public Light lamp;
        [Tooltip("Les autres lumières, le long du tube.")]
        public Light[] sideLamps;

        private MaterialPropertyBlock block;

        // What the Level Maker's Lights tool sets on a neon it places.
        public void Configure(Color newColor, float intensity, bool shadows)
        {
            color = newColor;
            lightIntensity = intensity;
            castShadows = shadows;
            Apply();
        }

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            float share = lightIntensity / (1 + (sideLamps != null ? sideLamps.Length : 0));
            ApplyLamp(lamp, share);
            if (sideLamps != null)
                foreach (Light side in sideLamps)
                    ApplyLamp(side, share);
            if (tubes == null) return;

            block ??= new MaterialPropertyBlock();
            // Already linear: SetColor would convert the over-bright value
            // too, and turn the brightness into its power of 2.2.
            Color glow = color.linear * tubeBrightness;
            block.SetVector(BaseColorId, new Vector4(glow.r, glow.g, glow.b, 1f));
            foreach (Renderer tube in tubes)
                if (tube != null) tube.SetPropertyBlock(block);
        }

        private void ApplyLamp(Light target, float intensity)
        {
            if (target == null) return;
            target.color = color;
            target.intensity = intensity;
            target.range = lightRange;
            target.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            // Light falls off with the square of the distance: the surface
            // right behind a close lamp burns while the far room barely
            // changes, so moving the lamps away softens only the hot spot.
            Vector3 position = target.transform.localPosition;
            position.y = lampDistance;
            target.transform.localPosition = position;
        }
    }
}
