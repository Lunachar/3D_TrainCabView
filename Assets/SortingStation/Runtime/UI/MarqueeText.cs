using TMPro;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// One-line ticker: text that fits stays centred; longer text scrolls from right to left
    /// through its (clipped) box, pauses briefly, and starts again. A new text restarts it.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class MarqueeText : MonoBehaviour
    {
        public const float SpeedPixelsPerSecond = 70f;
        private TextMeshProUGUI label;
        private RectTransform box;
        private string shown;
        private float started;

        private void Awake()
        {
            label = GetComponent<TextMeshProUGUI>();
            box = (RectTransform)transform.parent;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
        }

        private void LateUpdate()
        {
            if (label == null || box == null) return;
            if (label.text != shown)
            {
                shown = label.text;
                started = Time.unscaledTime;
            }
            float boxWidth = box.rect.width;
            float textWidth = label.preferredWidth;
            RectTransform rect = label.rectTransform;
            rect.sizeDelta = new Vector2(textWidth, rect.sizeDelta.y);
            if (textWidth <= boxWidth - 16f)
            {
                label.alignment = TextAlignmentOptions.Center;
                rect.anchoredPosition = new Vector2((boxWidth - textWidth) * 0.5f, rect.anchoredPosition.y);
                return;
            }
            label.alignment = TextAlignmentOptions.Left;
            // Start just inside the right edge, run until the end has passed the left edge.
            float travel = textWidth + boxWidth * 0.35f;
            float elapsed = Time.unscaledTime - started;
            float offset = Mathf.Repeat(elapsed * SpeedPixelsPerSecond, travel + 80f);
            rect.anchoredPosition = new Vector2(boxWidth * 0.35f - Mathf.Min(offset, travel), rect.anchoredPosition.y);
        }
    }
}
