using UnityEngine;

namespace UntitledPoolGame.Core
{
    // The original color and intensity of a decor lamp, so ambiences tint
    // and scale it from its own values instead of compounding on the last
    // ambience's. Added by AmbienceController the first time it touches the
    // light.
    [RequireComponent(typeof(Light))]
    public class AmbienceLight : MonoBehaviour
    {
        public Color baseColor = Color.white;
        public float baseIntensity = 1f;
        public bool captured;

        public void Capture(Light light)
        {
            baseColor = light.color;
            baseIntensity = light.intensity;
            captured = true;
        }
    }
}
