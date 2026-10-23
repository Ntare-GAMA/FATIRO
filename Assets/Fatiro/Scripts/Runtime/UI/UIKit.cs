using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>
    /// Holographic palette: dark glass, neon cyan and lime light, amber highlights. Imigongo
    /// patterns and Kinyarwanda labels are kept, redrawn as neon line art.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Paper = new Color(0.016f, 0.043f, 0.063f, 0.97f);
        public static readonly Color Backdrop = new Color(0.012f, 0.035f, 0.055f, 0.96f);
        public static readonly Color Surface = new Color(0.024f, 0.085f, 0.118f, 0.84f);
        public static readonly Color SurfaceRaised = new Color(0.055f, 0.150f, 0.200f, 0.88f);
        public static readonly Color SurfaceSelected = new Color(0.035f, 0.300f, 0.360f, 0.92f);
        public static readonly Color Primary = new Color(0.270f, 0.860f, 1.000f);
        public static readonly Color PrimaryDark = new Color(0.050f, 0.330f, 0.420f, 0.95f);
        public static readonly Color Leaf = new Color(0.720f, 0.950f, 0.250f);
        public static readonly Color Accent = new Color(0.790f, 0.980f, 0.300f);
        public static readonly Color AccentDark = PrimaryDark;
        public static readonly Color Sun = new Color(1.000f, 0.780f, 0.250f);
        public static readonly Color Teal = new Color(0.200f, 0.700f, 0.850f);
        public static readonly Color Ink = new Color(0.010f, 0.050f, 0.070f);
        public static readonly Color Text = new Color(0.860f, 0.960f, 1.000f);
        public static readonly Color OnPrimary = new Color(0.880f, 0.985f, 1.000f);
        public static readonly Color Muted = new Color(0.480f, 0.660f, 0.740f);
        public static readonly Color Success = new Color(0.550f, 0.950f, 0.450f);
        public static readonly Color Warning = new Color(1.000f, 0.780f, 0.250f);
        public static readonly Color Danger = new Color(1.000f, 0.360f, 0.400f);
        public static readonly Color Scrim = new Color(0f, 0.02f, 0.04f, 0.72f);
        public static readonly Color Line = new Color(0.270f, 0.860f, 1.000f, 0.45f);

        public const int Title = 76;
        public const int Heading = 50;
        public const int Subheading = 40;
        public const int Body = 38;
        public const int Small = 31;

        /// <summary>About 48 dp on a typical phone at the 1080 × 1920 reference resolution (NFR-05).</summary>
        public const float TouchTarget = 128f;
    }

    public enum ButtonStyle
    {
        Primary,
        Secondary,
        Ghost,
        Danger
    }

    /// <summary>Small helpers that build uGUI controls in code so every screen looks consistent.</summary>
    public static class UIKit
    {
        private static Sprite rounded;
        private static Font fallback;
        private static Font display;
        private static Font body;
        private static Font bold;
        private static Font italic;

        public static Font Font => body != null ? body : fallback ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Installs the Addressable typefaces: DM Serif Display for titles, Lato for text.</summary>
        public static void UseFonts(Font displayFont, Font bodyFont, Font boldFont, Font italicFont)
        {
            display = displayFont;
            body = bodyFont;
            bold = boldFont;
            italic = italicFont;
        }

        /// <summary>A title set in the display serif.</summary>
        public static Text Display(Transform parent, string text, int size, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var label = Label(parent, text, size, color ?? Theme.Primary, align, FontStyle.Normal, "Title");
            if (display != null) label.font = display;
            label.lineSpacing = 0.9f;
            return label;
        }

        /// <summary>A band of Imigongo zigzags, Rwanda's traditional geometric relief art.</summary>
        public static Image ImigongoBand(Transform parent, float height, string name = "Imigongo")
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = RwandaArt.Imigongo;
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 64f / height;
            image.raycastTarget = false;
            Size(rect.gameObject, minHeight: height, preferredHeight: height);
            return image;
        }

        /// <summary>Paper bar docked to the bottom edge with an Imigongo trim; returns the area for buttons.</summary>
        public static RectTransform Footer(Transform parent, float height, string name = "Footer")
        {
            var bar = Panel(parent, Theme.Paper, name, false);
            bar.rectTransform.Dock(TextAnchor.LowerCenter, height);
            var band = ImigongoBand(bar.transform, 12);
            band.rectTransform.anchorMin = new Vector2(0, 1);
            band.rectTransform.anchorMax = new Vector2(1, 1);
            band.rectTransform.pivot = new Vector2(0.5f, 1);
            band.rectTransform.sizeDelta = new Vector2(0, 12);
            return Rect("Buttons", bar.transform).Stretch(40, 34, 40, 28);
        }

        /// <summary>Colour key for utilisation, with words as well as colour (NFR-06).</summary>
        public static HorizontalLayoutGroup Legend(Transform parent)
        {
            var row = HStack(parent, 18, name: "Legend");
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            Size(row.gameObject, minHeight: 44, preferredHeight: 44);
            foreach (var (colour, text) in new[]
                     {
                         (World.MemberView.Safe, "under 70% safe"),
                         (World.MemberView.Warning, "70–100% near limit"),
                         (World.MemberView.Danger, "over 100% fails")
                     })
            {
                var swatch = Panel(row.transform, colour, "Swatch");
                swatch.raycastTarget = false;
                Size(swatch.gameObject, minWidth: 30, preferredWidth: 30, minHeight: 30, preferredHeight: 30);
                var label = Label(row.transform, text, 24, Theme.Muted);
                Size(label.gameObject, preferredWidth: label.preferredWidth + 8);
            }
            return row;
        }

        /// <summary>A 9-sliced rounded rectangle generated once at runtime.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                const float radius = 10f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Rounded UI" };
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                        var dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                        var distance = Mathf.Sqrt(dx * dx + dy * dy);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 0.5f)));
                    }
                }
                texture.Apply();
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
                return rounded;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Stretch(this RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform Dock(this RectTransform rect, TextAnchor edge, float size, float margin = 0f)
        {
            switch (edge)
            {
                case TextAnchor.UpperCenter:
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.offsetMin = new Vector2(margin, -size - margin);
                    rect.offsetMax = new Vector2(-margin, -margin);
                    break;
                case TextAnchor.LowerCenter:
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.offsetMin = new Vector2(margin, margin);
                    rect.offsetMax = new Vector2(-margin, size + margin);
                    break;
            }
            return rect;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel", bool roundedCorners = true)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (roundedCorners)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1.2f;
            }
            return image;
        }

        /// <summary>A thin glowing rim drawn over a panel, like the edge of a hologram.</summary>
        public static Image Outline(Image panel, Color color)
        {
            var existing = panel.transform.Find("Outline");
            var image = existing != null ? existing.GetComponent<Image>() : Overlay(panel.transform, "Outline", RoundedOutline);
            image.color = color;
            return image;
        }

        /// <summary>Bright L-shaped brackets on the four corners: the "targeting frame" look of the HUD.</summary>
        public static Image Corners(Image panel, Color color)
        {
            var image = Overlay(panel.transform, "Corners", CornerBrackets);
            image.color = color;
            return image;
        }

        private static Image Overlay(Transform parent, string name, Sprite sprite)
        {
            var rect = Rect(name, parent).Stretch();
            rect.SetAsLastSibling();
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.2f;
            image.raycastTarget = false;
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        private static Sprite roundedOutline;
        private static Sprite cornerBrackets;

        private static Sprite RoundedOutline => roundedOutline ??= MakeFrameSprite("Outline", (x, y, size, radius) =>
        {
            var dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
            var dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
            var distance = Mathf.Sqrt(dx * dx + dy * dy);
            var inside = Mathf.Clamp01(radius - distance + 0.5f);
            var innerDistance = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
            var ring = dx > 0f && dy > 0f ? Mathf.Clamp01(2.5f - Mathf.Abs(radius - 1.25f - distance)) : Mathf.Clamp01(2.5f - innerDistance);
            return inside * ring;
        });

        private static Sprite CornerBrackets => cornerBrackets ??= MakeFrameSprite("Corners", (x, y, size, radius) =>
        {
            const float length = 20f;
            const float thickness = 3.5f;
            var nearX = Mathf.Min(x + 0.5f, size - x - 0.5f);
            var nearY = Mathf.Min(y + 0.5f, size - y - 0.5f);
            var horizontal = nearY <= thickness && nearX <= length;
            var vertical = nearX <= thickness && nearY <= length;
            return horizontal || vertical ? 1f : 0f;
        });

        private static Sprite MakeFrameSprite(string name, System.Func<int, int, int, float, float> alpha)
        {
            const int size = 64;
            const float radius = 10f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y, size, radius)));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(24, 24, 24, 24));
        }

        public static Text Label(Transform parent, string text, int size = Theme.Body, Color? color = null,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, string name = "Label")
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            // Use the real bold/italic faces when loaded rather than synthesising them.
            var face = style == FontStyle.Bold ? bold : style == FontStyle.Italic ? italic : null;
            label.font = face != null ? face : Font;
            label.fontStyle = face != null ? FontStyle.Normal : style;
            label.text = text;
            label.fontSize = size;
            label.color = color ?? Theme.Text;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string text, UnityAction onClick, ButtonStyle style = ButtonStyle.Primary,
            float height = Theme.TouchTarget, int fontSize = Theme.Body)
        {
            var background = Panel(parent, BackgroundFor(style), "Button " + text);
            Outline(background, OutlineFor(style));
            var button = background.gameObject.AddComponent<Button>();
            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colours;
            if (onClick != null) button.onClick.AddListener(onClick);

            var label = Label(background.transform, text.ToUpperInvariant(), fontSize, TextFor(style), TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Stretch(16, 4, 16, 4);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = fontSize;

            Size(background.gameObject, minHeight: height, preferredHeight: height);
            return button;
        }

        public static Color BackgroundFor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary: return new Color(0.27f, 0.86f, 1f, 0.24f);
                case ButtonStyle.Danger: return new Color(1f, 0.36f, 0.40f, 0.16f);
                case ButtonStyle.Ghost: return new Color(0.27f, 0.86f, 1f, 0.04f);
                default: return Theme.SurfaceRaised;
            }
        }

        public static Color TextFor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary: return Theme.OnPrimary;
                case ButtonStyle.Danger: return Theme.Danger;
                case ButtonStyle.Ghost: return Theme.Muted;
                default: return Theme.Text;
            }
        }

        public static void SetLabel(this Button button, string text) => button.GetComponentInChildren<Text>().text = text.ToUpperInvariant();

        public static Color OutlineFor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary: return Theme.Primary;
                case ButtonStyle.Danger: return new Color(1f, 0.36f, 0.40f, 0.8f);
                case ButtonStyle.Ghost: return new Color(0.27f, 0.86f, 1f, 0.22f);
                default: return Theme.Line;
            }
        }

        public static void SetStyle(this Button button, ButtonStyle style)
        {
            button.GetComponent<Image>().color = BackgroundFor(style);
            button.GetComponentInChildren<Text>().color = TextFor(style);
            Outline(button.GetComponent<Image>(), OutlineFor(style));
        }

        public static VerticalLayoutGroup VStack(Transform parent, float spacing = 20f, int padding = 0, string name = "Column")
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup HStack(Transform parent, float spacing = 16f, int padding = 0, string name = "Row")
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static LayoutElement Size(GameObject target, float minHeight = -1, float preferredHeight = -1, float flexibleWidth = -1,
            float minWidth = -1, float preferredWidth = -1, float flexibleHeight = -1)
        {
            if (!target.TryGetComponent<LayoutElement>(out var element)) element = target.AddComponent<LayoutElement>();
            element.minHeight = minHeight;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.minWidth = minWidth;
            element.preferredWidth = preferredWidth;
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        /// <summary>Card with a vertical layout that grows to fit its content.</summary>
        public static VerticalLayoutGroup Card(Transform parent, Color? color = null, int padding = 32, float spacing = 14f, string name = "Card")
        {
            var image = Panel(parent, color ?? Theme.Surface, name);
            Outline(image, Theme.Line);
            Corners(image, new Color(0.27f, 0.86f, 1f, 0.9f));
            var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static ContentSizeFitter FitHeight(GameObject target)
        {
            if (!target.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter = target.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return fitter;
        }

        public static ScrollRect Scroll(Transform parent, out RectTransform content, bool horizontal = false, string name = "Scroll")
        {
            var viewport = Rect(name, parent);
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0, 0, 0, 0);

            content = Rect("Content", viewport);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            if (horizontal)
            {
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
                var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 16;
                row.padding = new RectOffset(8, 8, 8, 8);
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = true;
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
                var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
                column.spacing = 20;
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandWidth = true;
                column.childForceExpandHeight = false;
                FitHeight(content.gameObject);
            }
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            return scroll;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, bool wholeNumbers, UnityAction<float> onChanged)
        {
            var root = Rect("Slider", parent);
            Size(root.gameObject, minHeight: 90, preferredHeight: 90);
            var slider = root.gameObject.AddComponent<Slider>();

            var track = Panel(root, Theme.SurfaceRaised, "Track");
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(-40, 22);

            var fillArea = Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-40, 22);
            var fill = Panel(fillArea, Theme.Primary, "Fill");
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("Handle Area", root);
            handleArea.anchorMin = new Vector2(0, 0.5f);
            handleArea.anchorMax = new Vector2(1, 0.5f);
            handleArea.sizeDelta = new Vector2(-40, 64);
            var handle = Panel(handleArea, Theme.Sun, "Handle");
            handle.rectTransform.sizeDelta = new Vector2(64, 0);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static InputField Input(Transform parent, string placeholder, UnityAction<string> onSubmit = null)
        {
            var background = Panel(parent, Theme.SurfaceRaised, "Input");
            Outline(background, Theme.Line);
            Size(background.gameObject, minHeight: Theme.TouchTarget, preferredHeight: Theme.TouchTarget, flexibleWidth: 1);
            var text = Label(background.transform, string.Empty, Theme.Body);
            text.rectTransform.Stretch(28, 8, 28, 8);
            text.supportRichText = false;
            text.raycastTarget = true;
            var hint = Label(background.transform, placeholder, Theme.Body, Theme.Muted, style: FontStyle.Italic);
            hint.rectTransform.Stretch(28, 8, 28, 8);

            var input = background.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = 24;
            input.lineType = InputField.LineType.SingleLine;
            if (onSubmit != null) input.onEndEdit.AddListener(onSubmit);
            return input;
        }

        /// <summary>A horizontal progress bar; returns the fill image (scale X to set progress).</summary>
        public static RectTransform ProgressBar(Transform parent, Color fillColor, float height = 22f)
        {
            var track = Panel(parent, Theme.SurfaceRaised, "Progress");
            Size(track.gameObject, minHeight: height, preferredHeight: height);
            var fill = Panel(track.transform, fillColor, "Fill");
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            return fill.rectTransform;
        }

        public static void SetProgress(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            fill.offsetMax = Vector2.zero;
        }

        public static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        public static string Colour(string text, Color color) => $"<color=#{Hex(color)}>{text}</color>";

        public static void Safe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
