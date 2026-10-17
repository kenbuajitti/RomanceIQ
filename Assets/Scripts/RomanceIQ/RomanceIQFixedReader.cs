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
        Button readerAdvance, readerBack;
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

            var shade = Panel("Toolbar", fixedReader, new Color(.06f, .04f, .09f, .72f));
            SetBounds(shade, new Vector2(0, 1), Vector2.one, new Vector2(0, -54), Vector2.zero);
            var menu = FlatButton(shade, "MENU", ShowMenu);
            SetBounds(menu.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0, 1), new Vector2(10, 6), new Vector2(108, -6));
            var motionButton = FlatButton(shade, "", () => {
                bool enabled = PlayerPrefs.GetInt(VNMotionKey, 1) != 1;
                PlayerPrefs.SetInt(VNMotionKey, enabled ? 1 : 0); PlayerPrefs.Save();
                foreach (var stage in fixedReader.GetComponentsInChildren<RomanceIQStageMotion>()) stage.SetMotion(enabled);
            });
            SetBounds(motionButton.GetComponent<RectTransform>(), new Vector2(1, 0), Vector2.one, new Vector2(-242, 6), new Vector2(-114, -6));
            motionButton.name = "Reader motion";
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

            var dialogue = Panel("Dialogue — click to advance", fixedReader, new Color(.045f, .025f, .065f, .91f));
            SetBounds(dialogue, Vector2.zero, new Vector2(1, .30f), new Vector2(12, 12), new Vector2(-12, 0));
            readerAdvance = dialogue.gameObject.AddComponent<Button>();
            readerAdvance.targetGraphic = dialogue.GetComponent<Image>();
            readerAdvance.transition = Selectable.Transition.None;
            readerAdvance.onClick.AddListener(AdvanceReaderText);
            var speakerName = Label(dialogue, name, 22, new Color(1, .68f, .76f), false);
            SetBounds(speakerName.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(22, -42), new Vector2(-22, -10));
            speakerName.enableAutoSizing = true; speakerName.fontSizeMin = 14; speakerName.fontSizeMax = 22;
            readerText = Label(dialogue, "", 26, Color.white, false);
            SetBounds(readerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(22, 44), new Vector2(-22, -46));
            readerText.overflowMode = TextOverflowModes.Page;
            readerText.enableAutoSizing = false;
            readerCue = Label(dialogue, "›", 22, new Color(1, .68f, .76f), false);
            SetBounds(readerCue.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(22, 3), new Vector2(-22, 27));
            readerCue.alignment = TextAlignmentOptions.Right;

            var backArea = Panel("Previous text", dialogue, Color.clear);
            SetBounds(backArea, Vector2.zero, Vector2.zero, new Vector2(8, 0), new Vector2(60, 44));
            readerBack = backArea.gameObject.AddComponent<Button>();
            readerBack.targetGraphic = backArea.GetComponent<Image>();
            readerBack.transition = Selectable.Transition.None;
            readerBack.onClick.AddListener(PreviousReaderText);
            var backCue = Label(backArea, "‹", 22, new Color(1, .68f, .76f), false);
            Stretch(backCue.rectTransform);
            backCue.alignment = TextAlignmentOptions.Center;

            readerChoices = Panel("Choices", fixedReader, new Color(.045f, .025f, .065f, .88f));
            SetBounds(readerChoices, new Vector2(.06f, .33f), new Vector2(.94f, 1), Vector2.zero, new Vector2(0, -116));
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
            readerBack.interactable = readerChoosing || readerPassage > 0 || readerPage > 1 || readerPrevious != null;
            readerBack.GetComponentInChildren<TMP_Text>().color =
                new Color(1, .68f, .76f, readerBack.interactable ? 1f : .25f);
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
                readerCue.text = "›";
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
                readerText.fontSize = Mathf.Clamp(size.y * .035f, 18, 26);
                readerPage = 1;
                DisplayReaderPassage();
            }
            var toolbar = fixedReader.Find("Toolbar");
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
