#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TinCan.Features.CloudBoundary.Atmosphere
{
    /// <summary>
    /// Scene-embedded tuning rig for the volumetric clouds Volume override and the sun light.
    /// Writes into the assigned VolumeProfile's VolumetricClouds component and the Light every
    /// time a field changes (Edit mode via ExecuteAlways, or Play mode), so both can be dialed
    /// in from one Inspector instead of hunting through the profile asset and the light separately.
    /// </summary>
    [ExecuteAlways]
    public class CloudAtmosphereDirector : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private VolumeProfile? _cloudsProfile;
        [SerializeField] private Light? _sunLight;
        [Tooltip("The VolumetricCloudsURP renderer feature sub-asset on the URP renderer, for resolution/upscale quality settings.")]
        [SerializeField] private VolumetricCloudsURP? _rendererFeature;

        [Header("Cloud Enable")]
        [SerializeField] private bool _cloudsEnabled = true;
        [Tooltip("Local clouds use the camera's actual position and real scene depth, so the ship can fly through them. Off renders them as a fixed distant backdrop instead.")]
        [SerializeField] private bool _localClouds = true;

        [Header("Cloud Density & Shape")]
        [Tooltip("Overall opacity. Lower is more see-through/wispy; 1 is a solid wall.")]
        [Range(0f, 1f)] [SerializeField] private float _densityMultiplier = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float _shapeFactor = 0.9f;
        [Min(0.1f)] [SerializeField] private float _shapeScale = 100f;
        [Tooltip("How much surface detail is carved into the bulk shape. Near 0 gives smooth, blurry-looking blobs; higher gives the 'cauliflower' cloud texture.")]
        [Range(0f, 1f)] [SerializeField] private float _erosionFactor = 0.8f;
        [Tooltip("How finely that detail repeats. Low values (near the floor) barely vary across the cloud, which mutes the effect of Erosion Factor even when it's high.")]
        [Min(1f)] [SerializeField] private float _erosionScale = 2140f;
        [Tooltip("Adds a finer layer of detail on top of erosion. Only takes effect when Micro Erosion Enabled is on.")]
        [Min(0.1f)] [SerializeField] private float _microErosionScale = 4000f;
        [Tooltip("Enables the micro erosion detail layer (finer than Erosion). Noticeably more expensive.")]
        [SerializeField] private bool _microErosionEnabled = true;
        [Range(0f, 1f)] [SerializeField] private float _microErosionFactor = 0.5f;

        [Header("Cloud Altitude")]
        [Tooltip("World-space Y where the cloud layer begins.")]
        [Min(0.01f)] [SerializeField] private float _bottomAltitude = 10f;
        [Tooltip("Thickness of the cloud layer in world units, starting at Bottom Altitude.")]
        [Min(100f)] [SerializeField] private float _altitudeRange = 100f;

        [Header("Cloud Wind")]
        [SerializeField] private float _globalSpeed = 30f;
        [Range(0f, 360f)] [SerializeField] private float _globalOrientation = 45f;

        [Header("Cloud Shape Evolution")]
        [Tooltip("How fast the large-scale shape noise scrolls, relative to Global Speed.")]
        [Range(0f, 1f)] [SerializeField] private float _shapeSpeedMultiplier = 1f;
        [Tooltip("How fast the erosion (surface detail) noise scrolls, relative to Global Speed. Kept lower than Shape Speed Multiplier so detail drifts independently of the bulk shape instead of translating rigidly with it.")]
        [Range(0f, 1f)] [SerializeField] private float _erosionSpeedMultiplier = 0.25f;
        [Tooltip("Vertical drift speed of the large-scale shape, in the same units as Global Speed.")]
        [SerializeField] private float _verticalShapeWindSpeed = 3f;
        [Tooltip("Vertical drift speed of the erosion detail. Different from the shape's vertical speed so detail billows relative to the bulk shape over time, instead of the whole cloud just sliding sideways.")]
        [SerializeField] private float _verticalErosionWindSpeed = 8f;
        [Tooltip("How much wind distorts the cloud vertically as it moves.")]
        [Range(-1f, 1f)] [SerializeField] private float _altitudeDistortion = 0.25f;

        [Header("Cloud Lighting Response")]
        [Tooltip("Ambient/sky light contribution. Low values are the usual cause of clouds looking unlit except where the sun highlight lands directly.")]
        [Range(0f, 10f)] [SerializeField] private float _ambientLightProbeDimmer = 1.3f;
        [Tooltip("Magnifies how much the sun's intensity/angle affects cloud brightness, independent of the scene Light itself. 1 = physically matched; higher exaggerates it.")]
        [Range(0f, 10f)] [SerializeField] private float _sunLightDimmer = 1f;
        [Tooltip("Internal light bounce inside the cloud. Higher fills in the shadowed side instead of leaving it dark between sun highlights.")]
        [Range(0f, 1f)] [SerializeField] private float _multiScattering = 0.8f;
        [Tooltip("Backscatter darkening near the light source. Can partially cancel out the ambient/multi-scattering fill if pushed too high.")]
        [Range(0f, 1f)] [SerializeField] private float _powderEffectIntensity = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float _erosionOcclusion = 0.1f;
        [Tooltip("Width of the sun highlight (forward scattering lobe). Was hardcoded at 0.7 in the vendored shader; lower values spread the highlight out instead of a sharp, distinct hotspot.")]
        [Range(0f, 1f)] [SerializeField] private float _forwardEccentricity = 0.4f;
        [Tooltip("Width of the back-scattering lobe (visible looking toward the sun through the cloud).")]
        [Range(0f, 1f)] [SerializeField] private float _backwardEccentricity = 0.5f;

        [Header("Cloud Quality (edge/noise artifacts)")]
        [Tooltip("Raymarch step count for density. Low values under-sample sharp density gradients, showing up as noisy/aliased silhouette edges.")]
        [Range(24, 256)] [SerializeField] private int _numPrimarySteps = 64;
        [Tooltip("Raymarch step count for self-shadowing/lighting. Low values cause blotchy, inconsistent lighting across the cloud surface.")]
        [Range(1, 16)] [SerializeField] private int _numLightSteps = 6;
        [Tooltip("Caps the world-space distance per raymarch step. Was hardcoded as Altitude Range / 8 in the vendored shader, so a tall Altitude Range forced very coarse steps that more primary steps couldn't fix once the cap was hit. Lower is finer/less noisy but costs more.")]
        [Range(1f, 100f)] [SerializeField] private float _maxStepSize = 15f;
        [Tooltip("Render resolution scale on the renderer feature (not the Volume). Higher reduces blocky upscale artifacts at a performance cost.")]
        [Range(0.25f, 1f)] [SerializeField] private float _resolutionScale = 0.75f;
        [Tooltip("Bilateral filters more aggressively to hide noise from a low resolution scale; Bilinear is cheaper but noisier.")]
        [SerializeField] private VolumetricCloudsURP.CloudsUpscaleMode _upscaleMode = VolumetricCloudsURP.CloudsUpscaleMode.Bilateral;
        [Tooltip("How much of the previous frame's cloud shape blends into the current one. Reduces noise, but too high combined with fast wind/shape animation shows up as a duplicated, trailing 'staircase' edge behind moving cloud silhouettes (ghosting).")]
        [Range(0f, 1f)] [SerializeField] private float _temporalAccumulationFactor = 0.7f;

        [Header("Sun Light")]
        [Min(0f)] [SerializeField] private float _sunIntensity = 1f;
        [ColorUsage(false, true)] [SerializeField] private Color _sunColor = Color.white;

        private VolumetricClouds? _clouds;

        private void OnEnable() => Apply(markDirty: true);

        private void OnValidate() => Apply(markDirty: true);

        // Reapplied every frame (not just on Inspector change) because
        // VolumetricCloudsVolumeEditor fights back: it re-forces several fields to the
        // selected preset's values whenever its own Inspector redraws. Marking the targets
        // dirty every frame would falsely show the scene as having unsaved changes even when
        // nothing actually changed, so that only happens from OnEnable/OnValidate.
        private void Update() => Apply(markDirty: false);

        private void Apply(bool markDirty)
        {
            ApplyClouds(markDirty);
            ApplyRendererFeature(markDirty);
            ApplyLight(markDirty);
        }

        private void ApplyClouds(bool markDirty)
        {
            if (_cloudsProfile == null)
            {
                return;
            }

            if (_clouds == null && !_cloudsProfile.TryGet(out _clouds))
            {
                return;
            }

            if (_clouds == null)
            {
                return;
            }

            // VolumetricCloudsVolumeEditor re-forces bottomAltitude/altitudeRange (and several
            // other fields) back to the selected preset's hardcoded values every time its
            // Inspector redraws, unless the preset is Custom. Force Custom so our values stick.
            _clouds.cloudPreset = VolumetricClouds.CloudPresets.Custom;

            Set(_clouds.state, _cloudsEnabled);
            Set(_clouds.localClouds, _localClouds);
            Set(_clouds.densityMultiplier, _densityMultiplier);
            Set(_clouds.shapeFactor, _shapeFactor);
            Set(_clouds.shapeScale, _shapeScale);
            Set(_clouds.erosionFactor, _erosionFactor);
            Set(_clouds.erosionScale, _erosionScale);
            Set(_clouds.microErosionScale, _microErosionScale);
            Set(_clouds.microErosion, _microErosionEnabled);
            Set(_clouds.microErosionFactor, _microErosionFactor);
            Set(_clouds.bottomAltitude, _bottomAltitude);
            Set(_clouds.altitudeRange, _altitudeRange);
            Set(_clouds.globalSpeed, _globalSpeed);
            Set(_clouds.globalOrientation, _globalOrientation);
            Set(_clouds.shapeSpeedMultiplier, _shapeSpeedMultiplier);
            Set(_clouds.erosionSpeedMultiplier, _erosionSpeedMultiplier);
            Set(_clouds.verticalShapeWindSpeed, _verticalShapeWindSpeed);
            Set(_clouds.verticalErosionWindSpeed, _verticalErosionWindSpeed);
            Set(_clouds.altitudeDistortion, _altitudeDistortion);
            Set(_clouds.ambientLightProbeDimmer, _ambientLightProbeDimmer);
            Set(_clouds.sunLightDimmer, _sunLightDimmer);
            Set(_clouds.multiScattering, _multiScattering);
            Set(_clouds.powderEffectIntensity, _powderEffectIntensity);
            Set(_clouds.erosionOcclusion, _erosionOcclusion);
            Set(_clouds.forwardEccentricity, _forwardEccentricity);
            Set(_clouds.backwardEccentricity, _backwardEccentricity);
            Set(_clouds.numPrimarySteps, _numPrimarySteps);
            Set(_clouds.numLightSteps, _numLightSteps);
            Set(_clouds.maxStepSize, _maxStepSize);
            Set(_clouds.temporalAccumulationFactor, _temporalAccumulationFactor);

#if UNITY_EDITOR
            if (markDirty)
            {
                EditorUtility.SetDirty(_clouds);
                EditorUtility.SetDirty(_cloudsProfile);
            }
#endif
        }

        private void ApplyRendererFeature(bool markDirty)
        {
            if (_rendererFeature == null)
            {
                return;
            }

            _rendererFeature.ResolutionScale = _resolutionScale;
            _rendererFeature.UpscaleMode = _upscaleMode;

#if UNITY_EDITOR
            if (markDirty)
            {
                EditorUtility.SetDirty(_rendererFeature);
            }
#endif
        }

        private void ApplyLight(bool markDirty)
        {
            if (_sunLight == null)
            {
                return;
            }

            _sunLight.intensity = _sunIntensity;
            _sunLight.color = _sunColor;

#if UNITY_EDITOR
            if (markDirty)
            {
                EditorUtility.SetDirty(_sunLight);
            }
#endif
        }

        private static void Set(BoolParameter parameter, bool value)
        {
            parameter.value = value;
            parameter.overrideState = true;
        }

        private static void Set(ClampedFloatParameter parameter, float value)
        {
            parameter.value = value;
            parameter.overrideState = true;
        }

        private static void Set(MinFloatParameter parameter, float value)
        {
            parameter.value = value;
            parameter.overrideState = true;
        }

        private static void Set(FloatParameter parameter, float value)
        {
            parameter.value = value;
            parameter.overrideState = true;
        }

        private static void Set(ClampedIntParameter parameter, int value)
        {
            parameter.value = value;
            parameter.overrideState = true;
        }
    }
}
