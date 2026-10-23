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

            var header = UIKit.VStack(Root, 4);
            ((RectTransform)header.transform).Dock(TextAnchor.UpperCenter, 210, 44);
            UIKit.Label(header.transform, "TEGURA  ·  PREPARE", 26, Theme.Accent, style: FontStyle.Bold);
            UIKit.Display(header.transform, "Choose your hillside", Theme.Heading + 10);
            UIKit.Label(header.transform, "A house, the ground it stands on, and how you want to build it.", Theme.Small, Theme.Muted);
            var band = UIKit.ImigongoBand(Root, 22);
            ((RectTransform)band.transform).Dock(TextAnchor.UpperCenter, 22, 0);
            band.rectTransform.anchoredPosition = new Vector2(0, -262);

            var scroll = UIKit.Scroll(Root, out var content);
            ((RectTransform)scroll.transform).Stretch(44, 300, 44, 205);
            content.GetComponent<VerticalLayoutGroup>().spacing = 26;

            Section(content, "Inzu", "House");
            designList = UIKit.VStack(content, 20, name: "Designs").GetComponent<RectTransform>();

            Section(content, "Umusozi", "Slope of the hill");
            var slopes = UIKit.HStack(content, 14);
            UIKit.Size(slopes.gameObject, minHeight: Theme.TouchTarget);
            foreach (var value in SiteConditions.AllowedSlopes)
            {
                var captured = value;
                slopeButtons.Add((value, UIKit.Button(slopes.transform, $"{value}°", () => { Click(); slope = captured; Refresh(); }, ButtonStyle.Secondary)));
            }

            Section(content, "Ubutaka", "Soil");
            var soils = UIKit.HStack(content, 14);
            UIKit.Size(soils.gameObject, minHeight: Theme.TouchTarget);
            foreach (SoilType value in System.Enum.GetValues(typeof(SoilType)))
            {
                var captured = value;
                soilButtons.Add((value, UIKit.Button(soils.transform, value.ToString(), () => { Click(); soil = captured; Refresh(); }, ButtonStyle.Secondary)));
            }
            UIKit.Label(content, "Rock carries the most load; clay is weak and amplifies earthquakes.", Theme.Small, Theme.Muted);

            Section(content, "Uburyo", "How you build");
            var modes = UIKit.HStack(content, 14);
            UIKit.Size(modes.gameObject, minHeight: Theme.TouchTarget);
            AddMode(modes.transform, BuildMode.GuidedBuild, "Guided build");
            AddMode(modes.transform, BuildMode.FreeBuild, "Free build");
            AddMode(modes.transform, BuildMode.LoadTest, "Load test");
            modeHint = UIKit.Label(content, string.Empty, Theme.Small, Theme.Muted);

            var footer = UIKit.HStack(UIKit.Footer(Root, 200), 20);
            ((RectTransform)footer.transform).Stretch();
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
                var background = UIKit.Panel(designList, Theme.Surface, item.DesignId);
                var button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => { Click(); design = target; Refresh(); });
                var row = background.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(0, 30, 0, 0);
                row.spacing = 28;
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = true;

                // Imigongo diamonds down the card edge, tinted with the design's colour when chosen.
                var edge = UIKit.Rect("Imigongo edge", background.transform).gameObject.AddComponent<Image>();
                edge.sprite = RwandaArt.ImigongoDiamonds;
                edge.type = Image.Type.Tiled;
                edge.pixelsPerUnitMultiplier = 48f / 44f;
                edge.raycastTarget = false;
                UIKit.Size(edge.gameObject, minWidth: 44, preferredWidth: 44);

                var text = UIKit.VStack(background.transform, 8, name: "Text");
                text.padding = new RectOffset(0, 0, 28, 28);
                UIKit.Size(text.gameObject, flexibleWidth: 1);
                var heading = UIKit.HStack(text.transform, 18);
                heading.childForceExpandWidth = false;
                heading.childAlignment = TextAnchor.LowerLeft;
                var name = UIKit.Display(heading.transform, item.DisplayName, 64, Theme.Primary);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Size(name.gameObject, minWidth: name.preferredWidth + 12, preferredWidth: name.preferredWidth + 12);
                var meaning = UIKit.Label(heading.transform, $"“{item.Meaning}”", Theme.Body, Theme.Accent, style: FontStyle.Italic);
                UIKit.Size(meaning.gameObject, preferredWidth: meaning.preferredWidth + 6);

                UIKit.Label(text.transform, item.Description, Theme.Small);
                UIKit.Label(text.transform, item.Lesson, Theme.Small, Theme.Muted, style: FontStyle.Italic);
                var difficulty = item.Difficulty <= 1 ? "Gentle" : item.Difficulty == 2 ? "Steady" : "Demanding";
                UIKit.Label(text.transform, $"{difficulty}  ·  {item.MemberCount} members  ·  {item.Blueprint.Foundation.Name}",
                    24, Theme.Primary, style: FontStyle.Bold);
                designCards.Add((item, background));
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

        /// <summary>Section heading: the Kinyarwanda word in the display serif, English beside it.</summary>
        private static void Section(Transform parent, string kinyarwanda, string english)
        {
            var row = UIKit.HStack(parent, 18, name: kinyarwanda);
            row.childForceExpandWidth = false;
            row.childAlignment = TextAnchor.LowerLeft;
            UIKit.Size(row.gameObject, minHeight: 80);
            var word = UIKit.Display(row.transform, kinyarwanda, 54, Theme.Accent);
            UIKit.Size(word.gameObject, preferredWidth: word.preferredWidth + 6);
            var translation = UIKit.Label(row.transform, english, Theme.Body, Theme.Muted);
            UIKit.Size(translation.gameObject, preferredWidth: translation.preferredWidth + 6);
        }
    }
}
