using UnityEngine;

namespace Fatiro.UI
{
    /// <summary>Fits its rect to Screen.safeArea so buttons avoid notches and gesture bars.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect applied;

        private void Update()
        {
            var safe = Screen.safeArea;
            if (safe == applied || Screen.width == 0 || Screen.height == 0) return;
            applied = safe;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
