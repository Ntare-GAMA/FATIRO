using System;
using System.Collections.Generic;
using System.Linq;
using Fatiro.Domain;
using NUnit.Framework;

namespace Fatiro.Tests
{
    internal static class TestKit
    {
        public static readonly Dictionary<string, MaterialSpec> Materials = MaterialLibrary.CreateDefaults().ToDictionary(m => m.Id);

        public static ConnectionGraph Build(HouseBlueprint blueprint, SiteConditions site, bool includeOptional,
            IDictionary<string, string> materialOverrides = null)
        {
            var factory = new MemberFactory(new SiteLayout(site, blueprint.UphillReferenceZ));
            var graph = new ConnectionGraph();
            foreach (var spec in blueprint.Members)
            {
                if (!spec.Required && !includeOptional) continue;
                var material = materialOverrides != null && materialOverrides.TryGetValue(spec.Id, out var chosen) ? chosen : spec.DefaultMaterialId;
                graph.Add(factory.Create(spec, Materials[material]));
            }
            return graph;
        }

        public static LoadTestResult Solve(HouseBlueprint blueprint, SiteConditions site, ILoadCase loadCase, bool includeOptional = false,
            IDictionary<string, string> materialOverrides = null)
        {
            var graph = Build(blueprint, site, includeOptional, materialOverrides);
            return new StructuralSolver().Solve(graph, site, blueprint.Foundation, blueprint.HasDrainage, loadCase);
        }

        /// <summary>Footing → column → beam ← column ← footing, on flat rock, for hand-checkable numbers.</summary>
        public static HouseBlueprint Portal(float columnHeight = 3f, float span = 4f, bool brace = false)
        {
            var b = new BlueprintBuilder();
            var f1 = b.Pad("F1", "Footing 1", 0f, 0f, 1f, 1f, 0.2f, MaterialLibrary.Concrete);
            var f2 = b.Pad("F2", "Footing 2", span, 0f, 1f, 1f, 0.2f, MaterialLibrary.Concrete);
            var c1 = b.Strut("C1", "Column 1", MemberKind.Column, BuildStage.PrimaryStructure, Endpoint.Ground(0, 0, 0),
                Endpoint.At(0, columnHeight, 0), 0.2f, MaterialLibrary.Timber, MaterialLibrary.Frame, BlueprintBuilder.On(f1));
            var c2 = b.Strut("C2", "Column 2", MemberKind.Column, BuildStage.PrimaryStructure, Endpoint.Ground(span, 0, 0),
                Endpoint.At(span, columnHeight, 0), 0.2f, MaterialLibrary.Timber, MaterialLibrary.Frame, BlueprintBuilder.On(f2));
            b.Strut("B1", "Beam", MemberKind.Beam, BuildStage.PrimaryStructure, Endpoint.At(0, columnHeight, 0), Endpoint.At(span, columnHeight, 0),
                0.2f, MaterialLibrary.Timber, MaterialLibrary.Frame, BlueprintBuilder.On(c1, c2), tributaryArea: 8f);
            if (brace)
                b.Strut("X1", "Brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(0, 0, 0), Endpoint.At(span, columnHeight, 0),
                    0.2f, MaterialLibrary.Timber, MaterialLibrary.Bracing, BlueprintBuilder.On(c1, c2));
            return new HouseBlueprint("TEST", 1, "Portal", "test", "", "", 1,
                new FoundationStyle("Pad", "", 1f, 0.5f, 1f), 0f, b.Members);
        }
    }

    /// <summary>NFR-14: solver results within ±15 % of hand-calculated reference values.</summary>
    public sealed class SolverBenchmarkTests
    {
        private static readonly SiteConditions FlatRock = new SiteConditions(0, SoilType.Rock);

        [Test]
        public void Benchmark1_BeamUnderOccupancy_MatchesHandCalculation()
        {
            // Hand calculation: beam self weight 0.25 kN/m × 4 m = 1.0 kN; floor finish 1.5 kN/m² × 8 m² = 12 kN;
            // occupancy 3 kN/m² × 8 m² = 24 kN → demand 37 kN.
            // Capacity = 200 × 1.2 (timber bending) × 1.0 (0.2 m section) / 4 m = 60 kN → utilisation 0.617.
            var result = TestKit.Solve(TestKit.Portal(), FlatRock, new LiveLoad(3f));
            Assert.That(result.For("B1").Utilisation, Is.EqualTo(37f / 60f).Within(0.15f * 37f / 60f));
        }

