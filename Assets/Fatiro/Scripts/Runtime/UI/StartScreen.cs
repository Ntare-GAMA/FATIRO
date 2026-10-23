using System;
using Fatiro.Core;
using Fatiro.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>
    /// Start screen: project title, instructions, learner profile (FR-28), start, resume,
    /// history and settings.
    /// </summary>
    public sealed class StartScreen : UIScreen
    {
        private readonly Action openHistory;
        private Text profileLabel;
        private Button resumeButton;
        private Button soundButton;
        private Button debugButton;
        private GameObject profilePanel;
        private RectTransform profileList;
        private InputField nameInput;

        public StartScreen(UIContext context, Action showHistory) : base(context)
        {
            openHistory = showHistory;
        }

        public event Action DebugToggled;

        protected override void OnBuild()
        {
            UIKit.Panel(Root, Theme.Backdrop, "Backdrop", false).rectTransform.Stretch();
            var scroll = UIKit.Scroll(Root, out var page);
            ((RectTransform)scroll.transform).Stretch();
            page.GetComponent<VerticalLayoutGroup>().spacing = 0;

            // A landscape of a thousand hills, edged with an Imigongo band.
            var banner = UIKit.Rect("Thousand hills", page).gameObject.AddComponent<Image>();
            banner.sprite = RwandaArt.Hills;
            banner.raycastTarget = false;
            UIKit.Size(banner.gameObject, minHeight: 560, preferredHeight: 560);
            UIKit.ImigongoBand(page, 54);

            var content = UIKit.VStack(page, 30, 60, "Content").transform;

            UIKit.Label(content, "MURAKAZA NEZA  //  WELCOME", 26, Theme.Accent, style: FontStyle.Bold);
            var title = UIKit.Display(content, $"FATIRO {UIKit.Colour("AR", Theme.Primary)}", 150, Theme.Text);
            UIKit.Size(title.gameObject, minHeight: 150);
            UIKit.Label(content, "ARCHITECTURAL SIMULATION  //  KIGALI, RWANDA", 28, Theme.Muted, style: FontStyle.Bold);
            UIKit.Label(content, "Experimental houses, tested on a thousand hills.", Theme.Subheading);
            UIKit.Label(content, "From <i>urufatiro</i>, Kinyarwanda for “foundation”. Build boldly, fail safely on screen, and learn why.",
                Theme.Body, Theme.Muted);

            var steps = UIKit.VStack(content, 22, name: "Steps");
            AddStep(steps.transform, "1", "Find a table", "Scan it with your phone and set down a sloped Rwandan hillside.");
            AddStep(steps.transform, "2", "Build in order", "Raise a panther, an elephant or a car, member by member.");
            AddStep(steps.transform, "3", "Shake it", "Earthquakes, wind, heavy rain and people push on every member.");
            AddStep(steps.transform, "4", "Reinforce", "See what failed, brace it or change its material, and test again.");

            var learner = UIKit.Card(content, Theme.Surface, 32, 14);
            var row = UIKit.HStack(learner.transform, 20);
            UIKit.Size(row.gameObject, minHeight: Theme.TouchTarget);
            profileLabel = UIKit.Label(row.transform, string.Empty, Theme.Body);
            UIKit.Size(profileLabel.gameObject, flexibleWidth: 3);
            var change = UIKit.Button(row.transform, "Learner", () => { Click(); ShowProfiles(true); }, ButtonStyle.Secondary);
            UIKit.Size(change.gameObject, minHeight: Theme.TouchTarget, flexibleWidth: 1);

            UIKit.Button(content, "Start new session", () => { Click(); Simulation.StartNewSession(); }, height: 150, fontSize: Theme.Subheading);
            resumeButton = UIKit.Button(content, "Resume unfinished session", async () => { Click(); await Simulation.ResumeAutosave(); }, ButtonStyle.Secondary);
            UIKit.Button(content, "Session history", () => { Click(); openHistory(); }, ButtonStyle.Secondary);

            var settings = UIKit.HStack(content, 20);
            UIKit.Size(settings.gameObject, minHeight: Theme.TouchTarget);
            soundButton = UIKit.Button(settings.transform, string.Empty, ToggleSound, ButtonStyle.Ghost);
            debugButton = UIKit.Button(settings.transform, string.Empty, ToggleDebug, ButtonStyle.Ghost);

            UIKit.Label(content, "An educational simulator. Its structural results are simplified and are not for real construction decisions.",
                24, Theme.Muted, style: FontStyle.Italic);
            UIKit.ImigongoBand(page, 30);

            BuildProfilePanel();
        }

        private static void AddStep(Transform parent, string number, string title, string text)
        {
            var row = UIKit.HStack(parent, 26, name: "Step " + number);
            row.childForceExpandWidth = false;
            row.childAlignment = TextAnchor.UpperLeft;
            var numeral = UIKit.Display(row.transform, number, 96, Theme.Accent, TextAnchor.UpperCenter);
            UIKit.Size(numeral.gameObject, minWidth: 80, preferredWidth: 80);
            var words = UIKit.VStack(row.transform, 4);
            UIKit.Size(words.gameObject, flexibleWidth: 1);
            UIKit.Label(words.transform, title, Theme.Subheading, Theme.Primary, style: FontStyle.Bold);
            UIKit.Label(words.transform, text, Theme.Body, Theme.Muted);
        }

        protected override void OnShow()
        {
            RefreshProfile();
            resumeButton.gameObject.SetActive(Session.Data.HasAutosave && Session.Data.LoadAutosave() != null);
            RefreshSettings();
            if (Session.Data.ActiveProfile == null) ShowProfiles(true);
        }

        private void RefreshProfile()
        {
            var profile = Session.Data.ActiveProfile;
            profileLabel.text = profile == null
                ? UIKit.Colour("No learner selected", Theme.Warning)
                : $"Learner: <b>{profile.displayName}</b>";
        }

        private void RefreshSettings()
        {
            var settings = Session.Data.Settings;
            soundButton.SetLabel(settings.Muted ? "Sound: off" : "Sound: on");
            debugButton.SetLabel(settings.ShowDebugOverlay ? "Debug overlay: on" : "Debug overlay: off");
        }

        private void ToggleSound()
        {
            var settings = Session.Data.Settings;
            settings.Muted = !settings.Muted;
            Ui.Events.Publish(new SettingsChanged());
            Click();
            RefreshSettings();
        }

        private void ToggleDebug()
        {
            Click();
            var settings = Session.Data.Settings;
            settings.ShowDebugOverlay = !settings.ShowDebugOverlay;
            DebugToggled?.Invoke();
            RefreshSettings();
        }

        private void BuildProfilePanel()
        {
            var scrim = UIKit.Panel(Root, Theme.Scrim, "Profiles", false);
            scrim.rectTransform.Stretch();
            profilePanel = scrim.gameObject;

            var card = UIKit.Card(scrim.transform, Theme.Surface, 44, 20);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = new Vector2(0.05f, 0.12f);
            rect.anchorMax = new Vector2(0.95f, 0.88f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            UIKit.Display(card.transform, "Who is learning?", Theme.Heading + 10);
            UIKit.Label(card.transform, "Profiles stay on this device. Only a display name is stored.", Theme.Small, Theme.Muted);

            var scroll = UIKit.Scroll(card.transform, out profileList);
            UIKit.Size(scroll.gameObject, minHeight: 300, flexibleHeight: 1);

            var create = UIKit.HStack(card.transform, 16);
            UIKit.Size(create.gameObject, minHeight: Theme.TouchTarget);
            nameInput = UIKit.Input(create.transform, "New learner name");
            var add = UIKit.Button(create.transform, "Add", CreateProfile);
            UIKit.Size(add.gameObject, minHeight: Theme.TouchTarget, minWidth: 220, preferredWidth: 220);

            UIKit.Button(card.transform, "Done", () => { Click(); ShowProfiles(false); }, ButtonStyle.Secondary);
            profilePanel.SetActive(false);
        }

        private void ShowProfiles(bool show)
        {
            profilePanel.SetActive(show);
            if (show) RefreshProfileList();
            else RefreshProfile();
        }

        private void RefreshProfileList()
        {
            UIKit.ClearChildren(profileList);
            var active = Session.Data.ActiveProfile;
            var profiles = Session.Data.Profiles.List();
            if (profiles.Count == 0)
                UIKit.Label(profileList, "No learners yet. Type a name below and tap Add.", Theme.Body, Theme.Muted);

            foreach (var profile in profiles)
            {
                var row = UIKit.HStack(profileList, 14);
                UIKit.Size(row.gameObject, minHeight: Theme.TouchTarget, preferredHeight: Theme.TouchTarget);
                var selected = active != null && active.learnerId == profile.learnerId;
                var target = profile;
                var pick = UIKit.Button(row.transform, profile.displayName, () =>
                {
                    Click();
                    Session.Data.SelectProfile(target);
                    RefreshProfileList();
                }, selected ? ButtonStyle.Primary : ButtonStyle.Secondary);
                UIKit.Size(pick.gameObject, minHeight: Theme.TouchTarget, flexibleWidth: 1);
                var delete = UIKit.Button(row.transform, "Delete", () => DeleteProfile(target), ButtonStyle.Danger, fontSize: Theme.Small);
                UIKit.Size(delete.gameObject, minHeight: Theme.TouchTarget, minWidth: 200, preferredWidth: 200);
            }
        }

        private void CreateProfile()
        {
            Click();
            try
            {
                var profile = Session.Data.Profiles.Create(nameInput.text);
                Session.Data.SelectProfile(profile);
                nameInput.text = string.Empty;
                RefreshProfileList();
            }
            catch (ArgumentException exception)
            {
                Ui.Events.Publish(new ToastRequested(exception.Message, ToastKind.Warning));
            }
        }

        private void DeleteProfile(LearnerProfile profile)
        {
            Click();
            Session.Data.Profiles.Delete(profile.learnerId);
            if (Session.Data.Settings.LastProfileId == profile.learnerId) Session.Data.SelectProfile(null);
            Ui.Events.Publish(new ToastRequested($"Deleted {profile.displayName} and their sessions.", ToastKind.Info));
            RefreshProfileList();
        }
    }
}
