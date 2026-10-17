using System.Collections.Generic;
using System.Text;
using Fatiro.Core;
using Fatiro.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>
    /// Load test bench: pick a load case and its magnitude (FR-15), run it, read the results and the
    /// before/after comparison (FR-21), then modify and re-test or finish.
    /// </summary>
    public sealed class TestBenchPanel : UIScreen
    {
        private static readonly (LoadCaseKind kind, string label)[] Cases =
        {
            (LoadCaseKind.Seismic, "Quake"),
            (LoadCaseKind.Wind, "Wind"),
            (LoadCaseKind.Rain, "Rain"),
            (LoadCaseKind.Live, "People"),
            (LoadCaseKind.Dead, "Self weight")
        };

        private readonly List<(LoadCaseKind kind, Button button)> caseButtons = new List<(LoadCaseKind, Button)>();
        private readonly Dictionary<LoadCaseKind, float> magnitudes = new Dictionary<LoadCaseKind, float>();
        private LoadCaseKind selected = LoadCaseKind.Seismic;
        private Slider slider;
        private Text sliderLabel;
        private Text focusHint;
        private GameObject resultsCard;
        private Text verdict;
        private Text resultDetails;

        public TestBenchPanel(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            BuildResults();

            var bench = UIKit.Card(Root, Theme.Surface, 26, 12, "Test bench");
            ((RectTransform)bench.transform).Dock(TextAnchor.LowerCenter, 650, 24);
            UIKit.ImigongoBand(bench.transform, 16);

            var header = UIKit.HStack(bench.transform, 12);
            UIKit.Size(header.gameObject, minHeight: 100, preferredHeight: 100);
            var title = UIKit.Display(header.transform, "Igerageza  ·  Test bench", Theme.Subheading + 4);
            UIKit.Size(title.gameObject, flexibleWidth: 2.4f);
            UIKit.Button(header.transform, "Move site", () => { Click(); Simulation.Reposition(); }, ButtonStyle.Ghost, 100, 26);
            UIKit.Button(header.transform, "Pause", () => { Click(); Simulation.Pause(); }, ButtonStyle.Ghost, 100, 26);

            var row = UIKit.HStack(bench.transform, 8);
            UIKit.Size(row.gameObject, minHeight: 110, preferredHeight: 110);
            foreach (var (kind, label) in Cases)
            {
                var captured = kind;
                caseButtons.Add((kind, UIKit.Button(row.transform, label, () => { Click(); Select(captured); }, ButtonStyle.Secondary, 110, 26)));
                magnitudes[kind] = LoadCaseConfig.Default(kind);
            }

            sliderLabel = UIKit.Label(bench.transform, string.Empty, Theme.Body, style: FontStyle.Bold);
            slider = UIKit.Slider(bench.transform, 0, 1, 0, false, OnSlider);
            focusHint = UIKit.Label(bench.transform, string.Empty, 24, Theme.Muted);

            var actions = UIKit.HStack(bench.transform, 12);
            UIKit.Size(actions.gameObject, minHeight: Theme.TouchTarget, preferredHeight: Theme.TouchTarget);
            var run = UIKit.Button(actions.transform, "Run test", Run);
            UIKit.Size(run.gameObject, minHeight: Theme.TouchTarget, flexibleWidth: 1.4f);
            UIKit.Button(actions.transform, "Modify & re-test", () => { Click(); Simulation.ModifyStructure(); }, ButtonStyle.Secondary, fontSize: Theme.Small);
            UIKit.Button(actions.transform, "Finish", () => { Click(); Simulation.EndSession(); }, ButtonStyle.Ghost, fontSize: Theme.Small);

            Ui.Events.Subscribe<LoadTestCompleted>(ShowResult);
        }

        protected override void OnShow()
        {
            var focus = Session.Setup?.Design?.FocusCases;
            if (Session.LoadTests.TestsRun == 0 && focus != null && focus.Count > 0) Select(focus[0]);
            else Select(selected);
            resultsCard.SetActive(Session.LoadTests.LastResult != null);
        }

        private void BuildResults()
        {
            var card = UIKit.Card(Root, Theme.Surface, 28, 8, "Results");
            var rect = (RectTransform)card.transform;
            rect.Dock(TextAnchor.UpperCenter, 0, 30);
            UIKit.FitHeight(card.gameObject);
            verdict = UIKit.Label(card.transform, string.Empty, Theme.Subheading, style: FontStyle.Bold);
            resultDetails = UIKit.Label(card.transform, string.Empty, Theme.Small);
            UIKit.Legend(card.transform);
            resultsCard = card.gameObject;
            resultsCard.SetActive(false);
        }

        private void Select(LoadCaseKind kind)
        {
            selected = kind;
            foreach (var (caseKind, button) in caseButtons) button.SetStyle(caseKind == kind ? ButtonStyle.Primary : ButtonStyle.Secondary);
            slider.wholeNumbers = LoadCaseConfig.IsWholeNumber(kind);
            slider.minValue = LoadCaseConfig.Minimum(kind);
            slider.maxValue = LoadCaseConfig.Maximum(kind);
            slider.SetValueWithoutNotify(magnitudes[kind]);
            slider.interactable = kind != LoadCaseKind.Dead;

            var focus = Session.Setup?.Design?.FocusCases;
            var isFocus = focus != null && ContainsKind(focus, kind);
            focusHint.text = isFocus ? $"This case is a focus of {Session.Setup.Design.DisplayName} and counts towards your safety score." : Describe(kind);
            UpdateSliderLabel();
        }

        private void OnSlider(float value)
        {
            magnitudes[selected] = value;
            UpdateSliderLabel();
        }

        private void UpdateSliderLabel()
        {
            var value = magnitudes[selected];
            sliderLabel.text = selected switch
            {
                LoadCaseKind.Seismic => $"Ground shaking: level {value:0} of 5",
                LoadCaseKind.Wind => $"Wind speed: {value:0} km/h",
                LoadCaseKind.Rain => $"Rainfall: {value:0} mm/h",
                LoadCaseKind.Live => $"Occupancy: {value:0.0} kN/m² (about {value / 0.75f:0.#} people per m²)",
                _ => "Self weight of the structure and its finishes"
            };
        }

        private void Run()
        {
            Click();
            var value = selected == LoadCaseKind.Dead ? 1f : magnitudes[selected];
            Simulation.RunLoadTest(new LoadCaseConfig(selected, value));
        }

        private void ShowResult(LoadTestCompleted message)
        {
            var result = message.Result;
            resultsCard.SetActive(true);
            var peakColour = World.MemberView.UtilisationColor(result.PeakUtilisation);
            verdict.text = result.Survived
                ? UIKit.Colour("Survived", Theme.Success) + $" — {result.LoadCase.Describe()}"
                : UIKit.Colour($"Collapse: {result.Failed.Count} member{(result.Failed.Count == 1 ? string.Empty : "s")} failed", Theme.Danger) + $" — {result.LoadCase.Describe()}";

            var text = new StringBuilder();
            text.Append($"Peak utilisation {UIKit.Colour($"{result.PeakUtilisation * 100f:0}%", peakColour)}");
            if (message.Previous != null)
            {
                var before = message.Previous.PeakUtilisation;
                var arrow = result.PeakUtilisation < before ? UIKit.Colour("better", Theme.Success) : UIKit.Colour("worse", Theme.Danger);
                text.Append($"   ·   before {before * 100f:0}% to now {result.PeakUtilisation * 100f:0}% ({arrow})");
                text.Append($"\nFailed members: before {message.Previous.Failed.Count} to now {result.Failed.Count}");
            }
            if (result.LoadCase.Kind == LoadCaseKind.Rain)
                text.Append($"\nRoof runoff {result.RunoffLitresPerHour:0} L/h, collected {result.DrainedLitresPerHour:0} L/h");
            if (result.LateralForce > 0.01f) text.Append($"\nSideways force on the structure: {result.LateralForce:0.0} kN");

            var worst = new List<MemberResult>(result.Members);
            worst.Sort((left, right) => right.Utilisation.CompareTo(left.Utilisation));
            text.Append("\nMost loaded: ");
            for (var i = 0; i < Mathf.Min(3, worst.Count); i++)
            {
                var spec = Session.Blueprint.Get(worst[i].MemberId);
                var colour = World.MemberView.UtilisationColor(worst[i].Utilisation);
                text.Append($"{(i > 0 ? ", " : string.Empty)}{spec?.DisplayName} {UIKit.Colour($"{worst[i].Utilisation * 100f:0}%", colour)}");
            }
            if (!result.Survived && result.Failed.Count > 0)
            {
                var first = Session.Blueprint.Get(result.Failed[0].MemberId);
                text.Append($"\nFirst to fail: <b>{first?.DisplayName}</b> ({result.Failed[0].FailureReason}). Tap members to inspect, then Modify & re-test.");
            }
            resultDetails.text = text.ToString();
        }

        private static string Describe(LoadCaseKind kind) => kind switch
        {
            LoadCaseKind.Seismic => "Shaking pushes the structure sideways. Bracing and strong legs resist it.",
            LoadCaseKind.Wind => "Wind pushes walls and lifts roofs and canopies.",
            LoadCaseKind.Rain => "Water must drain off the roof. Ponding water is heavy.",
            LoadCaseKind.Live => "People and furniture load floors and cantilevers.",
            _ => "Can the structure carry its own weight?"
        };

        private static bool ContainsKind(IReadOnlyList<LoadCaseKind> list, LoadCaseKind kind)
        {
            foreach (var item in list)
                if (item == kind) return true;
            return false;
        }
    }
}
