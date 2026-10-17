using System.Collections.Generic;
using Fatiro.Build;
using Fatiro.Core;
using Fatiro.Domain;

namespace Fatiro.Simulation
{
    /// <summary>
    /// Builds the load case, runs the structural solver on the placed members, applies the results
    /// and announces failures (FR-15 to FR-19). Keeps the last result per case so a re-test can
    /// be compared with the one before it (FR-21).
    /// </summary>
    public sealed class LoadTestManager
    {
        private readonly IEventBus events;
        private readonly EntityManager entities;
        private readonly IStructuralSolver solver;
        private readonly Dictionary<LoadCaseKind, LoadTestResult> latest = new Dictionary<LoadCaseKind, LoadTestResult>();

        public LoadTestManager(IEventBus events, EntityManager entities, IStructuralSolver solver)
        {
            this.events = events;
            this.entities = entities;
            this.solver = solver;
        }

        public LoadTestResult LastResult { get; private set; }
        public IReadOnlyDictionary<LoadCaseKind, LoadTestResult> Latest => latest;
        public int TestsRun { get; private set; }

        public LoadTestResult Run(LoadCaseConfig config, SiteConditions site)
        {
            events.Publish(new LoadTestStarted(config));

            var loadCase = LoadCaseFactory.Create(config);
            var blueprint = entities.Blueprint;
            var result = solver.Solve(entities.Graph, site, blueprint.Foundation, blueprint.HasDrainage, loadCase);

            latest.TryGetValue(config.Kind, out var previous);
            latest[config.Kind] = result;
            LastResult = result;
            TestsRun++;

            entities.ApplyResults(result);
            foreach (var failed in result.Failed)
                events.Publish(new MemberFailed(failed.MemberId, failed.FailureWave, failed.FailureReason));
            events.Publish(new LoadTestCompleted(result, previous));
            return result;
        }

        public void Reset()
        {
            latest.Clear();
            LastResult = null;
            TestsRun = 0;
        }
    }
}
