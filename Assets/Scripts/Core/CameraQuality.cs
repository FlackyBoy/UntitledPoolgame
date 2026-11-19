using UnityEngine;
using UnityEngine.Rendering.Universal;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Applies RenderQualitySettings (post-processing, anti-aliasing) to the
    // camera it sits on, at start and whenever the quality level changes, so
    // a future options menu only has to call QualitySettings.SetQualityLevel.
    // Added from code to each player's camera (LocalFpsPlayerController).
    [RequireComponent(typeof(Camera))]
    public class CameraQuality : MonoBehaviour
    {
        private static RenderQualitySettings settings;

        private UniversalAdditionalCameraData cameraData;

        private void OnEnable()
        {
            if (settings == null) settings = PoolSettingsLoader.LoadOrDefault<RenderQualitySettings>("RenderQualitySettings");
            if (cameraData == null) cameraData = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            Apply();
            QualitySettings.activeQualityLevelChanged += OnQualityChanged;
        }

        private void OnDisable() => QualitySettings.activeQualityLevelChanged -= OnQualityChanged;

        private void OnQualityChanged(int previous, int current) => Apply();

        private void Apply()
        {
            RenderQualitySettings.Level level = settings.ForCurrentQuality();
            cameraData.renderPostProcessing = level.postProcessing;
            cameraData.antialiasing = level.antialiasing;
            cameraData.antialiasingQuality = level.smaaQuality;
        }
    }
}
