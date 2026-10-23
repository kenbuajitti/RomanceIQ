using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RomanceIQ
{
    public sealed partial class RomanceIQController
    {
        RectTransform fixedReader, readerChoices;
        TMP_Text readerText, readerCue;
        Button readerAdvance, readerBack, readerFirst, readerForward, readerLast;
        string[] readerPassages;
        int readerPassage, readerPage;
        Vector2 readerSize;
        UnityAction readerNext, readerPrevious;
        IQSpeakerGraphic readerSpeaker;
        bool readerChoosing;

        void CloseFixedReader()
        {
            if (fixedReader == null) return;
            fixedReader.gameObject.SetActive(false);
            Destroy(fixedReader.gameObject);
            fixedReader = null;
            readerChoices = null;
            readerText = null;
        }

        void OpenFixedReader(string name, string narrative, int focus,
            List<string> captions, List<UnityAction> actions, UnityAction next, bool singlePortrait = false, UnityAction previous = null)
        {
            CloseFixedReader();
            scroll.StopMovement();
            frame.gameObject.SetActive(false);
            fixedReader = Panel("Single screen visual novel", safeArea, ink);
            Stretch(fixedReader);
            // The fixed reader is a sibling of the old reading frame, outside its ScrollRect.
            if (vnCast == null) vnCast = Resources.Load<Texture2D>("RomanceIQ/VisualNovel/Cast");
            if (vnBackground == null) vnBackground = Resources.Load<Texture2D>("RomanceIQ/VisualNovel/Lobby");
            BuildVNStage(focus, fixedReader, singlePortrait);
            // Complement the current location's lighting. All source textures are
            // non-readable on WebGL, so select a curated palette by background name.
            Color sceneInk = ReaderSceneInk();
            Color sceneAccent = ReaderSceneAccent();

            var shade = Panel("Toolbar", fixedReader,
                new Color(sceneInk.r * .88f, sceneInk.g * .88f, sceneInk.b * .88f, .96f));
            SetBounds(shade, new Vector2(0, 1), Vector2.one, new Vector2(0, -54), Vector2.zero);
            var menu = FlatButton(shade, "MENU", ShowMenu);
            SetBounds(menu.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0, 1), new Vector2(10, 6), new Vector2(108, -6));
            StyleReaderToolbarButton(menu, sceneInk);
            var motionButton = FlatButton(shade, "", () => {
                bool enabled = PlayerPrefs.GetInt(VNMotionKey, 1) != 1;
                PlayerPrefs.SetInt(VNMotionKey, enabled ? 1 : 0); PlayerPrefs.Save();
                foreach (var stage in fixedReader.GetComponentsInChildren<RomanceIQStageMotion>()) stage.SetMotion(enabled);
            });
            SetBounds(motionButton.GetComponent<RectTransform>(), new Vector2(1, 0), Vector2.one, new Vector2(-242, 6), new Vector2(-114, -6));
            motionButton.name = "Reader motion";
            StyleReaderToolbarButton(motionButton, sceneInk);

            // WebGL uses a real HTML button in IQGamesResponsive/index.html.
            // This avoids browsers dropping deferred Unity UI fullscreen requests.
#if !UNITY_WEBGL || UNITY_EDITOR
            // Fullscreen lives inside the game, not just the itch.io wrapper.
            // The WebGL bridge requests fullscreen from a native browser pointer event.
            var fullscreenRect = Panel("Reader fullscreen", shade, Color.white);
            SetBounds(fullscreenRect, new Vector2(1, 0), Vector2.one,
                new Vector2(-388, 6), new Vector2(-250, -6));
            var fullscreenButton = fullscreenRect.gameObject.AddComponent<RomanceIQFullscreenButton>();
            fullscreenButton.targetGraphic = fullscreenRect.GetComponent<Image>();
            var fullscreenText = Label(fullscreenRect, "FULLSCREEN", 19, Color.white, false);
            Stretch(fullscreenText.rectTransform);
            fullscreenText.rectTransform.offsetMin = new Vector2(5, 2);
            fullscreenText.rectTransform.offsetMax = new Vector2(-5, -2);
            fullscreenText.alignment = TextAlignmentOptions.Center;
            fullscreenText.enableAutoSizing = true;
            fullscreenText.fontSizeMin = 13;
            fullscreenText.fontSizeMax = 19;
            fullscreenText.raycastTarget = false;
            StyleReaderToolbarButton(fullscreenButton, sceneInk);

#endif

            var sound = Panel("Reader sound", shade, Color.clear);
            SetBounds(sound, new Vector2(1, 0), Vector2.one, new Vector2(-66, 3), new Vector2(-10, -3));
            var soundButton = sound.gameObject.AddComponent<Button>();
            soundButton.targetGraphic = sound.GetComponent<Image>();
            soundButton.transition = Selectable.Transition.None;
            soundButton.onClick.AddListener(music.ToggleSound);
            var soundIcon = Rect("Speaker", sound);
            Stretch(soundIcon);
            readerSpeaker = soundIcon.gameObject.AddComponent<IQSpeakerGraphic>();
            readerSpeaker.color = Color.white;
            readerSpeaker.raycastTarget = false;

            // Smaller translucent band; scenery extends behind it.
            var dialogue = Panel("Dialogue — click to advance", fixedReader,
                new Color(sceneInk.r * .80f, sceneInk.g * .80f, sceneInk.b * .80f, .76f));
            SetBounds(dialogue, Vector2.zero, new Vector2(1, .18f), Vector2.zero, Vector2.zero);
            readerAdvance = dialogue.gameObject.AddComponent<Button>();
            readerAdvance.targetGraphic = dialogue.GetComponent<Image>();
            readerAdvance.transition = Selectable.Transition.None;
            readerAdvance.onClick.AddListener(AdvanceReaderText);
            var speakerName = Label(dialogue, name, 19, sceneAccent, false);
            SetBounds(speakerName.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(20, -27), new Vector2(-20, -4));
            speakerName.enableAutoSizing = true; speakerName.fontSizeMin = 13; speakerName.fontSizeMax = 19;
            readerText = Label(dialogue, "", 21, new Color(.97f, .95f, .91f), false);
            // The narrative occupies the compact space above the navigation strip.
            // TextMeshPro pages overflow instead of cutting off long passages.
            SetBounds(readerText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(20, 34), new Vector2(-20, -28));
            readerText.overflowMode = TextOverflowModes.Page;
            readerText.enableAutoSizing = false;
            readerCue = Label(dialogue, "", 22, sceneAccent, false);
            SetBounds(readerCue.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(20, 2), new Vector2(-20, 25));
            readerCue.alignment = TextAlignmentOptions.Right;

            // Family Rate-style edge navigation: invisible hit targets and text-only arrows.
            // << previous decision | < previous text | > next text | >> choices/end.
            readerFirst = ReaderNavigationButton(dialogue, "<<", .02f, .10f, PreviousReaderQuestion);
            readerBack = ReaderNavigationButton(dialogue, "<", .12f, .20f, PreviousReaderText);
            readerForward = ReaderNavigationButton(dialogue, ">", .80f, .88f, AdvanceReaderText);
            readerLast = ReaderNavigationButton(dialogue, ">>", .90f, .98f, SkipToReaderChoices);

            readerChoices = Panel("Choices", fixedReader,
                new Color(sceneInk.r, sceneInk.g, sceneInk.b, .98f));
            SetBounds(readerChoices, new Vector2(.06f, .20f), new Vector2(.94f, 1), Vector2.zero, new Vector2(0, -116));
            for (int i = 0; i < captions.Count; i++)
            {
                UnityAction action = actions[i];
                var choice = FlatButton(readerChoices, captions[i], () => {
                    if (!readerChoosing) return;
                    readerChoosing = false;
                    foreach (var button in readerChoices.GetComponentsInChildren<Button>()) button.interactable = false;
                    action();
                });
                float bottom = 1f - (i + 1f) / captions.Count, top = 1f - i / (float)captions.Count;
                SetBounds(choice.GetComponent<RectTransform>(), new Vector2(0, bottom), new Vector2(1, top), new Vector2(10, 5), new Vector2(-10, -5));
                choice.GetComponentInChildren<TMP_Text>().fontSizeMin = 12;
            }
            readerChoices.gameObject.SetActive(false);
            readerPassages = (narrative ?? "").Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (readerPassages.Length == 0) readerPassages = new[] { "…" };
            readerPassage = 0; readerPage = 1; readerNext = next; readerPrevious = previous; readerChoosing = false;
            readerSize = Vector2.zero;
            DisplayReaderPassage();
            UpdateFixedReader();
        }

        Color ReaderSceneInk()
        {
            Texture2D image = vnSceneBackground != null ? vnSceneBackground : vnBackground;
            string scene = image == null ? "" : image.name.ToLowerInvariant();
            if (scene.Contains("street")) return new Color(.075f, .105f, .18f, 1f); // midnight navy
            if (scene.Contains("projection")) return new Color(.16f, .095f, .13f, 1f); // warm burgundy
            if (scene.Contains("evening")) return new Color(.16f, .105f, .16f, 1f); // plum
            return new Color(.075f, .145f, .13f, 1f); // teal; warm lobby & morning art
        }

        Color ReaderSceneAccent()
        {
            Texture2D image = vnSceneBackground != null ? vnSceneBackground : vnBackground;
            string scene = image == null ? "" : image.name.ToLowerInvariant();
            if (scene.Contains("street")) return new Color(.95f, .79f, .60f);
            if (scene.Contains("projection")) return new Color(.98f, .77f, .74f);
            return new Color(.98f, .77f, .68f);
        }

        void StyleReaderToolbarButton(Button button, Color sceneInk)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = new Color(
                    Mathf.Min(1f, sceneInk.r * 1.8f),
                    Mathf.Min(1f, sceneInk.g * 1.8f),
                    Mathf.Min(1f, sceneInk.b * 1.8f), 1f);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = new Color(.97f, .95f, .91f);
        }

        Button ReaderNavigationButton(RectTransform parent, string caption, float left, float right, UnityAction action)
        {
            // The transparent image is only a clickable target; there is no box or border.
            var rect = Rect("Navigation " + caption, parent);
            SetBounds(rect, new Vector2(left, 0), new Vector2(right, 0),
                new Vector2(0, 3), new Vector2(0, 32));
            var hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            var arrow = Label(rect, caption, 23, ReaderSceneAccent(), false);
            Stretch(arrow.rectTransform);
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.enableAutoSizing = true;
            arrow.fontSizeMin = 15;
            arrow.fontSizeMax = 23;
            arrow.raycastTarget = false;
            button.targetGraphic = arrow;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .77f, .85f);
            colors.pressedColor = new Color(.88f, .62f, .74f);
            colors.disabledColor = new Color(1f, 1f, 1f, .3f);
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }

        void PreviousReaderQuestion()
        {
            if (fixedReader == null || readerPrevious == null) return;
            UnityAction action = readerPrevious;
            readerPrevious = null;
            action();
        }

        void SkipToReaderChoices()
        {
            if (fixedReader == null || readerChoosing) return;
            // Advance only to the available choices or next authored scene.
            // Never select a choice automatically.
            readerPassage = readerPassages.Length - 1;
            readerPage = 1;
            DisplayReaderPassage();
            readerPage = Mathf.Max(1, readerText.textInfo.pageCount);
            readerText.pageToDisplay = readerPage;
            if (readerNext != null)
            {
                UnityAction action = readerNext;
                readerNext = null;
                action();
                return;
            }
            readerChoosing = true;
            readerChoices.gameObject.SetActive(true);
            readerCue.text = "";
            readerAdvance.interactable = false;
            UpdateReaderBack();
        }

        void DisplayReaderPassage()
        {
            readerText.text = readerPassages[readerPassage];
            readerText.pageToDisplay = readerPage;
            Canvas.ForceUpdateCanvases();
            readerText.ForceMeshUpdate();
            UpdateReaderBack();
        }

        void UpdateReaderBack()
        {
            readerFirst.interactable = readerPrevious != null;
            readerBack.interactable = readerChoosing || readerPassage > 0 || readerPage > 1 || readerPrevious != null;
            readerForward.interactable = !readerChoosing;
            readerLast.interactable = !readerChoosing;
        }

        void PreviousReaderText()
        {
            if (fixedReader == null || !readerBack.interactable) return;
            if (readerChoosing)
            {
                // Return from the choices to the last text page without changing a choice.
                readerChoosing = false;
                readerChoices.gameObject.SetActive(false);
                readerAdvance.interactable = true;
                readerCue.text = "";
            }
            else if (readerPage > 1) readerPage--;
            else if (readerPassage > 0)
            {
                readerPassage--;
                readerPage = 1;
                DisplayReaderPassage();
                readerPage = Mathf.Max(1, readerText.textInfo.pageCount);
            }
            else if (readerPrevious != null)
            {
                UnityAction action = readerPrevious;
                readerPrevious = null;
                action();
                return;
            }
            DisplayReaderPassage();
        }

        void AdvanceReaderText()
        {
            if (fixedReader == null || readerChoosing) return;
            readerText.ForceMeshUpdate();
            if (readerPage < readerText.textInfo.pageCount)
            {
                readerText.pageToDisplay = ++readerPage;
                UpdateReaderBack();
                return;
            }
            if (readerPassage + 1 < readerPassages.Length)
            {
                readerPassage++; readerPage = 1; DisplayReaderPassage();
                return;
            }
            if (readerNext != null)
            {
                UnityAction action = readerNext; readerNext = null; action();
                return;
            }
            readerChoosing = true;
            readerChoices.gameObject.SetActive(true);
            readerCue.text = "";
            readerAdvance.interactable = false;
            UpdateReaderBack();
        }

        void UpdateFixedReader()
        {
            if (fixedReader == null) return;
            var size = fixedReader.rect.size;
            if ((size - readerSize).sqrMagnitude > 1)
            {
                readerSize = size;
                // Re-page this passage after orientation/fullscreen changes; never truncate it.
                readerText.fontSize = Mathf.Clamp(size.y * .030f, 16, 23);
                readerPage = 1;
                DisplayReaderPassage();
            }
            var toolbar = fixedReader.Find("Toolbar");
#if !UNITY_WEBGL || UNITY_EDITOR
            var fullscreenLabel = toolbar.Find("Reader fullscreen")?.GetComponentInChildren<TMP_Text>();
            if (fullscreenLabel != null)
                fullscreenLabel.text = RomanceIQFullscreenButton.IsFullscreen() ? "EXIT FULL" : "FULLSCREEN";
#endif
            toolbar.Find("Reader motion").GetComponentInChildren<TMP_Text>().text =
                "MOTION " + (PlayerPrefs.GetInt(VNMotionKey, 1) == 1 ? "ON" : "OFF");
            bool audible = music != null && music.IsAudible;
            if (readerSpeaker.IsOn != audible)
            {
                readerSpeaker.IsOn = audible;
                readerSpeaker.SetVerticesDirty();
            }
        }
    }
}
