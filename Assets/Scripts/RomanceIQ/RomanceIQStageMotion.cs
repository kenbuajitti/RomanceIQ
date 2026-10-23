using UnityEngine;
using UnityEngine.UI;

namespace RomanceIQ
{
    // Artwork remains reusable: no generated video or scene-specific animation clips.
    public sealed class RomanceIQStageMotion : MonoBehaviour
    {
        LayoutElement size;
        RawImage backdrop;
        RectTransform[] cast;
        float castAspect = .5f, lastWidth = -1, started;
        int focus;
        bool motion, fixedStage;
        float lastHeight = -1, lastDialogueInset = -1, dialogueFraction;
        public void Configure(LayoutElement element, RawImage background, RectTransform[] portraits,
            Texture2D texture, int active, bool animate, bool fixedLayout = false,
            float translucentDialogueFraction = 0f)
        {
            fixedStage = fixedLayout;
            dialogueFraction = Mathf.Clamp01(translucentDialogueFraction);
            size = element; backdrop = background; cast = portraits; focus = active; motion = animate;
            if (texture != null) castAspect = texture.width / (3f * texture.height);
            started = Time.unscaledTime;
            RefreshLayout();
        }
        public void SetMotion(bool enabled) { motion = enabled; }

        void RefreshLayout()
        {
            float width = ((RectTransform)transform).rect.width;
            float actualHeight = ((RectTransform)transform).rect.height;
            float bottomInset = fixedStage && dialogueFraction > 0f && transform.parent is RectTransform
                ? ((RectTransform)transform.parent).rect.height * dialogueFraction : 0f;
            if (width < 1 || (Mathf.Abs(width - lastWidth) < .5f &&
                Mathf.Abs(actualHeight - lastHeight) < .5f &&
                Mathf.Abs(bottomInset - lastDialogueInset) < .5f)) return;
            lastHeight = actualHeight;
            lastWidth = width;
            lastDialogueInset = bottomInset;
            float height = fixedStage ? Mathf.Max(1, actualHeight) : Mathf.Clamp(width * .56f, 210, 440);
            float portraitAreaHeight = Mathf.Max(1, height - bottomInset);
            if (!fixedStage) size.preferredHeight = size.minHeight = height;
            if (backdrop.texture != null)
            {
                // Cinematic full-bleed background, like Family Rate.
                // Crop *only the scenery* to cover the wide viewport; portraits are
                // separate UI images, so the speaker stays fully visible.
                float sourceAspect = (float)backdrop.texture.width / backdrop.texture.height;
                float viewportAspect = width / height;
                RectTransform imageRect = backdrop.rectTransform;
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.pivot = new Vector2(.5f, .5f);
                imageRect.anchoredPosition = Vector2.zero;
                imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
                if (viewportAspect > sourceAspect)
                {
                    float visibleV = sourceAspect / viewportAspect;
                    backdrop.uvRect = new Rect(0, (1f - visibleV) * .5f, 1f, visibleV);
                }
                else
                {
                    float visibleU = viewportAspect / sourceAspect;
                    backdrop.uvRect = new Rect((1f - visibleU) * .5f, 0, visibleU, 1f);
                }
            }
            foreach (var person in cast)
            {
                var image = person.GetComponent<RawImage>();
                float aspect = image.texture == null ? castAspect : image.texture.width * image.uvRect.width / image.texture.height;
                float x = (float)(System.Array.IndexOf(cast, person) + .5f) / cast.Length;
                person.anchorMin = person.anchorMax = new Vector2(x, Mathf.Clamp01(bottomInset / height));
                float h = Mathf.Min(portraitAreaHeight * (cast.Length == 1 ? .97f : 1.10f),
                    width / cast.Length / aspect);
                person.sizeDelta = new Vector2(h * aspect, h);
            }
        }
        void Update()
        {
            if (cast == null) return;
            RefreshLayout();
            float elapsed = Time.unscaledTime - started;
            for (int i = 0; i < cast.Length; i++)
            {
                float bob = motion ? Mathf.Sin(elapsed * 1.4f + i * .9f) * 1.5f : 0;
                cast[i].anchoredPosition = new Vector2(0, bob - 2);
                float emphasis = cast.Length > 1 && i == focus ? 1.025f : 1;
                float breath = motion ? Mathf.Sin(elapsed * 1.4f + i * .9f) * .0025f : 0;
                cast[i].localScale = Vector3.one * (emphasis + breath);
            }
        }
    }
}
