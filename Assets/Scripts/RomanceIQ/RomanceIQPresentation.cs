using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace RomanceIQ
{
    public sealed partial class RomanceIQController
    {
        RectTransform menuRoot, menuStack, menuCover, helpRoot, helpCard, introRoot, introCover;
        IQMusic music;
        IQSpeakerGraphic speaker;
        Texture2D coverTexture;
        TMP_Text menuDescription;
        readonly System.Collections.Generic.List<Button> menuButtons = new System.Collections.Generic.List<Button>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void RomanceIQOpenGames();
#endif

        void BuildSeriesMenu()
        {
            coverTexture = Resources.Load<Texture2D>("RomanceIQ/MenuLandscape");
            music = IQMusic.GetPlayer();
            menuRoot = Panel("RomanceIQ Menu", safeArea, new Color(.025f, .04f, .10f));
            Stretch(menuRoot);
            menuCover = CreateCover(menuRoot);
            menuStack = Panel("Menu buttons", menuRoot, new Color(.015f, .025f, .07f, .78f));
            menuDescription = Label(menuStack,
                "Three hearts. One opening night. Your choices.", 25, Color.white, false);
            menuDescription.alignment = TextAlignmentOptions.Center;
            menuDescription.enableAutoSizing = true;
            menuDescription.fontSizeMin = 18;
            menuDescription.fontSizeMax = 25;
            MenuButton("PLAY", BeginVisualNovel);
            MenuButton("HOW TO PLAY", ShowHelp);
            MenuButton("MEET THE CHARACTERS", ShowCast);
            MenuButton("ALL IQ GAMES", OpenAllGames);
            MenuButton("GAMEPLAY ANALYTICS", ShowAnalyticsOptions);

            // This control remains available on both menu and reading screens.
            RectTransform soundRect = Panel("Sound off - click to enable", safeArea, Color.clear);
            soundRect.anchorMin = soundRect.anchorMax = soundRect.pivot = new Vector2(1, 0);
            soundRect.anchoredPosition = new Vector2(-10, 10);
            soundRect.sizeDelta = new Vector2(52, 48);
            var soundButton = soundRect.gameObject.AddComponent<Button>();
            soundButton.targetGraphic = soundRect.GetComponent<Image>();
            soundButton.transition = Selectable.Transition.None;
            RectTransform icon = Rect("Speaker", soundRect);
            Stretch(icon);
            speaker = icon.gameObject.AddComponent<IQSpeakerGraphic>();
            speaker.color = Color.white;
            speaker.raycastTarget = false;
            soundButton.onClick.AddListener(music.ToggleSound);

            helpRoot = Panel("How to Play overlay", safeArea, new Color(0, 0, 0, .66f));
            Stretch(helpRoot);
            helpCard = Panel("Instructions", helpRoot, new Color(.035f, .055f, .11f, .98f));
            var heading = Label(helpCard, "HOW TO PLAY", 30, Color.white, false);
            heading.alignment = TextAlignmentOptions.Center;
            SetBounds(heading.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(20, -66), new Vector2(-20, -20));
            var body = Label(helpCard,
                "Click or tap the dialogue box to advance the text. Choices appear after the scene is read. There is no timer.\n\n" +
                "You are Maya Chen, 25. You choose her actions; Noah and Eli have their own plans.\n\n" +
                "PLAY opens an illustrated branching romance. Three prewritten decisions establish your story; later decisions continue online. Shared paths reuse saved story scenes, while your personal progress stays on this device. MEET THE CHARACTERS introduces the three leads.\n\n" +
                "Navigation: << goes to the previous decision, < rereads the previous text, > advances text, and >> skips to the scene choices without selecting one. You can also click the dialogue to advance. Your progress saves automatically on this device. The online continuation requires a network connection. When the story concludes, you can rate it and copy your feedback to share.",
                24, Color.white, false);
            body.enableAutoSizing = true;
            body.fontSizeMin = 16;
            body.fontSizeMax = 24;
            SetBounds(body.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(28, 92), new Vector2(-28, -82));
            Button close = FlatButton(helpCard, "CLOSE", () => helpRoot.gameObject.SetActive(false));
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, 0);
            closeRect.pivot = new Vector2(.5f, 0);
            closeRect.anchoredPosition = new Vector2(0, 24);
            closeRect.sizeDelta = new Vector2(220, 44);
            helpRoot.gameObject.SetActive(false);
            LayoutSeriesMenu();
        }

        RectTransform CreateCover(Transform parent)
        {
            RectTransform rect = Rect("Cover", parent);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = coverTexture;
            image.raycastTarget = false;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        Button FlatButton(Transform parent, string caption, UnityAction action)
        {
            RectTransform rect = Panel(caption, parent, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(.87f, .91f, 1);
            colors.pressedColor = new Color(.70f, .78f, .9f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(action);
            var text = Label(rect, caption, 22, Color.black, false);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10, 3);
            text.rectTransform.offsetMax = new Vector2(-10, -3);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = 22;
            return button;
        }

        Button MenuButton(string caption, UnityAction action)
        {
            Button button = FlatButton(menuStack, caption, action);
            menuButtons.Add(button);
            return button;
        }

        void LayoutSeriesMenu()
        {
            if (menuRoot == null) return;
            float w = safeArea.rect.width, h = safeArea.rect.height;
            if (w <= 0 || h <= 0) return;
            bool wide = w / h >= 1.25f;
            // Preserve the entire title and cast; tall screens place controls below the art.
            FitCover(menuCover, w, h, true);
            if (introCover != null) FitCover(introCover, w, h, false);
            int count = 0;
            foreach (var button in menuButtons) if (button.gameObject.activeSelf) count++;
            int columns = wide ? 3 : 1;
            int rows = Mathf.CeilToInt(count / (float)columns);
            float bh = 46;
            const float gap = 8, headingHeight = 58, padding = 12;
            float height = headingHeight + rows * (bh + gap) + padding;
            float width = wide ? Mathf.Min(1000, w - 144) : Mathf.Min(430, w - 36);
            menuStack.anchorMin = menuStack.anchorMax = new Vector2(.5f, 0);
            menuStack.pivot = new Vector2(.5f, 0);
            float bottom = wide ? 16 : 66;
            if (!wide && coverTexture != null)
            {
                float aspect = (float)coverTexture.width / coverTexture.height;
                float artHeight = Mathf.Min(w / aspect, Mathf.Max(1, h - height - 78));
                menuCover.sizeDelta = new Vector2(artHeight * aspect, artHeight);
                menuCover.anchoredPosition = new Vector2(0, (h - artHeight) * .5f);
                bottom = Mathf.Max(bottom, (h - artHeight - height) * .5f);
            }
            menuStack.anchoredPosition = new Vector2(0, bottom);
            menuStack.sizeDelta = new Vector2(width, height);
            SetBounds(menuDescription.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(16, -headingHeight + 4), new Vector2(-16, -8));
            float buttonWidth = (width - 2 * padding - (columns - 1) * gap) / columns;
            int index = 0;
            foreach (var button in menuButtons)
            {
                if (!button.gameObject.activeSelf) continue;
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(buttonWidth, bh);
                rect.anchoredPosition = new Vector2(padding + (index % columns) * (buttonWidth + gap),
                    -headingHeight - (index / columns) * (bh + gap));
                index++;
            }
            helpCard.anchorMin = helpCard.anchorMax = helpCard.pivot = new Vector2(.5f, .5f);
            helpCard.anchoredPosition = Vector2.zero;
            helpCard.sizeDelta = new Vector2(Mathf.Min(790, w - 40), Mathf.Min(570, h - 48));
        }

        void FitCover(RectTransform rect, float w, float h, bool top)
        {
            if (coverTexture == null) return;
            float aspect = (float)coverTexture.width / coverTexture.height;
            float width = Mathf.Min(w, h * aspect);
            rect.sizeDelta = new Vector2(width, width / aspect);
            rect.anchoredPosition = new Vector2(0, top ? (h - width / aspect) * .5f : 0);
        }

        IEnumerator ShowIntroduction()
        {
            // Open directly at the playable story menu.
            yield break;
        }

        void LateUpdate()
        {
            UpdateFixedReader();
            UpdateReadingArrow();
            if (speaker == null || music == null) return;
            bool audible = music.IsAudible;
            if (speaker.IsOn != audible) { speaker.IsOn = audible; speaker.SetVerticesDirty(); }
            speaker.transform.parent.name = audible ? "Sound on - click to mute" : "Sound off - click to enable";
        }

        void ShowAnalyticsOptions()
        {
            ClearPage("GAMEPLAY ANALYTICS");
            home.gameObject.SetActive(true);
            Label(content, "OPTIONAL GAMEPLAY ANALYTICS", 33, ink);
            Label(content,
                "Help improve RomanceIQ by sharing basic gameplay events: game launches, story starts, " +
                "decision numbers and choice IDs, progress, finishes, and deliberate exits. " +
                "No story text, survey comments, or names are sent. " +
                "Unity Analytics may process pseudonymous identifiers and device information. " +
                "Analytics is OFF until you choose Allow.", 23, ink);
            Label(content, "Current setting: " + (RomanceIQAnalytics.Choice == 1 ? "ON" :
                RomanceIQAnalytics.Choice == 0 ? "OFF" : "NOT SELECTED"), 25, teal);
            MakeButton(content, "ALLOW GAMEPLAY ANALYTICS", () => {
                RomanceIQAnalytics.SetPlayerChoice(true);
                ShowMenu();
            });
            MakeButton(content, "DECLINE / TURN OFF ANALYTICS", () => {
                RomanceIQAnalytics.SetPlayerChoice(false);
                ShowMenu();
            });
            MakeButton(content, "BACK WITHOUT CHANGING", ShowMenu);
            FinishPage();
        }

        void OpenAllGames()
        {
            RomanceIQAnalytics.StoryAbandoned(vnRun == null ? 1 : vnRun.path.Count + 1);
#if UNITY_WEBGL && !UNITY_EDITOR
            RomanceIQOpenGames();
#else
            Application.OpenURL("https://iqgamesonline.com/?iqreturn=1&game=RomanceIQ");
#endif
        }
    }
}
