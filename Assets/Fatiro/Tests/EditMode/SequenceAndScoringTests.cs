using System.Collections.Generic;
using System.Linq;
using Fatiro.Domain;
using NUnit.Framework;

namespace Fatiro.Tests
{
    public sealed class BuildSequenceTests
    {
        [Test]
        public void GuidedBuild_RejectsLaterStageWithReason()
        {
            var blueprint = Blueprints.Car();
            var sequence = new BuildSequence(blueprint, BuildMode.GuidedBuild);
            var roof = blueprint.Get("CR-ROOF-REAR");

            var check = sequence.CanPlace(roof);

            Assert.That(check.Allowed, Is.False);
            Assert.That(check.Reason, Does.Contain("Foundation"));
        }

        [Test]
        public void MissingPrerequisite_IsNamed()
        {
            var blueprint = Blueprints.Car();
            var sequence = new BuildSequence(blueprint, BuildMode.FreeBuild);

            var check = sequence.CanPlace(blueprint.Get("CR-STILT-L0"));

            Assert.That(check.Allowed, Is.False);
            Assert.That(check.MissingPrerequisite.Id, Is.EqualTo("CR-PAD-L0"));
            Assert.That(check.Reason, Does.Contain(check.MissingPrerequisite.DisplayName));
        }

        [Test]
        public void StageCompletesWhenAllRequiredMembersArePlaced()
        {
            var blueprint = Blueprints.Car();
            var sequence = new BuildSequence(blueprint, BuildMode.GuidedBuild);
            var pads = blueprint.Members.Where(m => m.Stage == BuildStage.Foundation && m.Required).ToList();

            IReadOnlyList<BuildStage> completed = null;
            foreach (var pad in pads) completed = sequence.MarkPlaced(pad.Id);

            Assert.That(completed, Does.Contain(BuildStage.Foundation));
            Assert.That(sequence.CurrentStage, Is.EqualTo(BuildStage.PrimaryStructure));
        }

        [Test]
        public void NextRequired_FollowsTheConstructionOrderToCompletion()
        {
            foreach (var blueprint in Blueprints.All())
            {
                var sequence = new BuildSequence(blueprint, BuildMode.GuidedBuild);
                var guard = 0;
                while (!sequence.IsComplete && guard++ < 400)
                {
                    var next = sequence.NextRequired();
                    Assert.That(next, Is.Not.Null, $"{blueprint.Name} stalled at {sequence.CurrentStage}");
                    Assert.That(sequence.CanPlace(next).Allowed, Is.True);
                    sequence.MarkPlaced(next.Id);
                }
                Assert.That(sequence.IsComplete, Is.True, blueprint.Name);
                Assert.That(sequence.Progress, Is.EqualTo(1f));
            }
        }


        [Test]
        public void NextComponent_BuildsEveryDesignInWholeComponents()
        {
            foreach (var blueprint in Blueprints.All())
            {
                var sequence = new BuildSequence(blueprint, BuildMode.GuidedBuild);
                var guard = 0;
                while (!sequence.IsComplete && guard++ < 100)
                {
                    var next = sequence.NextComponent();
                    Assert.That(next, Is.Not.Null, $"{blueprint.Name} stalled at {sequence.CurrentStage}");
                    Assert.That(sequence.CanPlace(next).Allowed, Is.True, next.Name);
                    foreach (var spec in sequence.Remaining(next)) sequence.MarkPlaced(spec.Id);
                    Assert.That(sequence.IsBuilt(next), Is.True);
                }
                Assert.That(sequence.IsComplete, Is.True, blueprint.Name);
            }
        }

        [Test]
        public void Component_NeedsTheComponentItRestsOn()
        {
            var blueprint = Blueprints.Car();
            var sequence = new BuildSequence(blueprint, BuildMode.FreeBuild);
            var stilts = blueprint.Components.First(c => c.Name == "Concrete stilts");

            var check = sequence.CanPlace(stilts);

            Assert.That(check.Allowed, Is.False);
            Assert.That(check.Reason, Does.Contain("Brick piers"));
        }

        [Test]
        public void Component_MayRestOnItsOwnMembers()
        {
            // A roof ring beam rests on the columns of the same storey, which are a different component;
            // within one component, later members may rest on earlier ones.
            var blueprint = Blueprints.Bungalow();
            var sequence = new BuildSequence(blueprint, BuildMode.FreeBuild);
            foreach (var component in blueprint.Components)
            {
                if (component.Stage != BuildStage.Foundation) continue;
                Assert.That(sequence.CanPlace(component).Allowed, Is.True, component.Name);
                foreach (var spec in component.Members) sequence.MarkPlaced(spec.Id);
            }
        }

