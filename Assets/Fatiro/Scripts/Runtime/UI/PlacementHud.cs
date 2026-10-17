using Fatiro.Core;
using Fatiro.States;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>AR setup: plane detection feedback, placement instructions and confirmation (brief 2.5).</summary>
    public sealed class PlacementHud : UIScreen
    {
        private Text status;
        private Text instructions;
        private Text scale;
        private Image statusDot;
        private Button confirm;
        private PlacementState placement;
        private int planes;
        private bool tracking = true;

        public PlacementHud(UIContext context, PlacementState placementState) : base(context)
        {
            placement = placementState;
        }

        protected override void OnBuild()
        {
            var card = UIKit.Card(Root, Theme.Surface, 34, 12);
            ((RectTransform)card.transform).Dock(TextAnchor.UpperCenter, 430, 40);

            var statusRow = UIKit.HStack(card.transform, 18);
            statusRow.childForceExpandWidth = false;
            UIKit.Size(statusRow.gameObject, minHeight: 60);
            statusDot = UIKit.Panel(statusRow.transform, Theme.Warning, "Dot");
            UIKit.Size(statusDot.gameObject, minWidth: 34, preferredWidth: 34, minHeight: 34, preferredHeight: 34);
            status = UIKit.Label(statusRow.transform, string.Empty, Theme.Subheading, style: FontStyle.Bold);
            UIKit.Size(status.gameObject, flexibleWidth: 1);

            instructions = UIKit.Label(card.transform, string.Empty, Theme.Body);
            scale = UIKit.Label(card.transform, string.Empty, Theme.Small, Theme.Accent);

            var footer = UIKit.HStack(Root, 20);
            ((RectTransform)footer.transform).Dock(TextAnchor.LowerCenter, 150, 50);
            confirm = UIKit.Button(footer.transform, "Place site here", () => { Click(); Simulation.ConfirmPlacement(); }, height: 150, fontSize: Theme.Subheading);

            Ui.Events.Subscribe<PlanesUpdated>(message => planes = message.Count);
            Ui.Events.Subscribe<TrackingChanged>(message => tracking = message.IsTracking);
        }

        public override void Tick()
        {
            var hasPose = placement.HasPose;
            if (!tracking)
            {
                SetStatus("Tracking limited", Theme.Warning);
                instructions.text = "Move the device slowly and point it at a textured table or floor in good light.";
            }
            else if (planes == 0)
            {
                SetStatus("Scanning for a surface…", Theme.Warning);
                instructions.text = "Point your phone at a table and move it slowly from side to side until a grid appears.";
            }
            else if (!hasPose)
            {
                SetStatus($"Surface found ({planes})", Theme.Success);
                instructions.text = "Tap the grid to place the hillside site.";
            }
            else
            {
                SetStatus("Site positioned", Theme.Success);
                instructions.text = "Drag to move · pinch to scale · twist with two fingers to rotate.\nThe downhill side faces you. Tap “Place site here” to anchor it.";
            }

            scale.text = Session.Site != null && hasPose ? $"Scale 1:{Session.Site.ScaleDenominator}   (1:20 – 1:100)" : string.Empty;
            confirm.interactable = hasPose;
        }

        private void SetStatus(string text, Color color)
        {
            status.text = text;
            statusDot.color = color;
        }
    }
}
