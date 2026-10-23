using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RomanceIQ
{
    [Serializable] internal sealed class VNPathRequest
    {
        public string storyId, storyVersion;
        public string[] path;
    }
    [Serializable] internal sealed class VNOnlineScene
    {
        public string id, speaker, text, background;
        public VNChoice[] choices;
    }
    [Serializable] internal sealed class VNOnlineResponse
    {
        public VNOnlineScene scene;
        public string error, message;
    }

    public sealed partial class RomanceIQController
    {
        // Separate from the optional older twenty-turn prototype story service.
        // Public gateway only: never embed an OpenAI or Supabase secret in the game.
        [SerializeField] string vnSharedStoryUrl =
            "https://vwrwgcqiixmqlzyuybbx.supabase.co/functions/v1/romanceiq-story";
        bool vnRequestBusy;
        RectTransform vnChapterTransition;
        float vnChapterTransitionStarted;
        const int VNMaxDecisions = 30;

        // The illustration includes the caption, so we display it as one image.
        // "FitInParent" keeps all six characters AND the bottom caption in view;
        // on unusually tall/narrow screens the surrounding plum colour fills the bars.
        void ShowVNChapterTransition()
        {
            HideVNChapterTransition();
            CloseFixedReader();
            frame.gameObject.SetActive(false);
            if (menuRoot != null) menuRoot.gameObject.SetActive(false);
            vnChapterTransition = Panel("Next chapter ensemble", safeArea,
                new Color(.13f, .075f, .17f, 1f));
            Stretch(vnChapterTransition);

            Texture2D artwork = Resources.Load<Texture2D>("RomanceIQ/VisualNovel/NextChapterEnsemble");
            if (artwork != null)
            {
                var art = Rect("Complete ensemble artwork", vnChapterTransition);
                art.anchorMin = art.anchorMax = art.pivot = new Vector2(.5f, .5f);
                art.anchoredPosition = Vector2.zero;
                var image = art.gameObject.AddComponent<RawImage>();
                image.texture = artwork;
                image.raycastTarget = false;
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)artwork.width / artwork.height;
            }
            else
            {
                // Even a missing/reimporting image must not obscure the request status.
                var fallback = Label(vnChapterTransition,
                    "RETRIEVING/WRITING YOUR NEXT CHAPTER...", 28, Color.white, false);
                Stretch(fallback.rectTransform);
                fallback.alignment = TMPro.TextAlignmentOptions.Center;
            }

            // Keep the original escape to the menu while an online chapter loads.
            var menuButton = FlatButton(vnChapterTransition, "MENU", ReturnToMenu);
            SetBounds(menuButton.GetComponent<RectTransform>(), new Vector2(1, 1), Vector2.one,
                new Vector2(-115, -60), new Vector2(-12, -14));
            menuButton.GetComponent<Image>().color = new Color(.13f, .075f, .17f, .92f);
            menuButton.GetComponentInChildren<TMPro.TMP_Text>().color = Color.white;
            vnChapterTransition.SetAsLastSibling();
            vnChapterTransitionStarted = Time.realtimeSinceStartup;
        }

        void HideVNChapterTransition()
        {
            if (vnChapterTransition == null) return;
            vnChapterTransition.gameObject.SetActive(false);
            Destroy(vnChapterTransition.gameObject);
            vnChapterTransition = null;
        }

        Texture2D VNBackgroundFor(VNNode node)
        {
            string setting = node.background;
            if (string.IsNullOrEmpty(setting))
            {
                string id = node.id.ToLowerInvariant();
                string text = (node.text ?? "").ToLowerInvariant();
                setting = id.StartsWith("ending_") ? "StreetNight" :
                    id == "tag" ? "LobbyEvening" :
                    text.Contains("rooftop") ? "StreetNight" :
                    text.Contains("projection booth") || text.Contains("projector room") ? "Projection" :
                    text.Contains("street") || text.Contains("outside") ? "StreetNight" :
                    text.Contains("morning") ? "LobbyMorning" : "Lobby";
            }
            var result = Resources.Load<Texture2D>("RomanceIQ/VisualNovel/" + setting);
            return result != null ? result : vnBackground;
        }

        void VNChooseOnline(string from, string choice)
        {
            if (vnRequestBusy || vnTransition || vnRun.nodeId != from || vnRun.path.Count >= VNMaxDecisions)
                return;
            vnTransition = true;
            RomanceIQAnalytics.ChoiceMade(vnRun.path.Count + 1, from + ":" + choice);
            vnRun.path.Add(from + ":" + choice);
            vnRun.nodeId = "live_" + vnRun.path.Count;
            vnRun.completed = false;
            SaveVNRun();
            StartCoroutine(VNLoadOnlineStory());
        }

        IEnumerator VNLoadOnlineChoices()
        {
            if (vnRequestBusy || vnRun.path.Count != 3) yield break;
            string runId = vnRun.runId;
            string[] path = vnRun.path.ToArray();
            VNOnlineScene received = null;
            string failure = null;
            yield return VNRequestScene(path, (scene, error) => { received = scene; failure = error; });
            if (vnRun == null || vnRun.runId != runId || vnRun.path.Count != path.Length || (menuRoot != null && menuRoot.gameObject.activeSelf)) yield break;
            if (received == null || received.choices == null || received.choices.Length != 3)
            { VNShowOnlineError(failure); yield break; }
            var actions = new List<UnityAction>();
            var captions = new List<string>();
            string from = vnRun.nodeId;
            foreach (var option in received.choices)
            {
                string id = option.id;
                captions.Add(option.text);
                actions.Add(() => VNChooseOnline(from, id));
            }
            var node = vnNodes[from];
            vnSceneBackground = VNBackgroundFor(node);
            OpenFixedReader(node.speaker, node.text, node.focus,
                captions, actions, null, true, BackVNQuestion);
        }

        IEnumerator VNLoadOnlineStory()
        {
            if (vnRequestBusy || vnRun == null || vnRun.path.Count < 4) yield break;
            string runId = vnRun.runId;
            string nodeId = vnRun.nodeId;
            string[] path = vnRun.path.ToArray();
            VNOnlineScene received = null;
            string failure = null;
            yield return VNRequestScene(path, (scene, error) => { received = scene; failure = error; });
            if (vnRun == null || vnRun.runId != runId || vnRun.nodeId != nodeId || vnRun.path.Count != path.Length || (menuRoot != null && menuRoot.gameObject.activeSelf))
                yield break;
            if (received == null || string.IsNullOrWhiteSpace(received.text) ||
                (path.Length < VNMaxDecisions && (received.choices == null || received.choices.Length != 3)))
            { VNShowOnlineError(failure); yield break; }
            vnNodes[nodeId] = new VNNode
            {
                id = nodeId, speaker = string.IsNullOrEmpty(received.speaker) ? "MAYA" : received.speaker,
                text = received.text, background = received.background,
                focus = string.Equals(received.speaker, "NOAH", StringComparison.OrdinalIgnoreCase) ? 1 :
                        string.Equals(received.speaker, "ELI", StringComparison.OrdinalIgnoreCase) ? 2 : 0,
                choices = received.choices, ending = path.Length >= VNMaxDecisions
            };
            vnTransition = false;
            ShowVNNode();
        }

        void VNShowOnlineError(string details)
        {
            vnTransition = false;
            ClearPage("THE STORY CONTINUES ONLINE");
            home.gameObject.SetActive(true);
            Label(content, "Your progress has been saved.", 33, ink);
            Label(content, string.IsNullOrEmpty(details)
                ? "The next chapter is temporarily unavailable. Try again when connected."
                : details, 22, ink);
            if (vnRun.path.Count == 3)
                MakeButton(content, "RETRY NEXT DECISIONS", () => StartCoroutine(VNLoadOnlineChoices()));
            else
                MakeButton(content, "RETRY STORY", () => StartCoroutine(VNLoadOnlineStory()));
            MakeButton(content, "PREVIOUS DECISION", BackVNQuestion);
            FinishPage();
        }

        IEnumerator VNRequestScene(string[] path, Action<VNOnlineScene, string> completed)
        {
            if (vnRequestBusy) { completed(null, "The previous request is still finishing."); yield break; }
            if (string.IsNullOrEmpty(vnSharedStoryUrl) || !vnSharedStoryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            { completed(null, "The public story gateway has not been configured."); yield break; }
            vnRequestBusy = true;
            ShowVNChapterTransition();
            string payload = JsonUtility.ToJson(new VNPathRequest {
                storyId = vnStory.storyId, storyVersion = vnStory.version, path = path
            });
            string error = "The story service is unavailable. Please retry.";
            VNOnlineScene result = null;
            for (int attempt = 0; attempt < 75; attempt++)
            {
                using (var req = new UnityWebRequest(vnSharedStoryUrl.TrimEnd('/') + "/api/scene", "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.timeout = 45;
                    yield return req.SendWebRequest();
                    if (req.responseCode == 202) { yield return new WaitForSecondsRealtime(2f); continue; }
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        try {
                            var body = JsonUtility.FromJson<VNOnlineResponse>(req.downloadHandler.text);
                            if (!string.IsNullOrWhiteSpace(body.message)) error = body.message;
                        } catch (Exception) { /* Keep the generic non-secret error. */ }
                        break;
                    }
                    try
                    {
                        var response = JsonUtility.FromJson<VNOnlineResponse>(req.downloadHandler.text);
                        if (response != null && response.scene != null) result = response.scene;
                        else error = "The story service returned an incomplete scene.";
                    }
                    catch (Exception) { error = "The story service returned invalid data."; }
                    break;
                }
            }
            // Cached chapters may return immediately: avoid a one-frame flash.
            if (result != null && vnChapterTransition != null)
            {
                float remaining = .8f - (Time.realtimeSinceStartup - vnChapterTransitionStarted);
                if (remaining > 0) yield return new WaitForSecondsRealtime(remaining);
            }
            vnRequestBusy = false;
            HideVNChapterTransition();
            completed(result, result == null ? error : null);
        }
    }
}
