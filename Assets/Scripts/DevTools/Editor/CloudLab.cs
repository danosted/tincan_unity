#nullable enable
using System;
using System.IO;
using TinCan.Features.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Build Cloud Lab: writes <see cref="ScenePath"/>, a bare scene for iterating on the clouds in
    /// edit mode (no network, no features, nothing to play). It holds the golden-afternoon look (its sun, ambient and
    /// post, and its air in the shader globals while the scene is open), a camera on the hero view (cruising height,
    /// the sun 45 degrees off ahead, so cloud rims and shadow sides both show), a 30 m ship stand-in and pillars at 250 m,
    /// 1 km and 2 km for scale and depth, the current volumetric clouds (off, to compare against) and "Clouds (new)", the
    /// stylized clouds (<see cref="BuildClouds"/>). Rebuilding overwrites the scene. Plan: .docs/plans/look-slice.md ("The cloud lab").
    /// </summary>
    [InitializeOnLoad]
    public static class CloudLab
    {
        public const string ScenePath = "Assets/Scenes/Lab/Lab_Clouds.unity";
        public const string CurrentCloudsProfilePath = "Assets/Settings/VolumetricCloudsTestProfile.asset";
        public const string CloudShader = "TinCan/Environment/Stylized Cloud";
        public const string SkyShader = "TinCan/Dev/Cloud Lab Sky";
        public const string MeshFolder = "Assets/Art/Clouds/Lab/";
        public const string MaterialFolder = "Assets/Materials/Clouds/";
        public const string BandedCumulus = "M_CloudCumulus";
        public const string BandedSea = "M_CloudSeaSurface";
        public const string SoftCumulus = "M_CloudSoft";
        public const string SoftSea = "M_CloudSeaSoft";
        public const string SeaRoot = "Sea";
        /// <summary>The cumulus styles, each under its own parent in "Clouds (new)": the dome (before lobes,
        /// <see cref="CloudShapes.Dome"/>), the first lobes (<see cref="CloudShapes.CumulusStyle.LobesV1"/>) and the heap
        /// (<see cref="CloudShapes.CumulusStyle.Heap"/>, the active one). A cloud is named by its heap plus the suffix.</summary>
        public static readonly (string Root, string Suffix, string MeshSuffix)[] CumulusStyles =
        {
            (CumulusDomeRoot, " (dome)", "Dome"),
            (CumulusV1Root, " (v1)", "V1"),
            (CumulusRoot, "", ""),
        };
        public const string CumulusRoot = "Cumulus";
        public const string CumulusDomeRoot = "Cumulus dome";
        public const string CumulusV1Root = "Cumulus lobes v1";
        /// <summary>The sea approaches, in the order the cloud sheet shows them.</summary>
        public static readonly string[] Seas = { "Sea A: puffs", "Sea B: billow surface", "Sea C: domed surface", "Sea D: domes + rim puffs" };
        public static readonly string DefaultSea = Seas[2];
        public const float ViewHeight = 70f;
        private const float ShipHeight = 60f;

        static CloudLab()
        {
            EditorSceneManager.sceneOpened += (_, _) => ApplyAir();
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => ApplyAir();
            // Shader globals do not survive every reload; set them again once the Editor settles.
            EditorApplication.delayCall += ApplyAir;
        }

        [MenuItem("TinCan/Dev/Look/Build Cloud Lab")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log($"[Look] cloud lab: {ScenePath}");
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load assets only after NewScene: opening a scene unloads unused assets, so an earlier reference is dead.
            var palette = LookPalette.Load();
            var look = AssetDatabase.LoadAssetAtPath<LookProfile>(AtmosphereAssetBuilder.LookPath)
                ?? throw new InvalidOperationException($"no look at {AtmosphereAssetBuilder.LookPath}: run TinCan > Dev > Look > Build Atmosphere");
            var settings = look.Settings;
            var frame = new AtmosphereProcessor().Evaluate(settings, whiteout: 0f, lightMultiplier: 1f);

            RenderSettings.sun = LookContactSheet.AddSun(scene, palette, look);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = frame.AmbientSky;
            RenderSettings.ambientEquatorColor = frame.AmbientHorizon;
            RenderSettings.ambientGroundColor = frame.AmbientGround;
            // The plain fog the look gives shaders that do not read its banded air, as at the view's height in play.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.Lerp(frame.AwayFog, frame.SunwardFog, 0.35f);
            RenderSettings.fogDensity = frame.UniformDensityAt(ViewHeight, settings.FogBaseHeight);

            if (look.Post != null) GlobalVolume("Look Volume", look.Post, priority: 100);

            // Heading the sunlight travels toward; the camera looks 135 degrees round from it, the sun ahead and to the right.
            float heading = settings.SunAngles.y + 135f;
            var view = Quaternion.Euler(0f, heading, 0f);
            var camera = new GameObject("Lab Camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, ViewHeight, 0f), Quaternion.Euler(4f, heading, 0f));
            // The lab sky (a gradient whose horizon is the air); the sheets switch to the plain colour for "before".
            RenderSettings.skybox = SkyMaterial(palette);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = settings.AwayFog;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 6000f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var reference = new GameObject("Reference").transform;
            var ship = Shape(scene, PrimitiveType.Capsule, view * new Vector3(-8f, ShipHeight - ViewHeight, 45f) + Vector3.up * ViewHeight,
                new Vector3(8f, 15f, 8f), "M_ShipHull", view * Quaternion.Euler(90f, 0f, 0f));
            ship.name = "Ship stand-in (30 m)";
            ship.transform.SetParent(reference, true);
            float[] distances = { 250f, 1000f, 2000f };
            float[] bearings = { 14f, -10f, 4f };
            for (int i = 0; i < distances.Length; i++)
            {
                var foot = view * new Vector3(Mathf.Tan(bearings[i] * Mathf.Deg2Rad) * distances[i], 0f, distances[i]);
                float width = Mathf.Max(6f, distances[i] * 0.02f), top = ShipHeight + distances[i] * 0.05f;
                var pillar = Shape(scene, PrimitiveType.Cylinder,
                    new Vector3(foot.x, (settings.FogBaseHeight + top) * 0.5f, foot.z), new Vector3(width, (top - settings.FogBaseHeight) * 0.5f, width),
                    "M_ShipWalls", Quaternion.identity);
                pillar.name = $"Pillar {distances[i]:0} m";
                pillar.transform.SetParent(reference, true);
            }

            var current = GlobalVolume("Clouds (current, volumetric)",
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(CurrentCloudsProfilePath)
                ?? throw new InvalidOperationException($"no cloud profile at {CurrentCloudsProfilePath}"), priority: 0);
            current.SetActive(false);
            BuildClouds(palette, view);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ApplyAir();
        }

        /// <summary>The stylized clouds (<see cref="CloudShapes"/>, <c>TinCan/Environment/Stylized Cloud</c>): a sea with
        /// holes below cruising height, a cumulus near the ship, one in the middle distance, one toward the sun (for the
        /// silver lining) and a tall one far off. Meshes are rewritten on every build; the materials are made only when
        /// missing, so tuning in their Inspector survives a rebuild.</summary>
        private static void BuildClouds(LookPalette palette, Quaternion view)
        {
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            // The banded materials (the first iteration) stay as the "before" the cloud sheets compare against.
            CloudMaterial(BandedCumulus, palette, _ => { });
            var sea = CloudMaterial("M_CloudSea", palette, material =>
            {
                // Mostly the sea's surface normal, and the puffs' own outlines at the edge: from above it reads as a surface,
                // and at a glancing view it does not fade out as a whole.
                material.SetFloat("_ShapeBlend", 0.75f);
                material.SetFloat("_EdgeShape", 0.2f);
            });
            CloudMaterial(BandedSea, palette, material =>
            {
                // One surface: the lumps' slope for the light, and only a thin fade where it truly turns away (hole rims),
                // so far flat sea seen at a glancing angle stays solid.
                material.SetFloat("_ShapeBlend", 0.6f);
                material.SetFloat("_EdgeShape", 0f);
                material.SetFloat("_EdgeSoftness", 0.04f);
                material.SetFloat("_BottomFade", 0.15f);
            });

            var soft = CloudMaterial(SoftCumulus, palette, material => Soften(material, palette, mottleSize: 30f));
            var softSea = CloudMaterial(SoftSea, palette, material =>
            {
                Soften(material, palette, mottleSize: 60f);
                material.SetFloat("_EdgeShape", 0f);
                material.SetFloat("_EdgeSoftness", 0.04f);
                material.SetFloat("_BottomFade", 0.15f);
            });

            var root = new GameObject("Clouds (new)").transform;
            BuildSeas(root, sea, softSea, soft, view * new Vector3(0f, 0f, 1200f));

            var heaps = new (string Name, int Seed, Vector3 At, float Base, Vector3 Size, int Lobes, int PuffsPerLobe, int Crown,
                int DomePuffs, int DomeCrown)[]
            {
                ("Cumulus near", 11, new Vector3(-260f, 0f, 420f), 35f, new Vector3(110f, 70f, 90f), 2, 6, 12, 26, 14),
                ("Cumulus middle", 23, new Vector3(260f, 0f, 800f), 30f, new Vector3(300f, 180f, 240f), 3, 7, 18, 30, 18),
                ("Cumulus sunward", 51, new Vector3(900f, 0f, 1100f), 30f, new Vector3(400f, 250f, 320f), 3, 7, 18, 32, 18),
                ("Cumulus tower", 37, new Vector3(-500f, 0f, 2200f), 25f, new Vector3(700f, 600f, 600f), 3, 8, 24, 40, 24),
            };
            // Each cumulus style under its own parent; only the default is active (toggle them in the hierarchy, or
            // compare them on the cloud sheets).
            foreach (var style in CumulusStyles)
            {
                var parent = new GameObject(style.Root).transform;
                parent.SetParent(root, false);
                foreach (var heap in heaps)
                {
                    string mesh = heap.Name.Replace(" ", "") + style.MeshSuffix;
                    var shape = style.Root switch
                    {
                        CumulusDomeRoot => CloudShapes.Dome(mesh, heap.Seed, heap.Size, heap.DomePuffs, heap.DomeCrown),
                        CumulusV1Root => CloudShapes.Cumulus(mesh, heap.Seed, heap.Size, heap.Lobes, heap.PuffsPerLobe, heap.Crown,
                            CloudShapes.CumulusStyle.LobesV1),
                        _ => CloudShapes.Cumulus(mesh, heap.Seed, heap.Size, heap.Lobes, heap.PuffsPerLobe, heap.Crown, CloudShapes.CumulusStyle.Heap),
                    };
                    Place(parent, heap.Name + style.Suffix, SaveMesh(shape), soft, view * heap.At + Vector3.up * heap.Base, heap.Seed * 47f);
                }

                parent.gameObject.SetActive(style.Root == CumulusRoot);
            }
        }

        /// <summary>The four sea approaches, under "Clouds (new)/Sea", built from one <see cref="CloudSeaField"/> layout
        /// (same seed, holes and swells), so they compare fairly. Only <see cref="DefaultSea"/> is active; toggle the
        /// others in the hierarchy, or render all of them with <see cref="CloudSheet"/>.</summary>
        private static void BuildSeas(Transform root, Material puffs, Material surface, Material cumulus, Vector3 centre)
        {
            const int seed = 3;
            const float surfaceHeight = 20f, floor = 0f, cell = 80f, holes = 0.32f, step = 10f;
            var area = new Vector2(3000f, 3000f);
            var seas = new GameObject(SeaRoot).transform;
            seas.SetParent(root, false);
            seas.position = centre;

            CloudSeaField Field(CloudSeaField.LumpStyle lumps) => new(seed, surfaceHeight, floor, cell, holes, lumps);
            var domes = Field(CloudSeaField.LumpStyle.Domes);
            var domedMesh = SaveMesh(CloudShapes.SeaSurface("SeaDomes", domes, area, step));

            Place(seas, Seas[0], SaveMesh(CloudShapes.SeaPuffs("SeaPuffs", Field(CloudSeaField.LumpStyle.None), area, seed)), puffs, centre, 0f);
            Place(seas, Seas[1], SaveMesh(CloudShapes.SeaSurface("SeaBillow", Field(CloudSeaField.LumpStyle.Billow), area, step)), surface, centre, 0f);
            Place(seas, Seas[2], domedMesh, surface, centre, 0f);
            var rims = new GameObject(Seas[3]).transform;
            rims.SetParent(seas, false);
            Place(rims, "Surface", domedMesh, surface, centre, 0f);
            Place(rims, "Rim puffs", SaveMesh(CloudShapes.RimPuffs("SeaRimPuffs", domes, area, seed)), cumulus, centre, 0f);

            foreach (Transform sea in seas) sea.gameObject.SetActive(sea.name == DefaultSea);
        }

        private static void Place(Transform root, string name, Mesh mesh, Material material, Vector3 position, float yaw)
        {
            var cloud = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            cloud.transform.SetParent(root, false);
            cloud.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            cloud.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = cloud.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>The soft look (after the Tears of the Kingdom sample, plan "The cloud lab"): the bands melted into one
        /// gradient, shadows a warm grey (between the palette's cloud shadow and canvas) that the sky tints, soft edges;
        /// creases between lobes and faint mottling are there but off until chosen.</summary>
        private static void Soften(Material material, LookPalette palette, float mottleSize)
        {
            var shadow = Color.Lerp(palette["cloud-shadow"], palette["canvas"], 0.55f);
            material.SetColor("_ShadowColour", shadow);
            material.SetColor("_BaseColour", Color.Lerp(palette["cloud-shadow"], palette["canvas"], 0.3f));
            material.SetColor("_CreaseColour", shadow * 0.85f);
            material.SetFloat("_BandSoftness", 0.9f);
            material.SetFloat("_Wrap", 0.45f);
            material.SetFloat("_ShapeBlend", 0.5f);
            material.SetFloat("_SunTint", 0.5f);
            material.SetFloat("_Ambient", 0.35f);
            material.SetFloat("_Rim", 0.25f);
            material.SetFloat("_EdgeSoftness", 0.5f);
            // Creases and mottling stay off: the developer chose the soft light alone (2026-11-01); the cloud sheets show
            // them on a variant.
            material.SetFloat("_Crease", 0f);
            material.SetFloat("_Mottle", 0f);
            material.SetFloat("_MottleScale", mottleSize);
        }

        /// <summary>The lab sky (<c>TinCan/Dev/Cloud Lab Sky</c>), made only when missing.</summary>
        private static Material SkyMaterial(LookPalette palette)
        {
            var path = MaterialFolder + "M_CloudLabSky.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);
            var material = new Material(Shader.Find(SkyShader) ?? throw new InvalidOperationException($"no shader {SkyShader}")) { name = "M_CloudLabSky" };
            material.SetColor("_Zenith", palette["sky"]);
            material.SetColor("_Horizon", palette["horizon-haze"]);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CloudMaterial(string name, LookPalette palette, Action<Material> tune)
        {
            var path = MaterialFolder + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var material = new Material(Shader.Find(CloudShader) ?? throw new InvalidOperationException($"no shader {CloudShader}")) { name = name };
            material.SetColor("_LitColour", palette["cloud"]);
            material.SetColor("_ShadowColour", palette["cloud-shadow"]);
            material.SetColor("_BaseColour", Color.Lerp(palette["cloud-shadow"], palette[LookMaterials.ShadowTintRole], 0.5f));
            tune(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Writes a mesh as a new asset, replacing any old one. Not copied into the old asset: a mesh copied with
        /// EditorUtility.CopySerialized kept drawing its old shape, so renders showed stale clouds. The scene is rebuilt
        /// with every build, so the new asset ids do not matter.</summary>
        private static Mesh SaveMesh(Mesh mesh)
        {
            var path = MeshFolder + mesh.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void EnsureFolder(string folder)
        {
            var path = folder.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent + "/");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>The look's banded air in the shader globals while the lab is the active scene; off otherwise, so other
        /// scenes in edit mode keep the plain fog they have.</summary>
        private static void ApplyAir()
        {
            // In play the Atmosphere feature owns the globals.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                AtmosphereShaderGlobals.SetEnabled(false);
                return;
            }

            var look = AssetDatabase.LoadAssetAtPath<LookProfile>(AtmosphereAssetBuilder.LookPath);
            if (look == null) return;
            var settings = look.Settings;
            AtmosphereShaderGlobals.Apply(new AtmosphereProcessor().Evaluate(settings, whiteout: 0f, lightMultiplier: 1f), settings);
            AtmosphereShaderGlobals.SetEnabled(true);
        }

        private static GameObject GlobalVolume(string name, VolumeProfile profile, float priority)
        {
            var holder = new GameObject(name);
            var volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
            return holder;
        }

        private static MeshRenderer Shape(Scene scene, PrimitiveType type, Vector3 position, Vector3 scale, string material, Quaternion rotation) =>
            LookLabSheet.Shape(scene, type, position, scale, LookLabSheet.Library(material), rotation);
    }
}
