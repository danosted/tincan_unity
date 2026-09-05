# Feature Installers & Ship Fixtures

**Why this exists.** Every feature used to edit the same four files: `ProjectLifetimeScope.cs` (registrations plus a
`[SerializeField]` per config), `GameLifetimeScope.prefab` (the values for those fields), `DefaultNetworkPrefabs.asset`
(every networked prefab) and `Airship_Prefab.prefab` (every fixture on the ship). With several people working in
parallel those files were in permanent conflict. A feature now contributes everything through **one asset it owns**.

## The pieces

| Piece | Where | Role |
|---|---|---|
| `FeatureInstaller` | `Core/Domain/Features/` | Abstract `ScriptableObject`. Holds the feature's config references, registers its services, lists its networked prefabs and its ship fixtures. |
| `FeatureInstallerCatalog` | `Core/Domain/Features/` | Loads every installer from any `Resources/Installers` folder, orders them (`Order`, then name), and aggregates prefabs and fixtures. |
| `ShipFixtureDefinition` | `Core/Domain/Features/` | A networked prefab plus a ship-local pose. |
| `ShipFixtureSpawningUseCase` | `Features/Airship/Fixtures/` | Server only. Furnishes each airship once with all fixtures, spawning each as its own `NetworkObject` parented to the ship (via `IModuleSpawningService`, the same path build-mode modules use). |
| `ISimulationTickable` | `Core/Domain/` | Lets a feature run on the fixed network tick without editing `NetworkSimulationScheduler`. Phases: `AfterAirship`, `AfterHumanoid`. |
| `IInjectedView` | `Core/Domain/` | Marker for scene/prefab `MonoBehaviour`s that want container injection at build (UI overlays). |

`ProjectLifetimeScope` does four generic things for installers: calls `Install(builder)` on each, registers each
`NetworkedPrefabs` entry with NGO at runtime (`NetworkManager.AddNetworkPrefab`) and with the DI interceptor,
injects every `IInjectedView`, and calls `OnContainerBuilt`. It has no per-feature fields any more.

Installers in this repo: `Assets/Resources/Installers/UiFeatureInstaller.asset`, `FuelFeatureInstaller.asset`,
`FlyingCanFeatureInstaller.asset`.

## Adding a feature

1. **Code**: subclass `FeatureInstaller` in your feature folder.

```csharp
[CreateAssetMenu(fileName = "MyFeatureInstaller", menuName = "TinCan/Features/My Feature Installer")]
public class MyFeatureInstaller : FeatureInstaller
{
    [SerializeField] private MyConfig? _config;
    [SerializeField] private ShipFixtureDefinition? _station;
    [SerializeField] private GameObject? _projectilePrefab;

    public override void Install(IContainerBuilder builder)
    {
        builder.RegisterInstance(_config!);
        builder.Register<MyProcessor>(Lifetime.Transient);
        builder.Register<MyUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>(); // or .As<ITickable>()
        builder.Register<MyInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
    }

    public override IEnumerable<GameObject> NetworkedPrefabs
    {
        get { if (_projectilePrefab != null) yield return _projectilePrefab; }
    }

    public override IEnumerable<ShipFixtureDefinition> ShipFixtures
    {
        get { if (_station != null) yield return _station; }
    }
}
```

2. **Asset**: create it under `Assets/Resources/Installers/` (any `Resources/Installers` folder works) and fill in the
   references. Order only matters if you depend on another feature's registrations (`UiFeatureInstaller` is -10,
   `FlyingCanFeatureInstaller` is 10 because it needs the fuel feature).
3. **Fixtures on the ship**: make the fixture a prefab with a `NetworkObject` (`AutoObjectParentSync` and
   `SyncOwnerTransformWhenParented` on, like `Cannon_Module`), no `NetworkTransform` needed since it rides the ship
   as a child. Author its parts in ship-local coordinates. Optionally implement `IShipModule` on the root to get
   `OnAttachedToShip(ship)` on the server; on clients override `OnNetworkObjectParentChanged` or read
   `transform.parent` in `OnNetworkSpawn` (see `FuelTankNetworkMediator`). Create a `ShipFixtureDefinition` asset
   (**TinCan > Features > Ship Fixture**) pointing at the prefab and list it from the installer.
4. **Networked prefabs you spawn yourself** (projectiles, debris): list them in `NetworkedPrefabs`; do not add them
   to `DefaultNetworkPrefabs.asset`. NGO normally auto-adds every imported network prefab to that list, which would
   register it twice; the project therefore has **Generate Default Network Prefabs** turned off
   (`ProjectSettings/NetcodeForGameObjects.asset`, also under Project Settings > Netcode for GameObjects). The
   lifetime scope skips runtime registration for anything already in a list asset, so a stray entry is harmless.
5. **Views**: a `MonoBehaviour` implementing `IInjectedView` placed on the `GameLifetimeScope` prefab (or any scene
   object) is injected automatically.

Nothing in `ProjectLifetimeScope`, `NetworkSimulationScheduler`, `GameLifetimeScope.prefab`, `Airship_Prefab.prefab`
or `DefaultNetworkPrefabs.asset` needs to change.

## What still needs a shared file

- Components that must live on the airship's or player's own `NetworkObject` (NGO requires `NetworkBehaviour`s to
  exist at spawn). Keep those as one nested prefab per feature so the shared prefab only ever gains one child line.
- Starting abilities on the player prefab and the input binding config are still lists in shared assets.

## Merging Unity YAML

Prefabs, scenes and assets merge far better with Unity's own merge tool. `.gitattributes` marks them with
`merge=unityyamlmerge`; each developer registers the driver once:

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.4.5f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %A %B %A"
```

Adjust the path to your Editor install. Without the driver configured, git falls back to a plain text merge.