        [Test]
        public void Benchmark2_ColumnCarriesHalfTheBeam_MatchesHandCalculation()
        {
            // Hand calculation: each column takes half the beam (13 kN / 2 = 6.5 kN) plus its own weight 0.25 × 3 = 0.75 kN → 7.25 kN.
            // Buckling capacity = 120 × 1.2 / (1 + (3 / 3.5)²) = 83.0 kN → utilisation 0.087.
            var result = TestKit.Solve(TestKit.Portal(), FlatRock, new DeadLoad());
            Assert.That(result.For("C1").VerticalDemand, Is.EqualTo(7.25f).Within(0.15f * 7.25f));
            Assert.That(result.For("C1").Utilisation, Is.EqualTo(7.25f / 83.0f).Within(0.15f * 7.25f / 83.0f));
        }

        [Test]
        public void Benchmark3_SeismicForce_IsCoefficientTimesShakingWeight()
        {
            // Shaking weight (no footings): columns 2 × 0.75 + beam 1.0 + floor finish 12 = 14.5 kN.
            // Coefficient at level 3 on flat rock = 0.1 × 3 × 1.0 × 1.0 = 0.3 → 4.35 kN.
            var result = TestKit.Solve(TestKit.Portal(), FlatRock, new SeismicLoad(3f));
            Assert.That(result.LateralForce, Is.EqualTo(4.35f).Within(0.15f * 4.35f));
        }

        [Test]
        public void RainRunoff_IsIntensityTimesRoofArea()
        {
            var site = new SiteConditions(0, SoilType.Laterite);
            var result = TestKit.Solve(Blueprints.Elephant(), site, new RainLoad(50f), includeOptional: false,
                new Dictionary<string, string> { { "EL-TRUNK", MaterialLibrary.Steel } });
            var roofArea = Blueprints.Elephant().Members.Where(m => m.Kind == MemberKind.Panel && m.IsRoof)
                .Sum(m => m.PanelSize.X * m.PanelSize.Z);
            Assert.That(result.RunoffLitresPerHour, Is.EqualTo(50f * roofArea).Within(0.01f));
        }
    }

    public sealed class SolverBehaviourTests
    {
        private static readonly SiteConditions Slope20 = new SiteConditions(20, SoilType.Laterite);

        [Test]
        public void SolverDoesNotMutateMembers()
        {
            var graph = TestKit.Build(Blueprints.Car(), Slope20, false);
            new StructuralSolver().Solve(graph, Slope20, Blueprints.Car().Foundation, false, new SeismicLoad(5f));
            Assert.That(graph.Members.All(m => m.State == MemberState.Placed && m.Utilisation == 0f));
        }

        [Test]
        public void MembersThatLoseAllSupportsFailInALaterWave()
        {
            var result = TestKit.Solve(Blueprints.Car(), Slope20, new SeismicLoad(3f));
            var stilt = result.For("CR-STILT-L2");
            var dependants = result.Failed.Where(r => r.FailureReason.StartsWith("lost support")).ToList();
            Assert.That(stilt.Failed, Is.True);
            Assert.That(dependants, Is.Not.Empty);
            Assert.That(dependants.All(r => r.FailureWave > stilt.FailureWave));
        }

        [Test]
        public void BracingIncreasesColumnCapacity()
        {
            var unbraced = TestKit.Solve(TestKit.Portal(4f), new SiteConditions(0, SoilType.Rock), new SeismicLoad(5f));
            var braced = TestKit.Solve(TestKit.Portal(4f, brace: true), new SiteConditions(0, SoilType.Rock), new SeismicLoad(5f));
            Assert.That(braced.For("C1").Utilisation, Is.LessThan(unbraced.For("C1").Utilisation));
        }

        [Test]
        public void SteeperSlopeMakesDownhillStiltsWorse()
        {
            var flat = TestKit.Solve(Blueprints.Car(), new SiteConditions(0, SoilType.Laterite), new DeadLoad());
            var steep = TestKit.Solve(Blueprints.Car(), new SiteConditions(30, SoilType.Laterite), new DeadLoad());
            Assert.That(steep.For("CR-STILT-L2").Utilisation, Is.GreaterThan(flat.For("CR-STILT-L2").Utilisation));
        }

        [Test]
        public void PlinthWallsTieFootingsAgainstSliding()
        {
            var car = Blueprints.Car();
            var graph = TestKit.Build(car, Slope20, false);
            var pier = graph.Get("CR-PAD-L2");
            Assert.That(graph.IsBraced(pier, _ => true), Is.True, "plinth walls tie the pier");
            var tied = pier.LateralCapacity(new CapacityContext(Slope20, car.Foundation, true, false));
            var loose = pier.LateralCapacity(new CapacityContext(Slope20, car.Foundation, false, false));
            Assert.That(tied, Is.EqualTo(loose * Calibration.TiedFootingFactor).Within(0.001f));
        }

