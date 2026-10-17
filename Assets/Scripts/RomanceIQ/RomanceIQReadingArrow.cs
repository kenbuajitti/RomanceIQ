using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RomanceIQ
{
    public sealed partial class RomanceIQController
    {
        Button readingArrow;

        void UpdateReadingArrow()
        {
            if (fixedReader != null)
            {
                if (readingArrow != null) readingArrow.gameObject.SetActive(false);
                return;
            }
            if (frame == null || scroll == null || !frame.gameObject.activeInHierarchy) return;
            if (readingArrow == null)
            {
                // Replace only the old footer; preserve the header and scene progress.
                foreach (var label in frame.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.transform.parent == frame &&
                        (label.text == "ROMANCEIQ  |  Scroll to read more" ||
                         label.text == "RomanceIQ | Scroll to see more"))
                        label.gameObject.SetActive(false);
                }
                var root = Rect("Scroll down", frame);
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, 0);
                root.anchoredPosition = new Vector2(0, 8);
                root.sizeDelta = new Vector2(44, 44);
                // Explicit renderer: this custom Graphic does not rely on an Image
                // component or on a font containing an arrow glyph.
                root.gameObject.AddComponent<CanvasRenderer>();
                var graphic = root.gameObject.AddComponent<RomanceIQDownArrowGraphic>();
                graphic.color = Color.white;
                graphic.raycastTarget = true;
                readingArrow = root.gameObject.AddComponent<Button>();
                readingArrow.targetGraphic = graphic;
                readingArrow.transition = Selectable.Transition.ColorTint;
                readingArrow.onClick.AddListener(ScrollReadingPageDown);
            }
            float remainingHeight = content.rect.height - scroll.viewport.rect.height;
            bool moreBelow = remainingHeight > 1 &&
                scroll.verticalNormalizedPosition * remainingHeight > 1;
            readingArrow.gameObject.SetActive(moreBelow);
        }

        void ScrollReadingPageDown()
        {
            Canvas.ForceUpdateCanvases();
            float overflow = content.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0) return;
            scroll.StopMovement();
            // Keep a little of the previous page visible so readers retain their place.
            scroll.verticalNormalizedPosition = Mathf.Clamp01(
                scroll.verticalNormalizedPosition - scroll.viewport.rect.height * .8f / overflow);
        }
    }
}
