using System;
using System.Collections.Generic;
using Fatiro.Core;
using Fatiro.Data;
using Fatiro.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>Completion: outcome, score breakdown and performance metrics, with restart, menu and export (FR-22, FR-25).</summary>
    public sealed class CompletionScreen : UIScreen
    {
        private Text outcome;
        private Text outcomeEnglish;
        private Text total;
        private Text summary;
        private readonly List<(Text label, RectTransform fill)> bars = new List<(Text, RectTransform)>();
        private Text metrics;
        private SessionRecord record;
        private ScoreBreakdown score;

        public CompletionScreen(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            UIKit.Panel(Root, Theme.Backdrop, "Backdrop", false).rectTransform.Stretch();
            var scroll = UIKit.Scroll(Root, out var page);
            ((RectTransform)scroll.transform).Stretch(0, 0, 0, 350);
            page.GetComponent<VerticalLayoutGroup>().spacing = 0;
            var banner = UIKit.Rect("Hills", page).gameObject.AddComponent<Image>();
            banner.sprite = RwandaArt.Hills;
            banner.raycastTarget = false;
            UIKit.Size(banner.gameObject, minHeight: 360, preferredHeight: 360);
            UIKit.ImigongoBand(page, 40);
            var content = UIKit.VStack(page, 24, 56, "Content").transform;

            outcome = UIKit.Display(content, string.Empty, Theme.Title + 10, Theme.Primary, TextAnchor.MiddleCenter);
            outcomeEnglish = UIKit.Label(content, string.Empty, Theme.Subheading, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            total = UIKit.Display(content, string.Empty, Theme.Title + 70, Theme.Primary, TextAnchor.MiddleCenter);
            summary = UIKit.Label(content, string.Empty, Theme.Body, Theme.Muted, TextAnchor.MiddleCenter);

            var breakdown = UIKit.Card(content, Theme.Surface, 36, 14);
            UIKit.Display(breakdown.transform, "Score", Theme.Heading, Theme.Accent);
            for (var i = 0; i < 3; i++)
            {
                var label = UIKit.Label(breakdown.transform, string.Empty, Theme.Body);
                var fill = UIKit.ProgressBar(breakdown.transform, i == 0 ? Theme.Teal : i == 1 ? Theme.Primary : Theme.Sun, 26);
                bars.Add((label, fill));
            }

            var details = UIKit.Card(content, Theme.Surface, 36, 10);
            UIKit.Display(details.transform, "How you did", Theme.Heading, Theme.Accent);
            metrics = UIKit.Label(details.transform, string.Empty, Theme.Body);

            var footer = UIKit.VStack(UIKit.Footer(Root, 350), 16);
            ((RectTransform)footer.transform).Stretch();
            var top = UIKit.HStack(footer.transform, 16);
            UIKit.Size(top.gameObject, minHeight: Theme.TouchTarget, preferredHeight: Theme.TouchTarget);
            UIKit.Button(top.transform, "Restart", () => { Click(); Simulation.Restart(); });
            UIKit.Button(top.transform, "Main menu", () => { Click(); Simulation.ReturnToMenu(); }, ButtonStyle.Secondary);
            var bottom = UIKit.HStack(footer.transform, 16);
            UIKit.Size(bottom.gameObject, minHeight: Theme.TouchTarget, preferredHeight: Theme.TouchTarget);
            UIKit.Button(bottom.transform, "Export JSON", () => Export(ExportFormat.Json), ButtonStyle.Ghost, fontSize: Theme.Small);
            UIKit.Button(bottom.transform, "Export CSV", () => Export(ExportFormat.Csv), ButtonStyle.Ghost, fontSize: Theme.Small);

            Ui.Events.Subscribe<SessionEnded>(message =>
            {
                record = message.Record;
                score = message.Score;
            });
        }

        protected override void OnShow()
        {
            if (record == null || score == null) return;
            outcome.text = score.Passed ? "Byagenze neza!" : "Ongera ugerageze";
            outcome.color = score.Passed ? Theme.Primary : Theme.Accent;
            outcomeEnglish.text = score.Passed ? "PASSED  ·  IT WENT WELL" : "NOT YET  ·  TRY AGAIN";
            total.text = $"{score.Total}<size={Theme.Heading}> / 100</size>";
            summary.text = $"{record.design.designName} on a {record.site.slopeDeg}° {record.site.soil} slope · {Mode(record.site.mode)}\nPass mark {score.PassScore}";

            SetBar(0, "Assembly accuracy", score.Assembly, 40);
            SetBar(1, "Structural safety", score.Safety, 40);
            SetBar(2, "Material cost efficiency", score.Cost, 20);

            var rejected = record.PlacementAttempts - record.AcceptedPlacements;
            var accuracy = record.PlacementAttempts == 0 ? 100f : 100f * record.AcceptedPlacements / record.PlacementAttempts;
            var worst = 0f;
            var collapses = 0;
            foreach (var test in record.loadTests)
            {
                worst = Mathf.Max(worst, test.peakUtilisation);
                if (test.failedMembers.Count > 0) collapses++;
            }

            var duration = TimeSpan.FromSeconds(record.durationSeconds);
            metrics.text =
                $"Placements accepted first time: <b>{accuracy:0}%</b> ({record.AcceptedPlacements} of {record.PlacementAttempts})\n" +
                $"Sequence mistakes caught: <b>{rejected}</b>\n" +
                $"Load tests run: <b>{record.loadTests.Count}</b>, with collapses in <b>{collapses}</b>\n" +
                $"Worst peak utilisation: <b>{worst * 100f:0}%</b>\n" +
                $"Members in final structure: <b>{Session.Entities.Count}</b>\n" +
                $"Time: <b>{(int)duration.TotalMinutes}:{duration.Seconds:00}</b>\n" +
                UIKit.Colour("Saved to this device. See it again under Session history.", Theme.Muted);
        }

        private void SetBar(int index, string name, int value, int maximum)
        {
            var (label, fill) = bars[index];
            label.text = $"{name}  <b>{value}</b> / {maximum}";
            UIKit.SetProgress(fill, (float)value / maximum);
        }

        private void Export(ExportFormat format)
        {
            Click();
            if (record == null) return;
            try
            {
                ReportExporter.Export(record, format);
                Ui.Events.Publish(new ToastRequested($"Report ready to share ({format.ToString().ToUpperInvariant()}).", ToastKind.Success));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Ui.Events.Publish(new ToastRequested("Export failed.", ToastKind.Error));
            }
        }

        private static string Mode(string mode) => mode == nameof(BuildMode.GuidedBuild) ? "Guided build" : mode == nameof(BuildMode.FreeBuild) ? "Free build" : "Load test";
    }

    /// <summary>Past sessions for the active learner, newest first (FR-24; brief 2.7 asks for at least five).</summary>
    public sealed class HistoryScreen : UIScreen
    {
        private readonly Action close;
        private RectTransform list;
        private Text header;
        private GameObject detailPanel;
        private Text detailText;
        private SessionRecord detailRecord;

        public HistoryScreen(UIContext context, Action onClose) : base(context)
        {
            close = onClose;
        }

        protected override void OnBuild()
        {
            UIKit.Panel(Root, Theme.Backdrop, "Backdrop", false).rectTransform.Stretch();
            var top = UIKit.VStack(Root, 6);
            ((RectTransform)top.transform).Dock(TextAnchor.UpperCenter, 170, 50);
            UIKit.Label(top.transform, "AMATEKA  ·  HISTORY", 26, Theme.Accent, style: FontStyle.Bold);
            UIKit.Display(top.transform, "Past sessions", Theme.Heading + 10);
            header = UIKit.Label(top.transform, string.Empty, Theme.Small, Theme.Muted);

            var scroll = UIKit.Scroll(Root, out list);
            ((RectTransform)scroll.transform).Stretch(50, 260, 50, 210);

            var footer = UIKit.HStack(UIKit.Footer(Root, 200), 16);
            ((RectTransform)footer.transform).Stretch();
            UIKit.Button(footer.transform, "Back", () => { Click(); close(); }, ButtonStyle.Secondary);

            var scrim = UIKit.Panel(Root, Theme.Scrim, "Detail", false);
            scrim.rectTransform.Stretch();
            detailPanel = scrim.gameObject;
            var card = UIKit.Card(scrim.transform, Theme.Surface, 40, 16);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = new Vector2(0.05f, 0.1f);
            rect.anchorMax = new Vector2(0.95f, 0.9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var detailScroll = UIKit.Scroll(card.transform, out var detailContent);
            UIKit.Size(detailScroll.gameObject, flexibleHeight: 1);
            detailText = UIKit.Label(detailContent, string.Empty, Theme.Small);
            var actions = UIKit.HStack(card.transform, 14);
            UIKit.Size(actions.gameObject, minHeight: Theme.TouchTarget, preferredHeight: Theme.TouchTarget);
            UIKit.Button(actions.transform, "Export CSV", () => ExportDetail(ExportFormat.Csv), ButtonStyle.Ghost, fontSize: Theme.Small);
            UIKit.Button(actions.transform, "Export JSON", () => ExportDetail(ExportFormat.Json), ButtonStyle.Ghost, fontSize: Theme.Small);
            UIKit.Button(actions.transform, "Close", () => { Click(); detailPanel.SetActive(false); }, ButtonStyle.Secondary);
            detailPanel.SetActive(false);
        }

        protected override void OnShow()
        {
            detailPanel.SetActive(false);
            UIKit.ClearChildren(list);
            var profile = Session.Data.ActiveProfile;
            if (profile == null)
            {
                header.text = "Choose a learner on the start screen to see their sessions.";
                return;
            }

            var sessions = Session.Data.Sessions.ListFor(profile.learnerId);
            header.text = $"{profile.displayName} · {sessions.Count} saved session{(sessions.Count == 1 ? string.Empty : "s")} (newest first, last 50 kept)";
            if (sessions.Count == 0)
                UIKit.Label(list, "No finished sessions yet. Complete a session and it will appear here.", Theme.Body, Theme.Muted);

            foreach (var summary in sessions)
            {
                var target = summary;
                var card = UIKit.Card(list, Theme.Surface, 28, 6, summary.SessionId);
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card.GetComponent<Image>();
                button.onClick.AddListener(() => { Click(); OpenDetail(target); });
                var verdict = summary.Passed ? UIKit.Colour("PASS", Theme.Success) : UIKit.Colour("NOT YET", Theme.Warning);
                UIKit.Label(card.transform, $"<b>{summary.DesignName}</b>   {verdict}   <b>{summary.Score}</b>/100", Theme.Body);
                UIKit.Label(card.transform, $"{summary.StartedAtUtc.ToLocalTime():ddd d MMM yyyy, HH:mm} · {summary.Site} · {summary.Mode} · {summary.LoadTests} tests",
                    Theme.Small, Theme.Muted);
            }
        }

        private void OpenDetail(SessionSummary summary)
        {
            detailRecord = Session.Data.Sessions.Load(summary.LearnerId, summary.SessionId);
            if (detailRecord == null) return;
            var text = new System.Text.StringBuilder();
            text.AppendLine($"<size={Theme.Subheading}><b>{detailRecord.design.designName}</b></size> (v{detailRecord.design.designVersion})");
            text.AppendLine($"{detailRecord.site.slopeDeg}° {detailRecord.site.soil} · {detailRecord.site.mode}");
            text.AppendLine($"Score {detailRecord.score?.total}/100 (assembly {detailRecord.score?.assembly}, safety {detailRecord.score?.safety}, cost {detailRecord.score?.cost})");
            text.AppendLine($"Placements: {detailRecord.AcceptedPlacements} accepted, {detailRecord.PlacementAttempts - detailRecord.AcceptedPlacements} rejected");
            text.AppendLine();
            text.AppendLine("<b>Load tests</b>");
            foreach (var test in detailRecord.loadTests)
                text.AppendLine($"· {test.@case} {test.magnitude:0.#}: peak {test.peakUtilisation * 100f:0}%, {test.failedMembers.Count} failed");
            text.AppendLine();
            text.AppendLine("<b>Rejected placements</b>");
            foreach (var placement in detailRecord.placements)
                if (placement.result == "rejected") text.AppendLine($"· {placement.memberName}: {placement.reason}");
            detailText.text = text.ToString();
            detailPanel.SetActive(true);
        }

        private void ExportDetail(ExportFormat format)
        {
            Click();
            if (detailRecord == null) return;
            ReportExporter.Export(detailRecord, format);
            Ui.Events.Publish(new ToastRequested("Report ready to share.", ToastKind.Success));
        }
    }
}
