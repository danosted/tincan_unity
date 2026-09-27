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
            "CoordinatedEventMediator",
            "HumanoidPlayer",
            "NetworkTransformMediator",
            "ToggleShipTagStation",
        };

        /// <summary>Global or scene lookups in <c>Assets/Scripts/Features</c>, as "path | pattern" (review B7).</summary>
        public static readonly string[] GlobalLookupsInFeatures =
        {
            "Scripts/Features/CloudBoundary/CloudEnvironmentView.cs | Camera.main",
            "Scripts/Features/GasChallenge/GasChallengeUseCase.cs | Find*ObjectsBy/OfType",
            "Scripts/Features/Interaction/InteractorControllerView.cs | Camera.main",
            "Scripts/Features/ThirdPersonCharacter/ThirdPersonLookView.cs | NetworkManager.Singleton",
        };

        /// <summary>Registration calls in <c>ProjectLifetimeScope.cs</c> (review B5).</summary>
        public const int ProjectLifetimeScopeRegistrationLimit = 50;

        /// <summary>Processors, use cases and interaction handlers no test file mentions (review C13).</summary>
        public static readonly string[] UntestedTypes =
        {
            "ActivateAbilityInteractionHandler",
            "AirshipMovementUseCase",
            "DoorInteractionHandler",
            "EventOrchestratorUseCase",
            "FreeCameraMovementProcessor",
            "FreeCameraMovementUseCase",
            "FreeCameraRotationProcessor",
            "GasChallengeUseCase",
            "InteractivityUseCase",
            "MaintenanceUseCase",
            "NetworkConditionsUseCase",
            "PlayerLookUseCase",
            "PossessionInteractionHandler",
            "PossessionUseCase",
            "ScenarioUseCase",
            "VehicleBoardingUseCase",
        };

        /// <summary>Scripts under <c>Assets/Scripts</c> without <c>#nullable enable</c> (review C12).</summary>
        public const int FilesWithoutNullableLimit = 115;
    }
}
