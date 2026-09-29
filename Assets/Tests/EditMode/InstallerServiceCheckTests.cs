#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>Covers InstallerServiceCheck: which dependencies of an installer's services the loaded registrations cannot satisfy.</summary>
    public class InstallerServiceCheckTests
    {
        public interface IFuelTank { }
        public interface IHud { }
        public sealed class FuelTank : IFuelTank { }

        public sealed class FuelUseCase
        {
            public FuelUseCase(IFuelTank tank, IObjectResolver resolver, IEnumerable<IHud> huds) { }
        }

        public sealed class CatchUseCase
        {
            [Inject] public CatchUseCase(IFuelTank tank) { }
            public CatchUseCase(IFuelTank tank, IHud testOnly) { }
        }

        public sealed class DoorHandler
        {
            public DoorHandler(IHud handlerTag, IFuelTank tank) { }
        }

        public sealed class HudView : MonoBehaviour
        {
            [Inject] public void Construct(IHud hud) { }
        }

        private sealed class TestInstaller : FeatureInstaller
        {
            public Action<IContainerBuilder> OnInstall = _ => { };
            public override void Install(IContainerBuilder builder) => OnInstall(builder);
        }

        private readonly List<Object> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void MissingDependency_IsReportedWithItsInstallerAndService()
        {
            var fuel = Installer("Fuel", b => b.Register<FuelUseCase>(Lifetime.Singleton));

            var missing = Check(fuel);

            Assert.That(missing, Is.EqualTo(new[] { "Fuel: FuelUseCase needs IFuelTank" }));
        }

        [Test]
        public void DependencyFromAnotherLoadedInstaller_IsSatisfied_AndResolverAndCollectionsNeverMissing()
        {
            var tanks = Installer("Tanks", b => b.Register<FuelTank>(Lifetime.Singleton).As<IFuelTank>());
            var fuel = Installer("Fuel", b => b.Register<FuelUseCase>(Lifetime.Singleton));

            Assert.That(Check(fuel, tanks), Is.Empty);
        }

        [Test]
        public void InjectConstructor_WinsOverTheLongestOne()
        {
            var catchFeature = Installer("Catch", b => b.Register<CatchUseCase>(Lifetime.Singleton));
            var tanks = Installer("Tanks", b => b.Register<FuelTank>(Lifetime.Singleton).As<IFuelTank>());

            Assert.That(Check(catchFeature, tanks), Is.Empty, "the [Inject] constructor takes no IHud");
        }

        [Test]
        public void ValuesSuppliedWithParameter_AreNotMissing()
        {
            var door = Installer("Door", b => b.Register<DoorHandler>(Lifetime.Singleton).WithParameter("handlerTag", null!));

            Assert.That(Check(door), Is.EqualTo(new[] { "Door: DoorHandler needs IFuelTank" }), "handlerTag comes from the registration");
        }

        [Test]
        public void Instances_AreNotAnalysed()
        {
            var fuel = Installer("Fuel", b => b.RegisterInstance(new FuelUseCase(null!, null!, null!)));

            Assert.That(Check(fuel), Is.Empty);
        }

        [Test]
        public void Dependencies_OfAComponent_AreItsInjectMembersOnly()
        {
            Assert.That(InstallerServiceCheck.Dependencies(typeof(HudView)), Is.EqualTo(new[] { typeof(IHud) }));
        }

        private static List<string> Check(params FeatureInstaller[] installers)
        {
            var builder = new ContainerBuilder();
            var installed = InstallerServiceCheck.Install(installers, builder);
            return InstallerServiceCheck.FindMissing(installed, t => builder.Exists(t, includeInterfaceTypes: true));
        }

        private TestInstaller Installer(string name, Action<IContainerBuilder> install)
        {
            var installer = ScriptableObject.CreateInstance<TestInstaller>();
            installer.name = name;
            installer.OnInstall = install;
            _assets.Add(installer);
            return installer;
        }
    }
}
