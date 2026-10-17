using System.Collections.Generic;
using Fatiro.Content;
using Fatiro.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>Design catalogue and site conditions (FR-06 to FR-08).</summary>
    public sealed class SetupScreen : UIScreen
    {
        private readonly List<(HouseDesign design, Image card)> designCards = new List<(HouseDesign, Image)>();
        private readonly List<(int value, Button button)> slopeButtons = new List<(int, Button)>();
        private readonly List<(SoilType value, Button button)> soilButtons = new List<(SoilType, Button)>();
        private readonly List<(BuildMode value, Button button)> modeButtons = new List<(BuildMode, Button)>();
        private RectTransform designList;
        private Text modeHint;
        private Button beginButton;

        private HouseDesign design;
        private int slope = 20;
        private SoilType soil = SoilType.Laterite;
        private BuildMode mode = BuildMode.GuidedBuild;

        public SetupScreen(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            UIKit.Panel(Root, Theme.Backdrop, "Backdrop", false).rectTransform.Stretch();

            var header = UIKit.VStack(Root, 6);
            ((RectTransform)header.transform).Dock(TextAnchor.UpperCenter, 190, 50);
            UIKit.Label(header.transform, "Set up the site", Theme.Heading, Theme.Accent, style: FontStyle.Bold);
            UIKit.Label(header.transform, "Choose a house, the hillside it stands on, and how you want to build.", Theme.Small, Theme.Muted);

            var scroll = UIKit.Scroll(Root, out var content);
            ((RectTransform)scroll.transform).Stretch(50, 250, 50, 230);
            content.GetComponent<VerticalLayoutGroup>().spacing = 26;

            Section(content, "1  House design");
            designList = UIKit.VStack(content, 20, name: "Designs").GetComponent<RectTransform>();

            Section(content, "2  Slope of the site");
            var slopes = UIKit.HStack(content, 14);
            UIKit.Size(slopes.gameObject, minHeight: Theme.TouchTarget);
            foreach (var value in SiteConditions.AllowedSlopes)
            {
                var captured = value;
                slopeButtons.Add((value, UIKit.Button(slopes.transform, $"{value}°", () => { Click(); slope = captured; Refresh(); }, ButtonStyle.Secondary)));
            }

            Section(content, "3  Soil");
            var soils = UIKit.HStack(content, 14);
            UIKit.Size(soils.gameObject, minHeight: Theme.TouchTarget);
            foreach (SoilType value in System.Enum.GetValues(typeof(SoilType)))
            {
                var captured = value;
                soilButtons.Add((value, UIKit.Button(soils.transform, value.ToString(), () => { Click(); soil = captured; Refresh(); }, ButtonStyle.Secondary)));
            }
            UIKit.Label(content, "Rock carries the most load; clay is weak and amplifies earthquakes.", Theme.Small, Theme.Muted);

            Section(content, "4  Mode");
            var modes = UIKit.HStack(content, 14);
            UIKit.Size(modes.gameObject, minHeight: Theme.TouchTarget);
            AddMode(modes.transform, BuildMode.GuidedBuild, "Guided build");
            AddMode(modes.transform, BuildMode.FreeBuild, "Free build");
            AddMode(modes.transform, BuildMode.LoadTest, "Load test");
            modeHint = UIKit.Label(content, string.Empty, Theme.Small, Theme.Muted);

            var footer = UIKit.HStack(Root, 20);
            ((RectTransform)footer.transform).Dock(TextAnchor.LowerCenter, 140, 50);
            UIKit.Button(footer.transform, "Back", () => { Click(); Simulation.BackToMenu(); }, ButtonStyle.Secondary);
            beginButton = UIKit.Button(footer.transform, "Place on table", Begin);
            UIKit.Size(beginButton.gameObject, minHeight: Theme.TouchTarget, flexibleWidth: 2);
        }

        protected override void OnShow()
        {
            if (designCards.Count == 0) BuildDesignCards();
            if (design == null && Session.Designs.Count > 0) design = Session.Designs[0];
            Refresh();
        }

        private void BuildDesignCards()
        {
            foreach (var item in Session.Designs)
            {
                var target = item;
                var card = UIKit.Card(designList, Theme.Surface, 30, 10, item.DesignId);
                var image = card.GetComponent<Image>();
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => { Click(); design = target; Refresh(); });

                var title = UIKit.Label(card.transform,
                    $"{UIKit.Colour("■", item.AccentColor)}  <b>{item.DisplayName}</b>  <size={Theme.Small}>({item.Meaning})</size>",
                    Theme.Subheading);
                title.supportRichText = true;
                UIKit.Label(card.transform, item.Description, Theme.Small);
                UIKit.Label(card.transform, $"Lesson: {item.Lesson}", Theme.Small, Theme.Muted, style: FontStyle.Italic);
                UIKit.Label(card.transform,
                    $"Difficulty {new string('●', item.Difficulty)}{new string('○', 3 - item.Difficulty)}   ·   {item.MemberCount} members   ·   Foundation: {item.Blueprint.Foundation.Name}",
                    Theme.Small, Theme.Accent);
                designCards.Add((item, image));
            }
        }

        private void Refresh()
        {
            foreach (var (item, card) in designCards)
            {
                var selected = item == design;
                card.color = selected ? Theme.SurfaceSelected : Theme.Surface;
            }
            foreach (var (value, button) in slopeButtons) button.SetStyle(value == slope ? ButtonStyle.Primary : ButtonStyle.Secondary);
            foreach (var (value, button) in soilButtons) button.SetStyle(value == soil ? ButtonStyle.Primary : ButtonStyle.Secondary);
            foreach (var (value, button) in modeButtons) button.SetStyle(value == mode ? ButtonStyle.Primary : ButtonStyle.Secondary);

            modeHint.text = mode switch
            {
                BuildMode.GuidedBuild => "Stages unlock in order: foundation, primary structure, bracing, roof, envelope. The next member glows.",
                BuildMode.FreeBuild => "Any order you like, but members still need their supports first. No glowing hints.",
                _ => "Start with the finished structure and go straight to load testing and reinforcing."
            };
            beginButton.interactable = design != null && !Simulation.IsBusy;
        }

        private async void Begin()
        {
            if (design == null) return;
            Click();
            await Simulation.ConfirmSetup(design, slope, soil, mode);
        }

        private void AddMode(Transform parent, BuildMode value, string label)
        {
            modeButtons.Add((value, UIKit.Button(parent, label, () => { Click(); mode = value; Refresh(); }, ButtonStyle.Secondary, fontSize: Theme.Small)));
        }

        private static void Section(Transform parent, string title) =>
            UIKit.Label(parent, title, Theme.Subheading, Theme.Accent, style: FontStyle.Bold);
    }
}
