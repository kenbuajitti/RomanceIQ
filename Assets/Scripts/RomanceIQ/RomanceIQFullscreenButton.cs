using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace RomanceIQ
{
    // Desktop builds toggle via Unity. WebGL registers the *visible Unity button's*
    // screen rectangle with a DOM-level pointer handler; requestFullscreen() must
    // run synchronously inside the browser's trusted input event, not in Unity's
    // later Update loop. The HTML button remains the normal Unity UI element.
    public sealed class RomanceIQFullscreenButton : Button
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void RIQ_SetFullscreenHitbox(float minX, float minY, float maxX, float maxY);
        [DllImport("__Internal")]
        static extern int RIQ_IsBrowserFullscreen();

        readonly Vector3[] corners = new Vector3[4];
        Vector4 lastHitbox = new Vector4(-1, -1, -1, -1);

        void LateUpdate()
        {
            if (!IsActive() || !IsInteractable() || Screen.width < 1 || Screen.height < 1)
                return;

            ((RectTransform)transform).GetWorldCorners(corners);
            // The reader Canvas uses Screen Space - Overlay.
            Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            Vector4 box = new Vector4(
                Mathf.Clamp01(lowerLeft.x / Screen.width),
                Mathf.Clamp01(lowerLeft.y / Screen.height),
                Mathf.Clamp01(upperRight.x / Screen.width),
                Mathf.Clamp01(upperRight.y / Screen.height));
            if ((box - lastHitbox).sqrMagnitude > .00000005f)
            {
                RIQ_SetFullscreenHitbox(box.x, box.y, box.z, box.w);
                lastHitbox = box;
            }
        }

        protected override void OnDisable()
        {
            RIQ_SetFullscreenHitbox(0, 0, 0, 0);
            lastHitbox = new Vector4(-1, -1, -1, -1);
            base.OnDisable();
        }

        public static bool IsFullscreen() { return RIQ_IsBrowserFullscreen() != 0; }
#else
        public static bool IsFullscreen() { return Screen.fullScreen; }
#endif

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
                Screen.fullScreen = !Screen.fullScreen;
#endif
            // On WebGL this is intentionally handled by the browser's *native*
            // pointer event to preserve the required user-activation permission.
        }
    }
}
