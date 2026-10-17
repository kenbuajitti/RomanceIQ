using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RomanceIQ
{
    [Serializable] public sealed class VNChoice { public string id, text, next; }
    [Serializable] public sealed class VNNode
    {
        public string id, speaker, text, next;
        public int focus;
        public bool ending;
        public VNChoice[] choices;
    }
    [Serializable] public sealed class VNStory { public string storyId, version, start; public VNNode[] nodes; }
    [Serializable] public sealed class VNRun
    {
        public string runId, storyId, version, nodeId, startedUtc, completedUtc;
        public List<string> path = new List<string>();
        public bool completed, surveyHandled;
    }
    [Serializable] public sealed class VNFeedback
    {
        public string runId, storyId, version, nodeId, path, submittedUtc, comment;
        public int enjoyment, ending, agency;
    }
    [Serializable] public sealed class VNArchive
    {
        public List<VNRun> runs = new List<VNRun>();
        public List<VNFeedback> feedback = new List<VNFeedback>();
    }

    public sealed partial class RomanceIQController
    {
        const string VNSaveKey = "RomanceIQ.VN.Run.v1";
        const string VNArchiveKey = "RomanceIQ.VN.Archive.v1";
        const string VNMotionKey = "RomanceIQ.VN.Motion.v1";
        VNStory vnStory;
        VNRun vnRun;
        readonly Dictionary<string, VNNode> vnNodes = new Dictionary<string, VNNode>();
        bool vnTransition;
        Texture2D vnCast, vnBackground;
        int ratingEnjoyment, ratingEnding, ratingAgency;

        void BeginVisualNovel()
        {
            try
            {
                if (vnStory == null)
                {
                    var asset = Resources.Load<TextAsset>("RomanceIQ/VisualNovel/Story");
                    if (asset == null) throw new Exception("Visual novel Story.json is missing.");
                    vnStory = JsonUtility.FromJson<VNStory>(asset.text);
                    vnNodes.Clear();
                    foreach (var n in vnStory.nodes) vnNodes.Add(n.id, n);
                    vnCast = Resources.Load<Texture2D>("RomanceIQ/VisualNovel/Cast");
                    vnBackground = Resources.Load<Texture2D>("RomanceIQ/VisualNovel/Lobby");
                }
                VNRun saved = null;
                try { saved = JsonUtility.FromJson<VNRun>(PlayerPrefs.GetString(VNSaveKey, "")); }
                catch { /* An incompatible local preview save starts a new run. */ }
                if (saved != null && (saved.version == vnStory.version || saved.version == "vn-poc-1") && saved.storyId == vnStory.storyId
                    && vnNodes.ContainsKey(saved.nodeId) && saved.path != null)
                {
                    vnRun = saved;
                    vnRun.version = vnStory.version;
                    if (!vnNodes[vnRun.nodeId].ending)
                    { vnRun.completed = false; vnRun.completedUtc = null; vnRun.surveyHandled = false; }
                    SaveVNRun();
                }
                else NewVNRun();
                ShowVNNode();
            }
            catch (Exception ex)
            {
                ClearPage("VISUAL NOVEL SETUP");
                Label(content, "The preview assets could not load.", 30, ink);
                Label(content, ex.Message, 22, ink);
                FinishPage();
                Debug.LogException(ex);
            }
        }

        void NewVNRun()
        {
            vnRun = new VNRun { runId = Guid.NewGuid().ToString("N"), storyId = vnStory.storyId,
                version = vnStory.version, nodeId = vnStory.start, startedUtc = DateTime.UtcNow.ToString("o") };
            SaveVNRun();
        }

        void SaveVNRun()
        {
            PlayerPrefs.SetString(VNSaveKey, JsonUtility.ToJson(vnRun));
            var archive = ReadVNArchive();
            int index = archive.runs.FindIndex(r => r.runId == vnRun.runId);
            // Snapshot, rather than holding a mutable reference to the current run.
            var snapshot = JsonUtility.FromJson<VNRun>(JsonUtility.ToJson(vnRun));
            if (index < 0) archive.runs.Add(snapshot); else archive.runs[index] = snapshot;
            while (archive.runs.Count > 40) archive.runs.RemoveAt(0);
            PlayerPrefs.SetString(VNArchiveKey, JsonUtility.ToJson(archive));
            PlayerPrefs.Save();
        }

        VNArchive ReadVNArchive()
        {
            try
            {
                var value = JsonUtility.FromJson<VNArchive>(PlayerPrefs.GetString(VNArchiveKey, ""));
                if (value != null && value.runs != null && value.feedback != null) return value;
            }
            catch { }
            return new VNArchive();
        }

        void ShowVNNode()
        {
            vnTransition = false;
            showingStory = false;
            var node = vnNodes[vnRun.nodeId];
            ClearPage("THE LAST FIRST NIGHT  |  " + (node.ending ? "CHAPTER COMPLETE" : "THE OPENING CHAPTER"));
            home.gameObject.SetActive(true);
            var captions = new List<string>();
            var actions = new List<UnityEngine.Events.UnityAction>();
            if (node.ending)
            {
                if (!vnRun.completed)
                {
                    vnRun.completed = true;
                    vnRun.completedUtc = DateTime.UtcNow.ToString("o");
                    SaveVNRun();
                }
                if (!vnRun.surveyHandled)
                {
                    captions.Add("OPTIONAL: RATE THIS STORY"); actions.Add(ShowVNSurvey);
                }
                captions.Add("COPY SAVED FEEDBACK"); actions.Add(ShowVNFeedbackExport);
                captions.Add("PLAY A DIFFERENT PATH");
                actions.Add(() => { NewVNRun(); ShowVNNode(); });
            }
            else if (node.choices != null) foreach (var option in node.choices)
            {
                VNChoice selected = option;
                captions.Add(selected.text);
                actions.Add(() => AdvanceVN(node.id, selected.next, selected.id));
            }
            OpenFixedReader(node.speaker, node.text, node.focus, captions, actions,
                node.ending || captions.Count > 0 ? (UnityEngine.Events.UnityAction)null
                    : () => AdvanceVN(node.id, node.next, null), false,
                vnRun.path.Count > 0 ? (UnityEngine.Events.UnityAction)BackVNQuestion : null);
            var navigation = Rect("Question navigation", fixedReader);
            SetBounds(navigation, new Vector2(0, 1), Vector2.one, new Vector2(10, -104), new Vector2(-10, -60));
            var position = Label(navigation, node.ending ? "CHAPTER COMPLETE" :
                "CHOICES MADE: " + vnRun.path.Count + " / 6", 18, Color.white, false);
            SetBounds(position.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), Vector2.zero);
            position.enableAutoSizing = true; position.fontSizeMin = 12; position.fontSizeMax = 18;
            position.alignment = TextAlignmentOptions.MidlineLeft;
        }

        void AdvanceVN(string from, string next, string choice)
        {
            if (vnTransition || vnRun.nodeId != from || !vnNodes.ContainsKey(next)) return;
            vnTransition = true;
            if (choice != null) vnRun.path.Add(from + ":" + choice);
            vnRun.nodeId = next;
            SaveVNRun();
            ShowVNNode();
        }

        void BackVNQuestion()
        {
            if (vnTransition || vnRun == null || vnRun.path.Count == 0) return;
            string entry = vnRun.path[vnRun.path.Count - 1];
            int separator = entry.IndexOf(':');
            if (separator < 1) return;
            string target = entry.Substring(0, separator);
            if (!vnNodes.ContainsKey(target)) return;
            // Keep the previously visited path and its feedback intact in the archive.
            // A rewind starts a new branch from the retained prefix, not a new story.
            vnRun = JsonUtility.FromJson<VNRun>(JsonUtility.ToJson(vnRun));
            vnRun.runId = Guid.NewGuid().ToString("N");
            vnRun.path.RemoveAt(vnRun.path.Count - 1);
            vnRun.nodeId = target;
            vnRun.completed = false; vnRun.completedUtc = null; vnRun.surveyHandled = false;
            SaveVNRun();
            ShowVNNode();
        }

        void BuildVNStage(int focus, RectTransform parent, bool singlePortrait = false)
        {
            var stage = Panel("Illustrated stage", parent, new Color(.20f, .15f, .24f));
            Stretch(stage);
            if (singlePortrait)
                SetBounds(stage, new Vector2(0, .30f), Vector2.one, Vector2.zero, new Vector2(0, -108));
            stage.gameObject.AddComponent<RectMask2D>();
            var element = stage.gameObject.AddComponent<LayoutElement>();
            var backdrop = Rect("Marquee lobby", stage).gameObject.AddComponent<RawImage>();
            backdrop.texture = vnBackground;
            backdrop.raycastTarget = false;
            Stretch(backdrop.rectTransform);
            var characters = new RectTransform[singlePortrait ? 1 : 3];
            for (int slot = 0; slot < characters.Length; slot++)
            {
                int i = singlePortrait ? Mathf.Clamp(focus, 0, 2) : slot;
                var portrait = Rect(new [] { "Maya", "Noah", "Eli" }[i], stage).gameObject.AddComponent<RawImage>();
                portrait.texture = vnCast;
                float[] edges = { 0, 468f / 1535f, 1024f / 1535f, 1 };
                portrait.uvRect = new UnityEngine.Rect(edges[i], 0, edges[i + 1] - edges[i], 1);
                portrait.raycastTarget = false;
                portrait.color = i == focus ? Color.white : new Color(.82f, .80f, .88f, 1);
                characters[slot] = portrait.rectTransform;
                characters[slot].anchorMin = characters[slot].anchorMax = new Vector2((slot + .5f) / characters.Length, 0);
                characters[slot].pivot = new Vector2(.5f, 0);
            }
            var motion = stage.gameObject.AddComponent<RomanceIQStageMotion>();
            motion.Configure(element, backdrop, characters, vnCast, singlePortrait ? 0 : focus, PlayerPrefs.GetInt(VNMotionKey, 1) == 1, true);
            if (vnCast == null || vnBackground == null) Label(stage, "Illustration assets missing — reimport the preview artwork.", 18, Color.white, false);
        }

        void ShowVNSurvey()
        {
            if (!vnRun.completed || vnRun.surveyHandled) return;
            ratingEnjoyment = ratingEnding = ratingAgency = 0;
            ClearPage("OPTIONAL STORY FEEDBACK");
            Label(content, "How did this chapter feel?", 34, ink);
            Label(content, "A few taps help shape the novel. Feedback is saved on this device only. After saving, copy it to share with the developer.", 22, ink);
            AddVNRating("Enjoyment", new [] { "1", "2", "3", "4", "5" }, value => ratingEnjoyment = value);
            AddVNRating("Satisfaction with this chapter’s ending", new [] { "1", "2", "3", "4", "5" }, value => ratingEnding = value);
            AddVNRating("Did your choices matter?", new [] { "Not really", "Somewhat", "Very much" }, value => ratingAgency = value);
            Label(content, "What would you change? (optional)", 22, teal);
            var fieldRoot = Panel("Comment", content, Color.white);
            fieldRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = 130;
            var field = fieldRoot.gameObject.AddComponent<TMP_InputField>();
            var fieldViewport = Rect("Text viewport", fieldRoot);
            Stretch(fieldViewport); fieldViewport.offsetMin = new Vector2(14, 10); fieldViewport.offsetMax = new Vector2(-14, -10);
            fieldViewport.gameObject.AddComponent<RectMask2D>();
            var text = Label(fieldViewport, "", 22, ink, false);
            Stretch(text.rectTransform);
            var placeholder = Label(fieldViewport, "A moment you loved, or something that felt wrong…", 22, new Color(.5f, .45f, .5f), false);
            Stretch(placeholder.rectTransform);
            field.textViewport = fieldViewport;
            field.textComponent = (TextMeshProUGUI)text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.characterLimit = 600;
            var message = Label(content, "1 = low, 5 = high. All feedback is optional.", 19, teal);
            MakeButton(content, "SAVE FEEDBACK", () => {
                if (vnRun.surveyHandled) return;
                if (ratingEnjoyment == 0 && ratingEnding == 0 && ratingAgency == 0 && string.IsNullOrWhiteSpace(field.text))
                { message.text = "Choose a rating, add a comment, or select Skip."; return; }
                var archive = ReadVNArchive();
                archive.feedback.Add(new VNFeedback { runId = vnRun.runId, storyId = vnRun.storyId, version = vnRun.version,
                    nodeId = vnRun.nodeId, path = string.Join("/", vnRun.path), submittedUtc = DateTime.UtcNow.ToString("o"),
                    enjoyment = ratingEnjoyment, ending = ratingEnding, agency = ratingAgency, comment = field.text.Trim() });
                while (archive.feedback.Count > 40) archive.feedback.RemoveAt(0);
                PlayerPrefs.SetString(VNArchiveKey, JsonUtility.ToJson(archive));
                vnRun.surveyHandled = true; SaveVNRun();
                ClearPage("THANK YOU");
                Label(content, "Your feedback has been saved on this device.", 30, ink);
                MakeButton(content, "COPY FEEDBACK TO SHARE", ShowVNFeedbackExport);
                MakeButton(content, "READ MY ENDING", ShowVNNode);
                MakeButton(content, "PLAY A DIFFERENT PATH", () => { NewVNRun(); ShowVNNode(); });
                FinishPage();
            });
            MakeButton(content, "SKIP", () => { vnRun.surveyHandled = true; SaveVNRun(); ShowVNNode(); });
            FinishPage();
        }

        void ShowVNFeedbackExport()
        {
            ClearPage("SHARE PROTOTYPE FEEDBACK");
            Label(content, "Select the text below, copy it, and share it with the developer. Nothing is sent automatically.", 24, ink);
            var root = Panel("Feedback text", content, Color.white);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 300;
            var field = root.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Text viewport", root);
            Stretch(viewport); viewport.offsetMin = new Vector2(12, 12); viewport.offsetMax = new Vector2(-12, -12);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label(viewport, "", 18, ink, false);
            Stretch(text.rectTransform);
            field.textViewport = viewport; field.textComponent = (TextMeshProUGUI)text;
            field.lineType = TMP_InputField.LineType.MultiLineNewline; field.readOnly = true;
            // Share only this branch's voluntary response, not the device's entire history.
            var feedback = ReadVNArchive().feedback.FindLast(item => item.runId == vnRun.runId);
            field.text = feedback == null ? "No feedback saved for this path yet." : JsonUtility.ToJson(feedback, true);
            MakeButton(content, "READ MY ENDING", ShowVNNode);
            FinishPage();
        }

        void AddVNRating(string title, string[] labels, Action<int> select)
        {
            Label(content, title, 24, teal);
            var row = Rect("Rating", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 8; group.childControlHeight = group.childControlWidth = true;
            group.childForceExpandWidth = true;
            var buttons = new List<Button>();
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var button = FlatButton(row, labels[i], () => {
                    select(index + 1);
                    for (int j = 0; j < buttons.Count; j++)
                    {
                        buttons[j].GetComponent<Image>().color = j == index ? teal : Color.white;
                        buttons[j].GetComponentInChildren<TMP_Text>().color = j == index ? Color.white : ink;
                    }
                });
                button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                buttons.Add(button);
            }
        }
    }
}
