#nullable enable
using System;
using System.IO;
using TinCan.Core.Domain.Features;
using TinCan.Features.Atmosphere;
using TinCan.Features.DayCycle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Build Atmosphere: the golden-afternoon look (<see cref="LookProfile"/>) with its colours
    /// from the palette (<see cref="LookPalette"/>; numbers are set when the asset is made and then left as tuned), its
    /// post volume (the wide, soft bloom that gives glows a halo), its day (the light by the sun's height,
    /// <see cref="DayKeys"/>, made once) and the day's config (<see cref="CloudLabDay.ConfigPath"/>, plan
    /// .docs/plans/day-cycle.md), the Atmosphere installer, and that installer in the
    /// play scenes' profiles (Profile_Galleon, Profile_FuelSandbox), and the one test area with the look (Profile_Test_Look:
    /// Test_Core plus Atmosphere, for the LookApplies scenario). The other test areas stay plain. Plan:
    /// .docs/plans/look-slice.md (L2).
    /// </summary>
    public static class AtmosphereAssetBuilder
    {
        public const string LookPath = "Assets/Settings/Look/Look_GoldenAfternoon.asset";
        public const string PostPath = "Assets/Settings/Look/Look_GoldenAfternoon_Post.asset";
        public const string InstallerPath = "Assets/Resources/Installers/AtmosphereFeatureInstaller.asset";
        public const string TestProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Look.asset";
        private const string TestCoreProfilePath = "Assets/Settings/FeatureProfiles/Test/Profile_Test_Core.asset";
        public static readonly string[] PlayProfiles =
        {
            "Assets/Settings/FeatureProfiles/Profile_Galleon.asset",
            "Assets/Settings/FeatureProfiles/Profile_FuelSandbox.asset",
        };

        [MenuItem("TinCan/Dev/Look/Build Atmosphere")]
        public static void Build()
        {
            var palette = LookPalette.Load();
            var post = Post();
            var look = Asset<LookProfile>(LookPath, profile =>
            {
                var settings = profile.Settings;
                settings.SunColour = Color.Lerp(Color.white, palette["leaf-gold"], 0.3f);
                settings.AmbientSky = palette["sky"] * 0.85f;
                settings.AmbientHorizon = palette["horizon-haze"] * 0.85f;
                settings.AmbientGround = palette["cloud-shadow"] * 0.85f;
                settings.SunwardFog = Color.Lerp(palette["horizon-haze"], palette["leaf-gold"], 0.35f);
                settings.AwayFog = Color.Lerp(palette["mist"], palette["cloud-shadow"], 0.5f);
                settings.WhiteoutColour = palette["cloud"];
                profile.Settings = settings;
                profile.Post = post;
                // Made once, then left as tuned (empty the list in the Inspector to rebuild it).
                if (profile.Day.Length == 0) profile.Day = DayKeys(palette, settings);
            });
            Asset<DayCycleConfig>(CloudLabDay.ConfigPath, _ => { });

            var installer = Asset<AtmosphereFeatureInstaller>(InstallerPath, i => SetField(i, "_look", look));
            foreach (var path in PlayProfiles) AddUnique(AssetDatabase.LoadAssetAtPath<FeatureProfile>(path), "_installers", installer);
            Asset<FeatureProfile>(TestProfilePath, profile =>
            {
                AddUnique(profile, "_includes", AssetDatabase.LoadAssetAtPath<FeatureProfile>(TestCoreProfilePath));
                AddUnique(profile, "_installers", installer);
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"[AtmosphereAssetBuilder] {look.name} built; {installer.name} in {string.Join(", ", PlayProfiles)}");
        }

        /// <summary>
        /// The look's day (plan .docs/plans/day-cycle.md), by the sun's height: night (-16), the blue hour (-8), dusk
        /// just after sunset (-3), sunrise and sunset (0), the low gold of a northern winter day (6) and the golden
        /// afternoon the look was made for (20, its own settings). Below the horizon the sun gives no light and the sky
        /// and the air carry it: mulberry and plum toward where the sun went, shadow tint and ink away from it.
        /// </summary>
        private static DayLightKey[] DayKeys(LookPalette palette, LookSettings day)
        {
            Color sky = palette["sky"], haze = palette["horizon-haze"], gold = palette["leaf-gold"], orange = palette["burnt-orange"];
            Color mulberry = palette["mulberry"], plum = palette["plum"], ink = palette["ink"], tint = palette["shadow-tint"];
            Color mist = palette["mist"], cloudShadow = palette["cloud-shadow"], canvas = palette["canvas"];
            static Color Shade(Color colour, float amount) => new(colour.r, colour.g, colour.b, amount);

            return new[]
            {
                new DayLightKey
                {
                    Elevation = -16f, SunColour = orange, SunIntensity = 0f,
                    AmbientSky = Color.Lerp(tint, ink, 0.55f) * 0.25f, AmbientHorizon = Color.Lerp(tint, plum, 0.5f) * 0.2f,
                    AmbientGround = Color.Lerp(plum, ink, 0.5f) * 0.2f,
                    SunwardFog = Color.Lerp(plum, tint, 0.4f) * 0.35f, AwayFog = Color.Lerp(tint, ink, 0.6f) * 0.3f,
                    SkyZenith = Color.Lerp(ink, tint, 0.15f), SkyBelt = Shade(plum, 0f),
                    NightShade = Shade(Color.Lerp(tint, ink, 0.5f) * 0.35f, 0.92f),
                },
                new DayLightKey
                {
                    Elevation = -8f, SunColour = orange, SunIntensity = 0f,
                    AmbientSky = tint * 0.35f, AmbientHorizon = Color.Lerp(tint, mulberry, 0.4f) * 0.32f,
                    AmbientGround = plum * 0.35f,
                    SunwardFog = Color.Lerp(mulberry, tint, 0.45f) * 0.6f, AwayFog = Color.Lerp(tint, plum, 0.45f) * 0.45f,
                    SkyZenith = Color.Lerp(tint, ink, 0.65f), SkyBelt = Shade(mulberry, 0.15f),
                    NightShade = Shade(tint * 0.6f, 0.75f),
                },
                new DayLightKey
                {
                    Elevation = -3f, SunColour = orange, SunIntensity = 0.15f,
                    AmbientSky = Color.Lerp(tint, sky, 0.3f) * 0.5f, AmbientHorizon = Color.Lerp(mulberry, orange, 0.35f) * 0.5f,
                    AmbientGround = Color.Lerp(plum, cloudShadow, 0.4f) * 0.45f,
                    SunwardFog = Color.Lerp(mulberry, orange, 0.55f) * 0.85f, AwayFog = Color.Lerp(tint, mulberry, 0.35f) * 0.6f,
                    SkyZenith = Color.Lerp(tint, sky, 0.25f) * 0.7f, SkyBelt = Shade(Color.Lerp(mulberry, canvas, 0.45f), 0.55f),
                    NightShade = Shade(Color.Lerp(tint, mulberry, 0.3f), 0.45f),
                },
                new DayLightKey
                {
                    Elevation = 0f, SunColour = Color.Lerp(orange, Color.white, 0.2f), SunIntensity = 0.6f,
                    AmbientSky = Color.Lerp(sky, mulberry, 0.3f) * 0.6f, AmbientHorizon = Color.Lerp(haze, orange, 0.35f) * 0.65f,
                    AmbientGround = cloudShadow * 0.55f,
                    SunwardFog = Color.Lerp(orange, gold, 0.4f), AwayFog = Color.Lerp(mist, mulberry, 0.35f),
                    SkyZenith = Color.Lerp(sky, tint, 0.4f), SkyBelt = Shade(Color.Lerp(mulberry, canvas, 0.5f), 0.6f),
                    NightShade = Shade(Color.Lerp(tint, orange, 0.25f), 0.2f),
                },
                new DayLightKey
                {
                    Elevation = 6f, SunColour = Color.Lerp(Color.white, gold, 0.55f), SunIntensity = 0.95f,
                    AmbientSky = sky * 0.75f, AmbientHorizon = Color.Lerp(haze, gold, 0.2f) * 0.8f,
                    AmbientGround = cloudShadow * 0.75f,
                    SunwardFog = Color.Lerp(haze, gold, 0.55f), AwayFog = Color.Lerp(mist, cloudShadow, 0.6f),
                    SkyZenith = Color.Lerp(sky, tint, 0.15f), SkyBelt = Shade(Color.Lerp(mulberry, canvas, 0.6f), 0.2f),
                    NightShade = Shade(Color.white, 0f),
                },
                new DayLightKey
                {
                    Elevation = 20f, SunColour = day.SunColour, SunIntensity = day.SunIntensity,
                    AmbientSky = day.AmbientSky, AmbientHorizon = day.AmbientHorizon, AmbientGround = day.AmbientGround,
                    SunwardFog = day.SunwardFog, AwayFog = day.AwayFog,
                    SkyZenith = sky, SkyBelt = Shade(mulberry, 0f), NightShade = Shade(Color.white, 0f),
                },
            };
        }

        /// <summary>The look's post volume: bloom with a high threshold and a wide scatter, so only glows bloom, softly.</summary>
        private static VolumeProfile Post()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                EnsureDirectory(Path.GetDirectoryName(PostPath));
                AssetDatabase.CreateAsset(profile, PostPath);
            }

            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(overrides: true);
                bloom.name = nameof(Bloom);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                bloom.threshold.value = 1.1f;
                bloom.intensity.value = 0.7f;
                bloom.scatter.value = 0.85f;
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                EnsureDirectory(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddUnique(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            }

            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        internal static void EnsureDirectory(string? folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder!.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            EnsureDirectory(Path.GetDirectoryName(folder));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)!.Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