        [Test]
        public void PlinthWallsAddNoShakingMass()
        {
            var car = Blueprints.Car();
            var withWalls = TestKit.Solve(car, Slope20, new SeismicLoad(3f));
            var graph = TestKit.Build(car, Slope20, false);
            foreach (var wall in car.Members.Where(m => m.Id.StartsWith("CR-PLINTH")).ToList()) graph.Remove(wall.Id);
            var withoutWalls = new StructuralSolver().Solve(graph, Slope20, car.Foundation, false, new SeismicLoad(3f));
            Assert.That(withWalls.LateralForce, Is.EqualTo(withoutWalls.LateralForce).Within(0.01f));
        }

        [Test]
        public void LoadCaseMagnitudesAreValidated()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WindLoad(200f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeismicLoad(-1f));
        }
    }

    /// <summary>Regression tests for the teaching scenarios each design was calibrated for.</summary>
    public sealed class TeachingScenarioTests
    {
        private static readonly SiteConditions Slope20 = new SiteConditions(20, SoilType.Laterite);

        [Test]
        public void Car_UnbracedDownhillStiltsFailInQuake3_BracingFixesIt()
        {
            var before = TestKit.Solve(Blueprints.Car(), Slope20, new SeismicLoad(3f));
            Assert.That(before.For("CR-STILT-L2").Failed, Is.True, "downhill stilt should fail");
            Assert.That(before.PeakUtilisation, Is.InRange(1.1f, 1.5f));

            var after = TestKit.Solve(Blueprints.Car(), Slope20, new SeismicLoad(3f), includeOptional: true);
            Assert.That(after.Survived, Is.True);
            Assert.That(after.PeakUtilisation, Is.LessThan(1f));
        }

        [Test]
        public void Elephant_EarsLiftOffInWind_TieDownsFixIt()
        {
            var before = TestKit.Solve(Blueprints.Elephant(), Slope20, new WindLoad(120f));
            Assert.That(before.For("EL-EAR-L-SPAR").Failed, Is.True);

            var after = TestKit.Solve(Blueprints.Elephant(), Slope20, new WindLoad(120f), includeOptional: true);
            Assert.That(after.Survived, Is.True);
        }

        [Test]
        public void Elephant_BambooTrunkOverflowsInHeavyRain_SteelTrunkDrainsIt()
        {
            var before = TestKit.Solve(Blueprints.Elephant(), Slope20, new RainLoad(80f));
            Assert.That(before.For("EL-TRUNK").Failed, Is.True);

            var after = TestKit.Solve(Blueprints.Elephant(), Slope20, new RainLoad(80f), false,
                new Dictionary<string, string> { { "EL-TRUNK", MaterialLibrary.Steel } });
            Assert.That(after.Survived, Is.True);
        }

        [Test]
        public void Panther_UnbracedRibsCollapseInQuake_FullBracingSurvives()
        {
            var before = TestKit.Solve(Blueprints.Panther(), Slope20, new SeismicLoad(3f));
            Assert.That(before.Failed.Count(r => r.MemberId.StartsWith("PN-RIB")), Is.GreaterThanOrEqualTo(2));

            var after = TestKit.Solve(Blueprints.Panther(), Slope20, new SeismicLoad(3f), includeOptional: true);
            Assert.That(after.Survived, Is.True);
        }

        [Test]
        public void Panther_HeadCantileverFailsUnderPeople_PropsFixIt()
        {
            var before = TestKit.Solve(Blueprints.Panther(), Slope20, new LiveLoad(3f));
            Assert.That(before.For("PN-HEAD").Failed, Is.True);

            var after = TestKit.Solve(Blueprints.Panther(), Slope20, new LiveLoad(3f), includeOptional: true);
            Assert.That(after.For("PN-HEAD").Failed, Is.False);
        }

        [Test]
        public void AllDesignsCarryTheirOwnWeight()
        {
            foreach (var blueprint in Blueprints.All())
            foreach (var slope in SiteConditions.AllowedSlopes)
                Assert.That(TestKit.Solve(blueprint, new SiteConditions(slope, SoilType.Clay), new DeadLoad()).Survived,
                    Is.True, $"{blueprint.Name} at {slope}°");
        }
    }
}
