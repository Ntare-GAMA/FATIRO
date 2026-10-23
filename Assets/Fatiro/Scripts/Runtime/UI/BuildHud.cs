using System.Collections.Generic;
using Fatiro.Core;
using Fatiro.Domain;
using Fatiro.States;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>
    /// Building HUD: current stage and task, progress, last message, and the member tray whose
    /// cards are dragged onto the scene (brief 2.5 "Simulation").
    /// </summary>
    public sealed class BuildHud : UIScreen
    {
        private static readonly BuildStage[] Stages =
            { BuildStage.Foundation, BuildStage.PrimaryStructure, BuildStage.Bracing, BuildStage.Roof, BuildStage.Envelope };

        private readonly List<(BuildStage stage, Image chip, Text label)> chips = new List<(BuildStage, Image, Text)>();
        private readonly List<TrayCard> cards = new List<TrayCard>();
        private Text title;
        private Text task;
        private Text progressText;
        private RectTransform progressFill;
        private Text trayTitle;
        private RectTransform trayContent;
        private ScrollRect trayScroll;
        private Button undo;
        private Button redo;
        private Button testBench;
        private RectTransform dragLabel;
        private Text dragText;
        private MemberSpec selected;
        private bool dirty = true;

        public BuildHud(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            BuildTopBar();
            BuildTray();

            var drag = UIKit.Panel(Root, Theme.Sun, "Drag label");
            drag.raycastTarget = false;
            dragLabel = drag.rectTransform;
            dragLabel.anchorMin = dragLabel.anchorMax = Vector2.zero;
            dragLabel.sizeDelta = new Vector2(420, 90);
            dragText = UIKit.Label(dragLabel, string.Empty, Theme.Small, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            dragText.rectTransform.Stretch(12, 4, 12, 4);
            drag.gameObject.SetActive(false);

            Ui.Events.Subscribe<BuildProgressChanged>(_ => dirty = true);
            Ui.Events.Subscribe<TraySelectionChanged>(message =>
            {
                selected = message.Spec;
                dirty = true;
            });
            Ui.Events.Subscribe<PlacementRejected>(message => SetTask(UIKit.Colour("Rejected: " + message.Reason, Theme.Danger)));
            Ui.Events.Subscribe<MemberPlaced>(message => dirty = true);
            Ui.Events.Subscribe<StateChanged>(_ => dirty = true);
        }

        protected override void OnShow() => dirty = true;

        public override void Tick()
        {
            undo.interactable = Session.Build.CanUndo;
            redo.interactable = Session.Build.CanRedo;
            if (!dirty) return;
            dirty = false;
            Refresh();
        }

        private void BuildTopBar()
        {
            var card = UIKit.Card(Root, Theme.Surface, 28, 12, "Top bar");
            ((RectTransform)card.transform).Dock(TextAnchor.UpperCenter, 430, 30);
            UIKit.ImigongoBand(card.transform, 16);

            var header = UIKit.HStack(card.transform, 14);
            UIKit.Size(header.gameObject, minHeight: 110, preferredHeight: 110);
            title = UIKit.Label(header.transform, string.Empty, Theme.Subheading, style: FontStyle.Bold);
            UIKit.Size(title.gameObject, flexibleWidth: 2);
            var move = UIKit.Button(header.transform, "Move site", () => { Click(); Simulation.Reposition(); }, ButtonStyle.Ghost, 110, Theme.Small);
            UIKit.Size(move.gameObject, minWidth: 150, minHeight: 110, flexibleWidth: 1);
            var view = ViewToggle(header.transform, 110, Theme.Small);
            UIKit.Size(view.gameObject, minWidth: 150, minHeight: 110, flexibleWidth: 1);
            var pause = UIKit.Button(header.transform, "Pause", () => { Click(); Simulation.Pause(); }, ButtonStyle.Ghost, 110, Theme.Small);
            UIKit.Size(pause.gameObject, minWidth: 150, minHeight: 110, flexibleWidth: 1);

            var chipRow = UIKit.HStack(card.transform, 8);
            UIKit.Size(chipRow.gameObject, minHeight: 64, preferredHeight: 64);
            foreach (var stage in Stages)
            {
                var chip = UIKit.Panel(chipRow.transform, Theme.SurfaceRaised, stage.ToString());
                var label = UIKit.Label(chip.transform, EnumText.StageName(stage), 24, align: TextAnchor.MiddleCenter, style: FontStyle.Bold);
                label.rectTransform.Stretch(6, 2, 6, 2);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 24;
                chips.Add((stage, chip, label));
            }

            var progressRow = UIKit.HStack(card.transform, 16);
            progressRow.childForceExpandHeight = false;
            progressRow.childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(progressRow.gameObject, minHeight: 44);
            progressFill = UIKit.ProgressBar(progressRow.transform, Theme.Leaf);
            UIKit.Size(progressFill.parent.gameObject, minHeight: 22, preferredHeight: 22, flexibleWidth: 3);
            progressText = UIKit.Label(progressRow.transform, string.Empty, Theme.Small, Theme.Muted, TextAnchor.MiddleRight);
            UIKit.Size(progressText.gameObject, flexibleWidth: 1);

            task = UIKit.Label(card.transform, string.Empty, Theme.Small);
        }

        private void BuildTray()
        {
            var card = UIKit.Card(Root, Theme.Surface, 22, 10, "Tray");
            ((RectTransform)card.transform).Dock(TextAnchor.LowerCenter, 500, 24);

            trayTitle = UIKit.Label(card.transform, "Members", Theme.Small, Theme.Muted);
            trayScroll = UIKit.Scroll(card.transform, out trayContent, horizontal: true, name: "Tray scroll");
            UIKit.Size(trayScroll.gameObject, minHeight: 230, preferredHeight: 230);

            var actions = UIKit.HStack(card.transform, 12);
            UIKit.Size(actions.gameObject, minHeight: Theme.TouchTarget, preferredHeight: Theme.TouchTarget);
            undo = UIKit.Button(actions.transform, "Undo", () => { Click(); Simulation.Undo(); }, ButtonStyle.Secondary, fontSize: Theme.Small);
            redo = UIKit.Button(actions.transform, "Redo", () => { Click(); Simulation.Redo(); }, ButtonStyle.Secondary, fontSize: Theme.Small);
            testBench = UIKit.Button(actions.transform, "Test bench", () => { Click(); Simulation.OpenTestBench(); }, ButtonStyle.Primary, fontSize: Theme.Small);
            UIKit.Size(testBench.gameObject, minHeight: Theme.TouchTarget, flexibleWidth: 1.6f);
            UIKit.Button(actions.transform, "Finish", () => { Click(); Simulation.EndSession(); }, ButtonStyle.Ghost, fontSize: Theme.Small);
        }

        private void Refresh()
        {
            var tasks = Session.Tasks;
            var setup = Session.Setup;
            if (setup == null) return;

            var modeName = setup.Mode == BuildMode.GuidedBuild ? "Guided build" : setup.Mode == BuildMode.FreeBuild ? "Free build" : "Reinforcing";
            title.text = $"{setup.Design.DisplayName} <size={Theme.Small}>· {modeName} · {setup.Site}</size>";

            foreach (var (stage, chip, label) in chips)
            {
                var done = tasks.RequiredRemaining(stage) == 0;
                var currentStage = !tasks.IsComplete && stage == tasks.CurrentStage;
                chip.color = currentStage ? Theme.Sun : done ? Theme.PrimaryDark : Theme.SurfaceRaised;
                label.color = currentStage ? Theme.Ink : done ? Theme.Text : Theme.Muted;
                label.text = EnumText.StageName(stage);
            }

            UIKit.SetProgress(progressFill, tasks.Progress);
            progressText.text = $"{tasks.RequiredPlaced} / {tasks.RequiredTotal} required";
            testBench.interactable = tasks.IsComplete || setup.Mode != BuildMode.GuidedBuild;
            testBench.SetStyle(tasks.IsComplete ? ButtonStyle.Primary : ButtonStyle.Secondary);

            if (tasks.IsComplete)
                SetTask(UIKit.Colour("Structure complete. ", Theme.Success) + "Add optional reinforcements, or open the test bench.");
            else if (selected != null)
                SetTask($"Next: <b>{selected.DisplayName}</b> — drag its card onto the glowing outline, or tap the outline.");
            else
                SetTask($"Stage: <b>{EnumText.StageName(tasks.CurrentStage)}</b> — pick a member from the tray.");

            RebuildTray();
        }

        private void RebuildTray()
        {
            UIKit.ClearChildren(trayContent);
            cards.Clear();
            var items = Session.Tasks.TrayItems();
            trayTitle.text = items.Count == 0 ? "All members placed" : $"Members to place ({items.Count}) — drag up onto the scene";

            foreach (var spec in items)
            {
                var isSelected = selected != null && selected.Id == spec.Id;
                var card = UIKit.Card(trayContent, isSelected ? Theme.SurfaceSelected : Theme.SurfaceRaised, 18, 4, spec.Id);
                UIKit.Size(card.gameObject, minWidth: 300, preferredWidth: 300, minHeight: 210);
                card.GetComponent<Image>().raycastTarget = true;
                card.childAlignment = TextAnchor.UpperLeft;

                UIKit.Label(card.transform, spec.DisplayName, 28, isSelected ? Theme.Primary : Theme.Ink, style: FontStyle.Bold);
                UIKit.Label(card.transform, $"{EnumText.PieceName(spec)} · {EnumText.StageName(spec.Stage)}", 22, Theme.Muted);
                var check = Session.Tasks.CanPlace(spec);
                var note = !spec.Required ? UIKit.Colour("Optional reinforcement", Theme.Teal)
                    : check.Allowed ? UIKit.Colour("Ready", Theme.Success)
                    : UIKit.Colour(check.MissingPrerequisite != null ? $"Needs {check.MissingPrerequisite.DisplayName}" : "Later stage", Theme.Warning);
                UIKit.Label(card.transform, note, 22);

                var trayCard = card.gameObject.AddComponent<TrayCard>();
                trayCard.Bind(spec, trayScroll);
                trayCard.Clicked += member => { Click(); Simulation.SelectTrayMember(member); };
                trayCard.DragStarted += (member, position) =>
                {
                    Simulation.BeginMemberDrag(member);
                    dragText.text = member.DisplayName;
                    dragLabel.gameObject.SetActive(true);
                    MoveDragLabel(position);
                };
                trayCard.Dragged += position =>
                {
                    MoveDragLabel(position);
                    Simulation.DragMember(position);
                };
                trayCard.DragEnded += position =>
                {
                    dragLabel.gameObject.SetActive(false);
                    Simulation.EndMemberDrag(position);
                };
                cards.Add(trayCard);
            }
        }

        private void MoveDragLabel(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screenPosition, null, out var local);
            dragLabel.anchorMin = dragLabel.anchorMax = new Vector2(0.5f, 0.5f);
            dragLabel.anchoredPosition = local + new Vector2(0, 140);
        }

        private void SetTask(string text) => task.text = text;
    }

    /// <summary>Tap a member to inspect it (FR-20) and change its material (FR-12).</summary>
    public sealed class InspectorPanel : UIScreen
    {
        private RectTransform card;
        private Text heading;
        private Text details;
        private RectTransform materials;
        private Button remove;
        private Text editNote;
        private string memberId;

        public InspectorPanel(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            var layout = UIKit.Card(Root, Theme.Surface, 32, 12, "Inspector");
            card = (RectTransform)layout.transform;
            card.anchorMin = new Vector2(0.04f, 0.5f);
            card.anchorMax = new Vector2(0.96f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(0, 40);
            UIKit.FitHeight(card.gameObject);

            heading = UIKit.Display(card, string.Empty, Theme.Heading, Theme.Primary);
            details = UIKit.Label(card, string.Empty, Theme.Small);
            UIKit.Label(card, "Material", Theme.Small, Theme.Muted, style: FontStyle.Bold);
            materials = UIKit.HStack(card, 10, name: "Materials").GetComponent<RectTransform>();
            UIKit.Size(materials.gameObject, minHeight: 112, preferredHeight: 112);
            editNote = UIKit.Label(card, "Tap “Modify & re-test” to change the structure.", 24, Theme.Muted, style: FontStyle.Italic);

            var actions = UIKit.HStack(card, 14);
            UIKit.Size(actions.gameObject, minHeight: 112, preferredHeight: 112);
            remove = UIKit.Button(actions.transform, "Remove", () => { Click(); Simulation.RemoveMember(memberId); }, ButtonStyle.Danger, 112, Theme.Small);
            UIKit.Button(actions.transform, "Close", () => { Click(); Simulation.SelectMember(null); }, ButtonStyle.Secondary, 112, Theme.Small);

            Ui.Events.Subscribe<MemberSelected>(message =>
            {
                memberId = message.MemberId;
                if (memberId == null) SetVisible(false);
                else
                {
                    SetVisible(true);
                    Refresh();
                }
            });
            Ui.Events.Subscribe<MaterialChanged>(message =>
            {
                if (message.MemberId == memberId) Refresh();
            });
        }

        private void Refresh()
        {
            var member = Session.Entities.Member(memberId);
            if (member == null)
            {
                SetVisible(false);
                return;
            }

            var spec = member.Spec;
            heading.text = spec.DisplayName;
            var lines = $"{EnumText.PieceName(spec)} · {EnumText.StageName(spec.Stage)}{(spec.Required ? string.Empty : " · optional")}\n" +
                        $"Resists load by {member.ResistanceMode}. Length {member.Geometry.Length:0.0} m.";
            if (Session.LoadTests.LastResult != null)
            {
                var result = Session.LoadTests.LastResult.For(spec.Id);
                if (result != null)
                {
                    var colour = World.MemberView.UtilisationColor(result.Utilisation);
                    lines += $"\nLast test: load {result.VerticalDemand:0.0} kN vertical, {result.LateralDemand:0.0} kN sideways\n" +
                             $"Utilisation {UIKit.Colour($"{result.Utilisation * 100f:0}%", colour)}" +
                             (result.Failed ? UIKit.Colour($" — FAILED ({result.FailureReason})", Theme.Danger) : string.Empty);
                }
            }
            if (!string.IsNullOrEmpty(spec.Hint)) lines += $"\n<i>{spec.Hint}</i>";
            details.text = lines;

            var editable = Simulation.CanEditStructure;
            editNote.gameObject.SetActive(!editable);
            remove.interactable = editable;

            UIKit.ClearChildren(materials);
            foreach (var id in spec.AllowedMaterialIds)
            {
                var data = Session.Entities.Material(id);
                if (data == null) continue;
                var current = member.Material.Id == id;
                var target = id;
                var button = UIKit.Button(materials, data.DisplayName.Replace("Reinforced concrete", "Concrete").Replace("Compressed-earth block", "Earth block"),
                    () => { Click(); Simulation.ChangeMaterial(memberId, target); }, current ? ButtonStyle.Primary : ButtonStyle.Secondary, 112, 24);
                button.interactable = editable || current;
                var swatch = UIKit.Panel(button.transform, data.Color, "Swatch");
                swatch.raycastTarget = false;
                var rect = swatch.rectTransform;
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(1, 0);
                rect.sizeDelta = new Vector2(-30, 10);
                rect.anchoredPosition = new Vector2(0, 12);
            }
        }
    }
}
