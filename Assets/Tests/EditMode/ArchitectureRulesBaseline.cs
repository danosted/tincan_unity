#nullable enable

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Today's known offenders for <see cref="ArchitectureRulesTests"/>. Lists may only shrink and limits may only
    /// go down: remove an entry when you fix it, and never add one without the developer's agreement.
    /// Source: <c>.docs/plans/architecture-review.md</c>.
    /// </summary>
    internal static class ArchitectureRulesBaseline
    {
        /// <summary><c>NetworkBehaviour</c>s not named <c>*NetworkMediator</c> (review C11, plus <c>HumanoidPlayer</c> and <c>NetworkTransformMediator</c>, found by this suite).</summary>
        public static readonly string[] MisnamedNetworkBehaviours =
        {
            "AirshipControlPanel",
            "AirshipDoor",
            "HumanoidPlayer",
            "NetworkTransformMediator",
            "ToggleShipTagStation",
        };

        /// <summary>Global or scene lookups in <c>Assets/Scripts/Features</c>, as "path | pattern" (review B7).</summary>
        public static readonly string[] GlobalLookupsInFeatures =
        {
        };

        /// <summary>Registration calls in <c>ProjectLifetimeScope.cs</c> (review B5).</summary>
        public const int ProjectLifetimeScopeRegistrationLimit = 37;

        /// <summary>Processors, use cases and interaction handlers no test file mentions (review C13).</summary>
        public static readonly string[] UntestedTypes =
        {
            "ActivateAbilityInteractionHandler",
            "AirshipMovementUseCase",
            "DoorInteractionHandler",
            "FreeCameraMovementProcessor",
            "FreeCameraMovementUseCase",
            "FreeCameraRotationProcessor",
            "NetworkConditionsUseCase",
            "PossessionInteractionHandler",
            "PossessionUseCase",
            "ScenarioUseCase",
            "VehicleBoardingUseCase",
        };

        /// <summary>Shared prefab components that need a feature's service, or starting abilities a feature also grants, as "prefab | component | dependency".</summary>
        public static readonly string[] SharedPrefabFeatureDependencies =
        {
        };

        /// <summary>
        /// Installers still compiled into a shared assembly instead of their own (<c>.docs/plans/feature-assemblies.md</c>).
        /// Carve a feature out with its own asmdef, then remove it here; <c>GasChallenge</c> is the worked example.
        /// </summary>
        public static readonly string[] InstallersInSharedAssemblies =
        {
        };

        /// <summary>
        /// Folders under <c>Assets/Scripts</c> whose scripts may compile into Assembly-CSharp. Empty since the top layer
        /// got its own assemblies (<c>TinCan.App</c>, <c>TinCan.Network</c>); keep it empty.
        /// </summary>
        public static readonly string[] AssemblyCSharpFolders =
        {
        };

        /// <summary>Scripts under <c>Assets/Scripts</c> without <c>#nullable enable</c> (review C12).</summary>
        public const int FilesWithoutNullableLimit = 104;
    }
}
