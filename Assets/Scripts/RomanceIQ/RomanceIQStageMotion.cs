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
        float lastHeight = -1;
        public void Configure(LayoutElement element, RawImage background, RectTransform[] portraits, Texture2D texture, int active, bool animate, bool fixedLayout = false)
        {
            fixedStage = fixedLayout; size = element; backdrop = background; cast = portraits; focus = active; motion = animate;
            if (texture != null) castAspect = texture.width / (3f * texture.height);
            started = Time.unscaledTime;
            RefreshLayout();
        }
        public void SetMotion(bool enabled) { motion = enabled; }

        void RefreshLayout()
        {
            float width = ((RectTransform)transform).rect.width;
            float actualHeight = ((RectTransform)transform).rect.height;
            if (width < 1 || (Mathf.Abs(width - lastWidth) < .5f && Mathf.Abs(actualHeight - lastHeight) < .5f)) return;
            lastHeight = actualHeight;
            lastWidth = width;
            float height = fixedStage ? Mathf.Max(1, actualHeight) : Mathf.Clamp(width * .56f, 210, 440);
            if (!fixedStage) size.preferredHeight = size.minHeight = height;
            if (backdrop.texture != null)
            {
                float source = (float)backdrop.texture.width / backdrop.texture.height;
                float target = width / height;
                backdrop.uvRect = target > source ? new Rect(0, (1 - source / target) / 2, 1, source / target)
                    : new Rect((1 - target / source) / 2, 0, target / source, 1);
            }
            foreach (var person in cast)
            {
                var image = person.GetComponent<RawImage>();
                float aspect = image.texture == null ? castAspect : image.texture.width * image.uvRect.width / image.texture.height;
                float h = Mathf.Min(height * (cast.Length == 1 ? .97f : 1.10f), width / cast.Length / aspect);
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