        [Test]
        public void ComponentsGroupEveryMemberOnce()
        {
            foreach (var blueprint in Blueprints.All())
            {
                var grouped = blueprint.Components.SelectMany(c => c.Members).Select(m => m.Id).ToList();
                Assert.That(grouped, Is.EquivalentTo(blueprint.Members.Select(m => m.Id)), blueprint.Name);
                Assert.That(blueprint.Components.Count, Is.LessThan(blueprint.Members.Count / 2), $"{blueprint.Name} should build fast");
            }
        }

        [Test]
        public void ShellPatchesHaveRealArea()
        {
            foreach (var blueprint in Blueprints.All())
            foreach (var member in blueprint.Members.Where(m => m.Shape == MemberShape.Shell))
            {
                var stations = blueprint.Shells[member.Patch.Profile].Resolve(null);
                var (area, _) = ShellMath.Measure(stations, member.Patch);
                Assert.That(area, Is.GreaterThan(0.2f), member.Id);
            }
        }

        [Test]
        public void ShellSectionRunsFromRightBaseOverTheCrownToLeftBase()
        {
            var station = new ShellStation(0f, 1f, 4f, 2f, 2f);
            var right = ShellMath.Point(station, 0f);
            var crown = ShellMath.Point(station, 0.5f);
            var left = ShellMath.Point(station, 1f);
            Assert.That(right.X, Is.EqualTo(2f).Within(0.001f));
            Assert.That(right.Y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(crown.Y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(left.X, Is.EqualTo(-2f).Within(0.001f));
        }
        [Test]
        public void CannotRemoveAMemberSomethingRestsOn()
        {
            var blueprint = Blueprints.Car();
            var sequence = new BuildSequence(blueprint, BuildMode.FreeBuild);
            sequence.MarkPlaced("CR-PAD-L0");
            sequence.MarkPlaced("CR-STILT-L0");

            Assert.That(sequence.CanRemove(blueprint.Get("CR-PAD-L0")).Allowed, Is.False);
            Assert.That(sequence.CanRemove(blueprint.Get("CR-STILT-L0")).Allowed, Is.True);
        }

        [Test]
        public void BlueprintsHaveNoDanglingSupports()
        {
            foreach (var blueprint in Blueprints.All())
            foreach (var member in blueprint.Members)
            foreach (var support in member.Supports.Concat(member.ExtraSupports))
                Assert.That(blueprint.Get(support), Is.Not.Null, $"{member.Id} → {support}");
        }
    }

    public sealed class ScoringTests
    {
        private readonly WeightedScoringPolicy policy = new WeightedScoringPolicy();

        [Test]
        public void PerfectSessionScores100()
        {
            var input = new ScoreInput(10, 10, new[] { new CaseOutcome(LoadCaseKind.Seismic, 0.5f, 0, 30) },
                new[] { LoadCaseKind.Seismic }, materialCost: 50f, baselineCost: 50f);
            Assert.That(policy.Score(input).Total, Is.EqualTo(100));
        }

        [Test]
        public void WeightsAreFortyFortyTwenty()
        {
            var input = new ScoreInput(10, 5, new CaseOutcome[0], new[] { LoadCaseKind.Seismic }, 100f, 50f);
            var score = policy.Score(input);
            Assert.That(score.Assembly, Is.EqualTo(20));
            Assert.That(score.Safety, Is.EqualTo(0));
            Assert.That(score.Cost, Is.EqualTo(10));
        }

        [Test]
        public void UntestedFocusCaseCountsAsZeroSafety()
        {
            var tested = new[] { new CaseOutcome(LoadCaseKind.Wind, 0.5f, 0, 20) };
            var input = new ScoreInput(1, 1, tested, new[] { LoadCaseKind.Wind, LoadCaseKind.Rain }, 1f, 1f);
            Assert.That(WeightedScoringPolicy.SafetyRatio(input), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void CollapseScoresLowerThanSurvival()
        {
            var survived = WeightedScoringPolicy.CaseScore(new CaseOutcome(LoadCaseKind.Seismic, 0.95f, 0, 30));
            var collapsed = WeightedScoringPolicy.CaseScore(new CaseOutcome(LoadCaseKind.Seismic, 1.3f, 3, 30));
            Assert.That(collapsed, Is.LessThan(survived));
        }

        [Test]
        public void SessionRecordIsImmutableAfterClose()
        {
            var record = SessionRecord.Start("l", "Learner", Blueprints.Car(), new SiteConditions(20, SoilType.Laterite),
                BuildMode.GuidedBuild, System.DateTime.UtcNow);
            record.Close(new ScoreBreakdown(40, 40, 20, 60), System.DateTime.UtcNow);
            Assert.Throws<System.InvalidOperationException>(() => record.AddPlacement(new PlacementEvent()));
        }
    }
}
