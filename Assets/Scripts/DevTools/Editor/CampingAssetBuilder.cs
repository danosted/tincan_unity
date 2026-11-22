#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.Camping;
using TinCan.Core.Domain.Abilities.Inputs;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Camping > Build Assets: every asset of camping, so its data is reviewable as code: the carrying tags
    /// and effects, the items (firewood, the campfire bundle, the tent roll) and their held visuals, the interactions, the
    /// config (with Primary as the input that ties and places), the placed campfire and tent and their previews (in the
    /// grappling hook's aim colours: build its assets first), the hearth fitting (the hearth and a basket of tent rolls,
    /// fitting camp.hearth), the installer and its place in Profile_Galleon (the live game). Scriptable objects are
    /// re-applied on every run; prefabs are built only when missing (delete one to rebuild it), so network ids stay
    /// stable. The models are placeholders made of primitives in the look's library materials; please review them in the
    /// Inspector. Plans: .docs/plans/peaceful-mode.md, .docs/plans/island-life.md.
    /// </summary>
    public static class CampingAssetBuilder
    {
        private const string Tags = "Assets/Abilities/Tags/";
        private const string Effects = "Assets/Abilities/Effects/";
        private const string Items = "Assets/Items/";
        private const string Interactions = "Assets/Interactions/";
        private const string Prefabs = "Assets/Prefabs/Camping/";
        private const string HeldVisuals = "Assets/Prefabs/Items/";
        private const string CampfirePath = Prefabs + "Campfire.prefab";
        private const string TentPath = Prefabs + "Tent.prefab";
        private const string HearthPath = Prefabs + "Hearth.prefab";
        private const string HearthFittingPath = "Assets/Settings/ShipFittings/FITTING_Hearth.asset";
        private const string ConfigPath = "Assets/Settings/Camping/CampingConfig.asset";
        private const string InstallerPath = "Assets/Resources/Installers/CampingFeatureInstaller.asset";
        private const string PrimaryInputPath = "Assets/Abilities/Inputs/Input_Primary.asset";
        // The grappling hook's aim marker colours: green, it goes there; red, it does not.
        private const string FitsMaterialPath = "Assets/Materials/GrappleHook/M_GrappleAim_InRange.mat";
        private const string NoFitMaterialPath = "Assets/Materials/GrappleHook/M_GrappleAim_OutOfRange.mat";

        public const string HearthFittingId = "camp.hearth";

        /// <summary>The names of the placed campfire's and tent's interaction targets, for scenarios.</summary>
        public const string CampfireTargetName = "Campfire";
        public const string TentTargetName = "TentSpot";

        // Stable network ids: 1-4 are the jerry can, the net, the repair tool and the grappling hook.
        private const int FirewoodItemId = 5;
        private const int TentItemId = 6;
        private const int BundleItemId = 7;

        // Under the player's Visual (origin at the capsule centre, eyes about 0.7 m up): low in front, in both arms.
        private static readonly Vector3 HeldLocalPosition = new(0f, -0.15f, 0.5f);

        [MenuItem("TinCan/Dev/Camping/Build Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CampingAssetBuilder] Stop Play mode before building the camping assets.");
                return;
            }

            var materials = new Materials();
            var firewood = Item(Items + "ITEM_Firewood.asset", FirewoodItemId, "Firewood", "Firewood",
                HeldVisual("Carry_Firewood", root => FirewoodModel(root, materials)));
            var tent = Item(Items + "ITEM_TentRoll.asset", TentItemId, "Tent roll", "TentRoll",
                HeldVisual("Carry_TentRoll", root => TentRollModel(root, materials)));

            var gather = Interaction("IA_GatherFirewood", typeof(GatherFirewoodInteractionHandler));
            var stoke = Interaction("IA_StokeFire", typeof(StokeFireInteractionHandler));
            var pitch = Interaction("IA_PitchTent", typeof(PitchTentInteractionHandler));
            var takeTent = Interaction("IA_TakeTentRoll", typeof(TakeItemInteractionHandler));

            var bundle = Item(Items + "ITEM_CampfireBundle.asset", BundleItemId, "Campfire bundle", "CampfireBundle",
                HeldVisual("Carry_CampfireBundle", root => BundleModel(root, materials)));
            // The carrying tags resolve by name through the database: a tag missing from it is never held.
            Load<GameplayTagDatabase>("Assets/Abilities/GameplayTagDatabase.asset").Refresh();

            var config = Asset<CampingConfig>(ConfigPath, c =>
            {
                c.FirewoodItem = firewood;
                c.BundleItem = bundle;
                c.TentItem = tent;
                c.PlaceInput = Load<GameplayInput>(PrimaryInputPath);
                c.CarryingFirewoodTag = Load<GameplayTag>(Tags + "State.Carrying.Firewood.asset");
                c.CarryingBundleTag = Load<GameplayTag>(Tags + "State.Carrying.CampfireBundle.asset");
                c.CarryingTentTag = Load<GameplayTag>(Tags + "State.Carrying.TentRoll.asset");
                c.FitsMaterial = Load<Material>(FitsMaterialPath);
                c.NoFitMaterial = Load<Material>(NoFitMaterialPath);
            });

            var campfire = CampfirePrefab(config, stoke, materials);
            var placedTent = TentPrefab(config, pitch, materials);
            var campfirePreview = Preview("Preview_Campfire", root => FireRing(root, materials));
            var tentPreview = Preview("Preview_Tent", root => TentModel(root, materials));
            Asset<CampingConfig>(ConfigPath, c =>
            {
                c.CampfirePrefab = campfire;
                c.TentPrefab = placedTent;
                c.CampfirePreview = campfirePreview;
                c.TentPreview = tentPreview;
            });

            var hearth = HearthPrefab(config, stoke, takeTent, tent, materials);
            var hearthFitting = Asset<ShipFittingDefinition>(HearthFittingPath, fitting =>
            {
                var fields = new SerializedObject(fitting);
                fields.FindProperty("_fittingId").stringValue = HearthFittingId;
                fields.FindProperty("_displayName").stringValue = "Hearth";
                fields.FindProperty("_prefab").objectReferenceValue = hearth;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });

            var installer = Asset<CampingFeatureInstaller>(InstallerPath, i =>
            {
                SetField(i, "_config", config);
                SetList(i, "_items", firewood, bundle, tent);
                SetList(i, "_fittings", hearthFitting);
            });

            AddUnique(Load<FeatureProfile>(ShipSocketsAssetBuilder.GalleonProfilePath), "_installers", installer);

            AssetDatabase.SaveAssets();
            Debug.Log("[CampingAssetBuilder] Camping assets are up to date.");
        }

        private sealed class Materials
        {
            public readonly Material Stone = LookMaterials.Get("M_ShipCairn");
            public readonly Material Wood = LookMaterials.Get("M_ShipHull");
            public readonly Material DarkWood = LookMaterials.Get("M_ShipWalls");
            public readonly Material Canvas = LookMaterials.Get("M_ShipCanvas");
            public readonly Material Iron = LookMaterials.Get("M_ShipIron");
            public readonly Material Flame = LookMaterials.Get("M_CampFlame");
            public readonly Material Ember = LookMaterials.Get("M_CampEmber");
            public readonly Material Leather = LookMaterials.Get("M_CampLeather");
            public readonly Color FireLight = LookPalette.Load()["lantern-amber"];
        }

        private static ItemDefinition Item(string path, int id, string displayName, string tagName, GameObject heldVisual)
        {
            var carrying = Asset<GameplayTag>(Tags + $"State.Carrying.{tagName}.asset", _ => { });
            var holding = Asset<GameplayEffectDefinition>(Effects + $"GE_Holding_{tagName}.asset", effect =>
            {
                effect.DurationType = DurationType.Infinite;
                effect.DurationSeconds = 0f;
                effect.Modifiers = new List<AttributeModifier>();
                effect.GrantedTags = new List<GameplayTag> { carrying };
                effect.Cues ??= new List<GameplayTag>();
            });

            return Asset<ItemDefinition>(path, definition =>
            {
                var fields = new SerializedObject(definition);
                fields.FindProperty("_id").intValue = id;
                fields.FindProperty("_displayName").stringValue = displayName;
                fields.FindProperty("_heldVisual").objectReferenceValue = heldVisual;
                fields.FindProperty("_equippedEffect").objectReferenceValue = holding;
                fields.FindProperty("_grantedAbilities").arraySize = 0;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static InteractionDefinition Interaction(string name, Type handler) =>
            Asset<InteractionDefinition>(Interactions + name + ".asset", definition =>
                SetField(definition, "_handlerTypeName", handler.AssemblyQualifiedName!));

        private static GameObject HeldVisual(string name, Action<Transform> model)
        {
            string path = HeldVisuals + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(name);
            root.transform.localPosition = HeldLocalPosition;
            model(root.transform);
            EnsureFolder(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Three short logs held across the arms, tied with a leather strap.</summary>
        private static void FirewoodModel(Transform parent, Materials m)
        {
            Visual(PrimitiveType.Cylinder, "Log_A", parent, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.1f, 0.28f, 0.1f), m.Wood);
            Visual(PrimitiveType.Cylinder, "Log_B", parent, new Vector3(0.02f, 0f, 0.1f), new Vector3(0f, 8f, 90f), new Vector3(0.09f, 0.26f, 0.09f), m.DarkWood);
            Visual(PrimitiveType.Cylinder, "Log_C", parent, new Vector3(-0.01f, 0.08f, 0.05f), new Vector3(0f, -6f, 90f), new Vector3(0.09f, 0.27f, 0.09f), m.Wood);
            Visual(PrimitiveType.Cube, "Strap", parent, new Vector3(0f, 0.03f, 0.05f), Vector3.zero, new Vector3(0.03f, 0.22f, 0.24f), m.Leather);
        }

        /// <summary>A rolled canvas tent with two leather straps.</summary>
        private static void TentRollModel(Transform parent, Materials m)
        {
            Visual(PrimitiveType.Cylinder, "Roll", parent, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.26f, 0.32f, 0.26f), m.Canvas);
            Visual(PrimitiveType.Cylinder, "Strap_L", parent, new Vector3(-0.18f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.275f, 0.02f, 0.275f), m.Leather);
            Visual(PrimitiveType.Cylinder, "Strap_R", parent, new Vector3(0.18f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.275f, 0.02f, 0.275f), m.Leather);
        }

        /// <summary>A log and a handful of kindling, bound in a leather strap: a campfire to set down.</summary>
        private static void BundleModel(Transform parent, Materials m)
        {
            Visual(PrimitiveType.Cylinder, "Log", parent, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.11f, 0.26f, 0.11f), m.DarkWood);
            for (int i = 0; i < 4; i++)
            {
                Visual(PrimitiveType.Cylinder, $"Kindling_{i}", parent, new Vector3(-0.08f + i * 0.05f, 0.08f, 0.04f * (i % 2)),
                    new Vector3(0f, 10f * i - 15f, 90f), new Vector3(0.035f, 0.22f, 0.035f), m.Wood);
            }
            Visual(PrimitiveType.Cube, "Strap", parent, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.03f, 0.22f, 0.2f), m.Leather);
        }

        /// <summary>
        /// The campfire a bundle becomes, its root on the ground: a ring of stones round kindling, unlit until a log is fed
        /// to it. Its front (+Z) faces the player who set it down.
        /// </summary>
        private static GameObject CampfirePrefab(CampingConfig config, InteractionDefinition stoke, Materials m)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CampfirePath);
            if (existing != null) return existing;

            var root = NetworkedRoot("Campfire");
            var fire = Part(CampfireTargetName, root.transform, Vector3.zero, 0f);
            FireRing(fire, m);
            Fire(fire, config, stoke, m, flameBase: 0.12f, lightHeight: 0.8f);
            InteractVolume(fire, new Vector3(0f, 0.8f, 0f), new Vector3(1.1f, 1.6f, 1.1f));
            return SaveNetworkPrefab(root, CampfirePath);
        }

        /// <summary>The tent a roll becomes, pitched, its door (+Z) toward the player who set it down; packed, it is gone.</summary>
        private static GameObject TentPrefab(CampingConfig config, InteractionDefinition pitch, Materials m)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TentPath);
            if (existing != null) return existing;

            var root = NetworkedRoot("Tent");
            var spot = Part(TentTargetName, root.transform, Vector3.zero, 0f);
            var tentSpot = spot.gameObject.AddComponent<TentSpotNetworkMediator>();
            SetField(tentSpot, "_interactionDefinition", pitch);
            SetField(tentSpot, "_config", config);
            var placed = new SerializedObject(tentSpot);
            placed.FindProperty("_placed").boolValue = true;
            placed.ApplyModifiedPropertiesWithoutUndo();
            TentModel(spot, m);
            Object.DestroyImmediate(spot.Find("TentMarker").gameObject);
            InteractVolume(spot, new Vector3(0f, 0.8f, 0f), new Vector3(1.6f, 1.6f, 1.8f));
            return SaveNetworkPrefab(root, TentPath);
        }

        /// <summary>A ghost of what is set down (its renderers only; the preview recolours them), its root on the ground.</summary>
        private static GameObject Preview(string name, Action<Transform> model)
        {
            string path = Prefabs + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(name);
            model(root.transform);
            var marker = root.transform.Find("TentMarker");
            if (marker != null) Object.DestroyImmediate(marker.gameObject);
            EnsureFolder(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>The pitched tent (an A-frame of canvas on poles over a groundsheet) and the pegs that mark a free spot.</summary>
        private static void TentModel(Transform spot, Materials m)
        {
            var tent = new GameObject("Tent").transform;
            tent.SetParent(spot, false);
            Visual(PrimitiveType.Cube, "Groundsheet", tent, new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(1.8f, 0.02f, 2.2f), m.Leather);
            Visual(PrimitiveType.Cube, "Panel_L", tent, new Vector3(-0.43f, 0.61f, 0f), new Vector3(0f, 0f, -35f), new Vector3(0.04f, 1.5f, 2f), m.Canvas);
            Visual(PrimitiveType.Cube, "Panel_R", tent, new Vector3(0.43f, 0.61f, 0f), new Vector3(0f, 0f, 35f), new Vector3(0.04f, 1.5f, 2f), m.Canvas);
            Visual(PrimitiveType.Cylinder, "Ridge", tent, new Vector3(0f, 1.24f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 1.1f, 0.05f), m.Wood);
            Visual(PrimitiveType.Cylinder, "Pole_Front", tent, new Vector3(0f, 0.62f, 1.05f), new Vector3(0f, 0f, 2f), new Vector3(0.05f, 0.62f, 0.05f), m.Wood);
            Visual(PrimitiveType.Cylinder, "Pole_Back", tent, new Vector3(0f, 0.62f, -1.05f), new Vector3(0f, 0f, -2f), new Vector3(0.05f, 0.62f, 0.05f), m.Wood);

            var marker = new GameObject("TentMarker").transform;
            marker.SetParent(spot, false);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.8f : 0.8f;
                float z = i < 2 ? -1f : 1f;
                Visual(PrimitiveType.Cube, $"Peg_{i}", marker, new Vector3(x, 0.1f, z), new Vector3(8f, 0f, 0f), new Vector3(0.04f, 0.2f, 0.04f), m.Wood);
            }
        }

        /// <summary>A ring of eight uneven stones and crossed kindling, the fire's bed.</summary>
        private static void FireRing(Transform fire, Materials m)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f + (i % 3) * 6f;
                var position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.07f, 0.55f);
                float size = 0.24f + (i % 3) * 0.03f;
                Visual(PrimitiveType.Sphere, $"Stone_{i}", fire, position, new Vector3(0f, angle, 0f), new Vector3(size, 0.17f, size * 0.85f), m.Stone);
            }

            for (int i = 0; i < 3; i++)
            {
                Visual(PrimitiveType.Cylinder, $"Kindling_{i}", fire, new Vector3(0f, 0.09f, 0f), new Vector3(0f, i * 60f, 90f), new Vector3(0.08f, 0.3f, 0.08f), m.DarkWood);
            }
        }

        /// <summary>The fire itself on <paramref name="fire"/>: the mediator, its view, embers, three flames and a warm light.</summary>
        private static void Fire(Transform fire, CampingConfig config, InteractionDefinition stoke, Materials m, float flameBase, float lightHeight)
        {
            var mediator = fire.gameObject.AddComponent<FireNetworkMediator>();
            SetField(mediator, "_interactionDefinition", stoke);
            SetField(mediator, "_config", config);
            fire.gameObject.AddComponent<FireView>();

            Visual(PrimitiveType.Sphere, "Embers", fire, new Vector3(0f, flameBase - 0.06f, 0f), Vector3.zero, new Vector3(0.5f, 0.07f, 0.5f), m.Ember);
            Visual(PrimitiveType.Sphere, "Flame_0", fire, new Vector3(0f, flameBase + 0.25f, 0f), Vector3.zero, new Vector3(0.3f, 0.55f, 0.3f), m.Flame);
            Visual(PrimitiveType.Sphere, "Flame_1", fire, new Vector3(0.11f, flameBase + 0.17f, 0.07f), Vector3.zero, new Vector3(0.2f, 0.38f, 0.2f), m.Flame);
            Visual(PrimitiveType.Sphere, "Flame_2", fire, new Vector3(-0.1f, flameBase + 0.15f, -0.08f), Vector3.zero, new Vector3(0.18f, 0.34f, 0.18f), m.Flame);

            var light = new GameObject("FireLight").AddComponent<Light>();
            light.transform.SetParent(fire, false);
            light.transform.localPosition = new Vector3(0f, lightHeight, 0f);
            light.type = LightType.Point;
            light.color = m.FireLight;
            light.range = 9f;
            light.shadows = LightShadows.None;
            light.enabled = false;
        }

        /// <summary>
        /// The hearth fitting (pivot at its base, facing +Z): a stone firebox with a timber mantel and an iron hood and flue,
        /// and a basket of tent rolls beside it (a rack: take a roll, or put one back).
        /// </summary>
        private static GameObject HearthPrefab(CampingConfig config, InteractionDefinition stoke, InteractionDefinition takeTent,
            ItemDefinition tent, Materials m)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HearthPath);
            if (existing != null) return existing;

            var root = NetworkedRoot("Hearth");

            var hearth = Part("Hearth", root.transform, Vector3.zero, 0f);
            Visual(PrimitiveType.Cube, "Base", hearth, new Vector3(0f, 0.175f, 0f), Vector3.zero, new Vector3(1.3f, 0.35f, 1f), m.Stone);
            Visual(PrimitiveType.Cube, "BackWall", hearth, new Vector3(0f, 0.9f, -0.38f), Vector3.zero, new Vector3(1.3f, 1.1f, 0.25f), m.Stone);
            Visual(PrimitiveType.Cube, "Side_L", hearth, new Vector3(-0.55f, 0.7f, -0.05f), new Vector3(0f, 2f, 0f), new Vector3(0.2f, 0.7f, 0.75f), m.Stone);
            Visual(PrimitiveType.Cube, "Side_R", hearth, new Vector3(0.55f, 0.7f, -0.05f), new Vector3(0f, -1.5f, 0f), new Vector3(0.2f, 0.7f, 0.75f), m.Stone);
            Visual(PrimitiveType.Cube, "Mantel", hearth, new Vector3(0f, 1.1f, -0.02f), new Vector3(0f, 0f, 0.8f), new Vector3(1.5f, 0.12f, 0.9f), m.Wood);
            Visual(PrimitiveType.Cube, "Hood", hearth, new Vector3(0f, 1.42f, -0.2f), Vector3.zero, new Vector3(0.9f, 0.5f, 0.5f), m.Iron);
            Visual(PrimitiveType.Cylinder, "Flue", hearth, new Vector3(0f, 1.9f, -0.22f), Vector3.zero, new Vector3(0.3f, 0.5f, 0.3f), m.Iron);
            for (int i = 0; i < 2; i++)
            {
                Visual(PrimitiveType.Cylinder, $"Kindling_{i}", hearth, new Vector3(0f, 0.42f, 0f), new Vector3(0f, i * 70f + 10f, 90f), new Vector3(0.08f, 0.3f, 0.08f), m.DarkWood);
            }
            Fire(hearth, config, stoke, m, flameBase: 0.45f, lightHeight: 0.9f);
            InteractVolume(hearth, new Vector3(0f, 0.9f, 0.3f), new Vector3(1.2f, 1.4f, 1.2f));

            var basket = Part("TentBasket", root.transform, new Vector3(1.15f, 0f, 0.15f), 0f);
            var rack = basket.gameObject.AddComponent<ItemRackNetworkMediator>();
            SetField(rack, "_interactionDefinition", takeTent);
            SetField(rack, "_item", tent);
            Visual(PrimitiveType.Cylinder, "Basket", basket, new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.55f, 0.22f, 0.55f), m.Wood);
            Visual(PrimitiveType.Cylinder, "Roll_0", basket, new Vector3(0f, 0.52f, -0.1f), new Vector3(0f, 0f, 90f), new Vector3(0.2f, 0.25f, 0.2f), m.Canvas);
            Visual(PrimitiveType.Cylinder, "Roll_1", basket, new Vector3(0.02f, 0.55f, 0.12f), new Vector3(0f, 12f, 90f), new Vector3(0.2f, 0.25f, 0.2f), m.Canvas);
            InteractVolume(basket, new Vector3(0f, 0.7f, 0f), new Vector3(0.8f, 1.4f, 0.8f));

            return SaveNetworkPrefab(root, HearthPath);
        }

        private static Transform Part(string name, Transform parent, Vector3 position, float yaw)
        {
            var part = new GameObject(name).transform;
            part.SetParent(parent, false);
            part.localPosition = position;
            part.localEulerAngles = new Vector3(0f, yaw, 0f);
            return part;
        }

        private static void InteractVolume(Transform parent, Vector3 centre, Vector3 size)
        {
            var volume = new GameObject("InteractVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(parent, false);
            volume.transform.localPosition = centre;
            volume.isTrigger = true;
            volume.size = size;
        }

        private static GameObject NetworkedRoot(string name)
        {
            var root = new GameObject(name);
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = true;
            networkObject.SyncOwnerTransformWhenParented = true;
            root.AddComponent<EntityNetworkMediator>();
            return root;
        }

        private static GameObject SaveNetworkPrefab(GameObject root, string path)
        {
            EnsureFolder(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            // The NetworkObject's id was hashed while it was a scene object; its validation re-hashes it from the asset,
            // or a client would refuse the host.
            var saved = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(saved, null);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        private static void Visual(PrimitiveType shape, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = name;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localEulerAngles = euler;
            visual.transform.localScale = scale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                EnsureFolder(path);
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing asset {path}.");

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            (serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}."))
                .objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            (serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}."))
                .stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AddUnique(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");
            var present = new HashSet<Object>();
            for (int i = 0; i < property.arraySize; i++) present.Add(property.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var value in values)
            {
                if (!present.Add(value)) continue;
                property.arraySize++;
                property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string assetPath) => EnsureDirectory(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));

        private static void EnsureDirectory(string? folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
