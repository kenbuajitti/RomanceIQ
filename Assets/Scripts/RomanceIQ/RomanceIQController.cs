using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace RomanceIQ
{
    public sealed partial class RomanceIQController : MonoBehaviour
    {
        readonly Color ink = new Color(.15f, .09f, .18f);
        readonly Color cream = new Color(.97f, .95f, .88f);
        readonly Color teal = new Color(.48f, .16f, .30f);
        [SerializeField] string storyServerUrl = "http://127.0.0.1:8788";
        StoryResponse liveStory;
        StoryRequest pendingRequest;
        string liveSessionId;
        bool liveBusy;
        StorySetup setup;
        StorySession session = new StorySession();
        TMP_FontAsset font;
        RectTransform safeArea, frame, content;
        ScrollRect scroll;
        TMP_Text progress;
        Button home;
        Rect lastSafeArea;
        Vector2 lastScreen;
        bool showingStory;

        void Start()
        {
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            BuildShell();
            TextAsset json = Resources.Load<TextAsset>("RomanceIQ/Opening");
            try
            {
                setup = json == null ? null : JsonUtility.FromJson<StorySetup>(json.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("RomanceIQ opening could not be read: " + ex.Message);
            }
            if (setup == null || !setup.IsValid())
            {
                home.gameObject.SetActive(false);
                ClearPage("SETUP ERROR");
                Label(content, "Opening data is missing or invalid.", 30, ink);
                Debug.LogError("Check Assets/Resources/RomanceIQ/Opening.json. Five unique choices are required.");
                return;
            }
            BuildSeriesMenu();
            ShowMenu();
            StartCoroutine(ShowIntroduction());
        }

        void BuildShell()
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.localPosition = new Vector3(0, 0, -10);
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = ink;
            }
            var canvasObject = new GameObject("RomanceIQCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            RectTransform background = Panel("Background", canvasObject.transform, ink);
            Stretch(background);

            safeArea = Rect("SafeArea", canvasObject.transform);
            frame = Rect("ReadingFrame", safeArea);
            frame.anchorMin = new Vector2(.5f, 0);
            frame.anchorMax = new Vector2(.5f, 1);
            frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;

            RectTransform header = Rect("Header", frame);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(.5f, 1);
            header.sizeDelta = new Vector2(0, 96);
            var title = Label(header, "ROMANCEIQ", 36, cream, false);
            SetBounds(title.rectTransform, new Vector2(0, 0), new Vector2(.73f, 1), new Vector2(22, 0), new Vector2(-8, 0));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.enableAutoSizing = true;
            title.fontSizeMin = 24;
            title.fontSizeMax = 36;
            home = MakeButton(header, "MENU", ReturnToMenu);
            SetBounds(home.GetComponent<RectTransform>(), new Vector2(.74f, .20f), new Vector2(1, .80f), Vector2.zero, new Vector2(-22, 0));

            RectTransform page = Panel("Page", frame, cream);
            Stretch(page);
            page.offsetMin = new Vector2(14, 104);
            page.offsetMax = new Vector2(-14, -96);
            scroll = page.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            RectTransform viewport = Rect("Viewport", page);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(10, 8);
            viewport.offsetMax = new Vector2(-10, -8);
            viewport.gameObject.AddComponent<RectMask2D>();
            // A raycast surface lets a drag begin anywhere in the reading panel.
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 26, 30);
            layout.spacing = 18;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            progress = Label(frame, "", 22, cream, false);
            SetBounds(progress.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(18, 61), new Vector2(-18, 96));
            progress.alignment = TextAlignmentOptions.Center;
            var note = Label(frame, "ROMANCEIQ  |  Scroll to read more", 18, new Color(.73f, .80f, .78f), false);
            SetBounds(note.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(18, 8), new Vector2(-18, 57));
            note.alignment = TextAlignmentOptions.Center;

            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem));
                events.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            if (FindFirstObjectByType<AudioListener>() == null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            UpdateSafeArea();
        }

        void Update()
        {
            if (safeArea != null && (lastSafeArea != Screen.safeArea
                || lastScreen != new Vector2(Screen.width, Screen.height))) UpdateSafeArea();
        }

        void UpdateSafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            lastScreen = new Vector2(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            safeArea.anchorMin = lastSafeArea.min / lastScreen;
            safeArea.anchorMax = lastSafeArea.max / lastScreen;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            frame.sizeDelta = new Vector2(Mathf.Min(1050, safeArea.rect.width), 0);
            LayoutSeriesMenu();
        }

        void ClearPage(string status)
        {
            CloseFixedReader();
            if (menuRoot != null) menuRoot.gameObject.SetActive(false);
            if (helpRoot != null) helpRoot.gameObject.SetActive(false);
            frame.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            progress.text = status;
        }

        void FinishPage()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        void ShowMenu()
        {
            CloseFixedReader();
            showingStory = false;
            EventSystem.current?.SetSelectedGameObject(null);
            frame.gameObject.SetActive(false);
            helpRoot.gameObject.SetActive(false);
            menuRoot.gameObject.SetActive(true);
            LayoutSeriesMenu();
        }

        void BeginStay()
        {
            session = new StorySession();
            showingStory = true;
            ClearPage("OFFLINE PREVIEW  |  DAY 1  |  DECISION 1");
            home.gameObject.SetActive(true);
            Label(content, "ASHEVILLE / THE MARQUEE", 23, teal);
            Label(content, setup.openingTitle, 40, ink);
            Label(content, setup.opening, 27, ink);
            Label(content, "What do you do?", 30, teal);
            for (int i = 0; i < setup.choices.Length; i++)
            {
                StoryChoice choice = setup.choices[i];
                MakeButton(content, (i + 1) + ".  " + choice.text, () => Choose(choice));
            }
            FinishPage();
        }

        void Choose(StoryChoice choice)
        {
            if (!session.TryRecord(choice)) return;
            showingStory = false;
            ClearPage("OFFLINE PREVIEW  |  ONE SAMPLE DECISION");
            Label(content, "YOUR FIRST MOVE", 23, teal);
            Label(content, choice.text, 36, ink);
            Label(content, choice.previewResponse, 27, ink);
            Label(content, "End of the opening preview", 30, teal);
            Label(content, "This is a written sample response. Return to the menu and select PLAY for the live, twenty-decision story.", 24, ink);
            MakeButton(content, "TRY ANOTHER OPENING CHOICE", BeginStay);
            FinishPage();
        }

        void ShowCast()
        {
            ShowCastMember(0);
        }

        void ShowCastMember(int index)
        {
            showingStory = false;
            ClearPage("MEET THE CHARACTERS");
            if (setup.cast == null || setup.cast.Length == 0) { ShowMenu(); return; }
            index = Mathf.Clamp(index, 0, setup.cast.Length - 1);
            CastMember person = setup.cast[index];
            // Reuse the story reader, including its translucent panel and overflow pagination.
            // Sentence breaks keep introductions to short, click-through passages.
            string introduction = System.Text.RegularExpressions.Regex.Replace(
                person.introduction ?? "", @"(?<=[.!?])\s+", "\n\n");
            OpenFixedReader(person.name, introduction, index,
                new System.Collections.Generic.List<string>(),
                new System.Collections.Generic.List<UnityEngine.Events.UnityAction>(),
                () => { if (index + 1 < setup.cast.Length) ShowCastMember(index + 1); else ShowMenu(); }, true,
                index > 0 ? (UnityEngine.Events.UnityAction)(() => ShowCastMember(index - 1)) : null);
            var navigation = Panel("Character navigation", fixedReader, new Color(.06f, .04f, .09f, .72f));
            SetBounds(navigation, new Vector2(0, 1), Vector2.one,
                new Vector2(0, -108), new Vector2(0, -54));
            var position = Label(navigation, (index + 1) + " / " + setup.cast.Length, 20, Color.white, false);
            SetBounds(position.rectTransform, new Vector2(.30f, 0), new Vector2(.62f, 1), Vector2.zero, Vector2.zero);
            position.alignment = TextAlignmentOptions.Center;

        }

        void ShowHelp()
        {
            helpRoot.gameObject.SetActive(true);
            helpRoot.SetAsLastSibling();
            LayoutSeriesMenu();
        }

        void ReturnToMenu()
        {
            if (liveBusy) return;
            // No story is lost in Step 1 before the single preview choice.
            if (showingStory) session = new StorySession();
            ShowMenu();
        }

        void BeginLive()
        {
            if (liveBusy) return;
            if (pendingRequest != null || (liveStory != null && !liveStory.isEnding))
            {
                ClearPage("BEGIN A NEW STORY?");
                home.gameObject.SetActive(true);
                Label(content, "Start a different version?", 38, ink);
                Label(content, "Beginning again replaces your current story with a fresh opening.", 27, ink);
                MakeButton(content, "BEGIN A NEW STORY", StartNewLive);
                MakeButton(content, "KEEP MY CURRENT STORY", ShowMenu);
                FinishPage();
                return;
            }
            ShowStorySetup();
        }

        void ShowStorySetup()
        {
            ClearPage("THE LAST FIRST NIGHT  |  BEFORE SCENE 1");
            home.gameObject.SetActive(true);
            Label(content, setup.title, 38, ink);
            Label(content, setup.premise, 25, ink);
            foreach (CastMember person in setup.cast)
            {
                Label(content, person.name, 28, teal);
                Label(content, person.introduction, 24, ink);
            }
            Label(content, "You control only Maya. There is no timer or correct partner. Each scene offers five actions; your twentieth choice leads to the ending.", 24, ink);
            MakeButton(content, "START SCENE 1", StartNewLive);
            FinishPage();
        }

        void StartNewLive()
        {
            if (liveBusy) return;
            liveStory = null;
            liveSessionId = System.Guid.NewGuid().ToString("N");
            pendingRequest = new StoryRequest
            {
                sessionId = liveSessionId, requestId = System.Guid.NewGuid().ToString("N"),
                expectedTurn = 0, choiceId = ""
            };
            RetryLive();
        }

        void ChooseLive(LiveChoice choice)
        {
            if (liveBusy || pendingRequest != null || liveStory == null || liveStory.isEnding) return;
            pendingRequest = new StoryRequest
            {
                sessionId = liveSessionId, requestId = System.Guid.NewGuid().ToString("N"),
                expectedTurn = liveStory.completed, choiceId = choice.id
            };
            RetryLive();
        }

        void RetryLive()
        {
            if (liveBusy || pendingRequest == null) return;
            liveBusy = true;
            showingStory = false;
            home.gameObject.SetActive(true);
            home.interactable = false;
            ClearPage(liveStory == null ? "OPENING NIGHT, FIVE DAYS AWAY" :
                "DAY " + liveStory.day + "  |  " + liveStory.completed + " OF 20 DECISIONS MADE");
            Label(content, liveStory == null ? "Your story is beginning..." : "The story continues...", 36, ink);
            Label(content, "Please wait while the next scene is written.", 27, ink);
            FinishPage();
            StartCoroutine(RomanceIQService.Send(storyServerUrl, pendingRequest, (response, error) =>
            {
                liveBusy = false;
                home.interactable = true;
                if (response == null)
                {
                    // Retain the same request ID: a lost response can be recovered
                    // from the server without applying the choice a second time.
                    ClearPage("STORY CONNECTION PAUSED");
                    Label(content, "Your next scene could not be loaded.", 34, ink);
                    Label(content, error, 25, ink);
                    MakeButton(content, "RETRY THE SAME REQUEST", RetryLive);
                    FinishPage();
                    return;
                }
                liveStory = response;
                pendingRequest = null;
                ShowLiveStory();
            }));
        }

        void ShowLiveStory()
        {
            if (liveStory == null) { ShowMenu(); return; }
            showingStory = false;
            home.gameObject.SetActive(true);
            ClearPage(liveStory.isEnding ? "STORY COMPLETE  |  20 OF 20 CHOICES" :
                "DAY " + liveStory.day + " OF 5  |  SCENE " + (liveStory.completed + 1) + " OF 20");
            var options = new System.Collections.Generic.List<UnityAction>();
            var captions = new System.Collections.Generic.List<string>();
            if (liveStory.isEnding)
            {
                captions.Add("BEGIN A NEW STORY"); options.Add(StartNewLive);
            }
            else foreach (var choice in liveStory.choices)
            {
                LiveChoice selected = choice;
                captions.Add(selected.text); options.Add(() => ChooseLive(selected));
            }
            OpenFixedReader(liveStory.title,
                liveStory.narrative + (liveStory.isEnding ? "\n\n" + liveStory.epilogue : ""),
                0, captions, options, null);
        }

        RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        RectTransform Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        TMP_Text Label(Transform parent, string value, float size, Color color, bool layout = true)
        {
            RectTransform rect = Rect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.richText = false;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            if (layout)
            {
                var element = rect.gameObject.AddComponent<LayoutElement>();
                element.minHeight = size * 1.4f;
            }
            return text;
        }

        Button MakeButton(Transform parent, string caption, UnityAction action)
        {
            RectTransform rect = Panel("ChoiceButton", parent, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(.78f, .88f, .86f);
            colors.pressedColor = new Color(.62f, .75f, .72f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(action);
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 64;
            var padding = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            padding.padding = new RectOffset(18, 18, 12, 12);
            padding.childControlHeight = padding.childControlWidth = true;
            padding.childForceExpandHeight = padding.childForceExpandWidth = true;
            var text = Label(rect, caption, 23, Color.black);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            SetBounds(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        static void SetBounds(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = low;
            rect.offsetMax = high;
        }
    }
}
