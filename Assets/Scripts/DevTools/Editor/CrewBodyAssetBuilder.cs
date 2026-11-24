#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Humanoid;
using TinCan.Features.CrewBody;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Crew Body > Build Assets: everything the crew body needs from the Blender model
    /// (.tools/blender/crew_body.py): the model imported as a humanoid with its looping clips and library materials, the
    /// Animator controller, the body prefab, the feature installer, and the installer listed in the game's profile.
    /// Idempotent: rebuilds from the model each time. Plan: .docs/plans/crew-animation.md (S2).
    /// </summary>
    public static class CrewBodyAssetBuilder
    {
        public const string ModelPath = "Assets/Models/Characters/CrewBody/crew_body.fbx";
        public const string ControllerPath = "Assets/Animations/CrewBody/CrewBody.controller";
        public const string PrefabPath = "Assets/Prefabs/CrewBody/CrewBody.prefab";
        public const string InstallerPath = "Assets/Resources/Installers/CrewBodyFeatureInstaller.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string GameProfilePath = "Assets/Settings/FeatureProfiles/Profile_Galleon.asset";

        /// <summary>The ground speed the Fall and Jump clips sit at on the Air blend (VerticalSpeed, m/s).</summary>
        private const float FallSpeed = -6f, RiseSpeed = 3f;

        // The Blender material names (the model's contract) and the library material each becomes.
        private static readonly Dictionary<string, string> Materials = new()
        {
            ["CrewBody_Tunic"] = "M_ShipCanvas",
            ["CrewBody_Trousers"] = "M_ShipIron",
            ["CrewBody_Skin"] = "M_ShipDeck",
            ["CrewBody_Boots"] = "M_CampLeather",
            ["CrewBody_Scarf"] = "M_ShipPennant",
            ["CrewBody_Hair"] = "M_ShipWalls",
        };

        [MenuItem("TinCan/Dev/Crew Body/Build Assets")]
        public static void Build()
        {
            var (run, sprint) = PlayerSpeeds();
            ImportModel();
            var controller = BuildController(run, sprint);
            var prefab = BuildPrefab(controller);
            var installer = BuildInstaller(prefab);
            ListInProfile(installer, GameProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CrewBody] built {ControllerPath}, {PrefabPath}, {InstallerPath} (run {run} m/s, sprint {sprint} m/s)");
        }

        private static (float Run, float Sprint) PlayerSpeeds()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) ?? throw new InvalidOperationException($"missing {PlayerPrefabPath}");
            var movement = player.GetComponent<HumanoidControllerView>() ?? throw new InvalidOperationException("the player has no HumanoidControllerView");
            return (movement.WalkSpeed, movement.WalkSpeed * movement.SprintMultiplier);
        }

        /// <summary>
        /// Imports the model twice: as generic to read its skeleton, then as a humanoid mapped by name (the Blender bones
        /// carry HumanTrait's names), with the clips looping in place.
        /// </summary>
        private static void ImportModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter
                           ?? throw new InvalidOperationException($"Missing model {ModelPath}: run .tools/blender/crew_body.py.");
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importAnimation = true;
            foreach (var (source, library) in Materials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), LookMaterials.Get(library));
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var names = new HashSet<string>(transforms.Select(t => t.name));
            var human = HumanTrait.BoneName.Where(names.Contains).Select(bone => new HumanBone
            {
                humanName = bone,
                boneName = bone,
                limit = new HumanLimit { useDefaultValues = true },
            }).ToArray();
            importer.humanDescription = new HumanDescription
            {
                human = human,
                skeleton = transforms.Select(t => new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale,
                }).ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.clipAnimations = importer.defaultClipAnimations.Select(Looping).ToArray();
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                throw new InvalidOperationException($"{ModelPath}: the humanoid avatar did not build (mapped {human.Length} bones); see the importer's Rig tab.");
            }
        }

        private static ModelImporterClipAnimation Looping(ModelImporterClipAnimation clip)
        {
            // Blender names takes "<armature>|<action>"; the clip is the action.
            clip.name = clip.takeName.Contains('|') ? clip.takeName[(clip.takeName.LastIndexOf('|') + 1)..] : clip.takeName;
            clip.loopTime = true;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            return clip;
        }

        private static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == name)
            ?? throw new InvalidOperationException($"{ModelPath} has no clip {name}");

        /// <summary>
        /// Move: a blend on Speed (the run reversed when backing, idle, run at the walk speed, sprint at the sprint speed);
        /// Air: a blend on VerticalSpeed (fall, jump). Grounded switches between them.
        /// </summary>
        private static AnimatorController BuildController(float run, float sprint)
        {
            EnsureFolder(Path.GetDirectoryName(ControllerPath)!);
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });

            var move = controller.CreateBlendTreeInController("Move", out var moveTree);
            moveTree.blendType = BlendTreeType.Simple1D;
            moveTree.blendParameter = "Speed";
            moveTree.useAutomaticThresholds = false;
            moveTree.children = new[]
            {
                new ChildMotion { motion = Clip("Run"), threshold = -run, timeScale = -1f },
                new ChildMotion { motion = Clip("Idle"), threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = Clip("Run"), threshold = run, timeScale = 1f },
                new ChildMotion { motion = Clip("Sprint"), threshold = sprint, timeScale = 1f },
            };

            var air = controller.CreateBlendTreeInController("Air", out var airTree);
            airTree.blendType = BlendTreeType.Simple1D;
            airTree.blendParameter = "VerticalSpeed";
            airTree.useAutomaticThresholds = false;
            airTree.children = new[]
            {
                new ChildMotion { motion = Clip("Fall"), threshold = FallSpeed, timeScale = 1f },
                new ChildMotion { motion = Clip("Jump"), threshold = RiseSpeed, timeScale = 1f },
            };

            var machine = controller.layers[0].stateMachine;
            machine.defaultState = move;
            var leave = move.AddTransition(air);
            leave.hasExitTime = false;
            leave.duration = 0.1f;
            leave.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            var land = air.AddTransition(move);
            land.hasExitTime = false;
            land.duration = 0.08f;
            land.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static CrewBodyView BuildPrefab(AnimatorController controller)
        {
            EnsureFolder(Path.GetDirectoryName(PrefabPath)!);
            var root = new GameObject("CrewBody");
            try
            {
                root.AddComponent<CrewBodyView>();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
                model.name = "Model";
                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().First();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<CrewBodyView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CrewBodyFeatureInstaller BuildInstaller(CrewBodyView prefab)
        {
            var installer = AssetDatabase.LoadAssetAtPath<CrewBodyFeatureInstaller>(InstallerPath);
            if (installer == null)
            {
                installer = ScriptableObject.CreateInstance<CrewBodyFeatureInstaller>();
                AssetDatabase.CreateAsset(installer, InstallerPath);
            }
            var serialized = new SerializedObject(installer);
            serialized.FindProperty("_body").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(installer);
            return installer;
        }

        private static void ListInProfile(FeatureInstaller installer, string profilePath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<FeatureProfile>(profilePath) ?? throw new InvalidOperationException($"missing {profilePath}");
            if (profile.Installers.Contains(installer)) return;
            var serialized = new SerializedObject(profile);
            var list = serialized.FindProperty("_installers");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            EnsureFolder(Path.GetDirectoryName(folder)!);
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)!.Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
