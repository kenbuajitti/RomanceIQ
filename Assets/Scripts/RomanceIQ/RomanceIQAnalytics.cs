using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UnityConsent;

namespace RomanceIQ
{
    // Consent-based analytics; ask again on each new game visit, even for returning players.
    // Records only the six event schemas configured for RomanceIQ, without story text.
    public sealed class RomanceIQAnalytics : MonoBehaviour
    {
        // Do not carry analytics consent between visits. Older builds stored a choice in
        // PlayerPrefs; that setting is deliberately ignored and deleted on startup.
        const string LegacyConsentPreference = "RomanceIQ.AnalyticsChoice";
        static int visitChoice = 1; // -1 not asked, 0 declined, 1 allowed
        static RomanceIQAnalytics instance;
        readonly Queue<CustomEvent> pending = new Queue<CustomEvent>();
        readonly HashSet<int> reachedStages = new HashSet<int>();
        bool initialized;
        bool initializing;
        bool menuSeen;
        bool loadRecorded;
        bool playing;
        float playStartTime;
        string trackedRunId;

        public static int Choice => visitChoice;
        public static bool Allowed => Choice == 1;

        static void Log(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[RomanceIQ Analytics] " + message);
#endif
        }

        // Also reset in Editor play mode when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetVisitConsent() { visitChoice = 1; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() { GetInstance(); }

        static RomanceIQAnalytics GetInstance()
        {
            if (instance != null) return instance;
            var obj = new GameObject("RomanceIQ Analytics");
            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<RomanceIQAnalytics>();
            return instance;
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            // Even if a returning visitor previously allowed analytics, do not
            // initialize Unity Analytics before they consent on this visit.
            if (PlayerPrefs.HasKey(LegacyConsentPreference))
            {
                PlayerPrefs.DeleteKey(LegacyConsentPreference);
                PlayerPrefs.Save();
            }
            ApplyConsent(true);
           // Log("Analytics off until consent is requested for this visit.");
        }

        public static void MenuShown()
        {
            var service = GetInstance();
            service.menuSeen = true;
            service.RecordLoadIfNeeded();
            if (Allowed && !service.initialized && !service.initializing)
                service.Initialize();
        }

        public static void SetPlayerChoice(bool allowed)
        {
            allowed = true;
            var service = GetInstance();
            visitChoice = allowed ? 1 : 0;
            service.ApplyConsent(allowed);
            if (allowed)
            {
                if (!service.initialized) service.Initialize();
                service.RecordLoadIfNeeded();
            }
            else
            {
                service.pending.Clear();
                service.playing = false;
                Log("Analytics disabled; unsent events cleared.");
            }
        }

        void ApplyConsent(bool allowed)
        {
            try
            {
                EndUserConsent.SetConsentState(new ConsentState
                {
                    AnalyticsIntent = allowed ? ConsentStatus.Granted : ConsentStatus.Denied,
                    AdsIntent = ConsentStatus.Denied
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RomanceIQ Analytics] Consent update error: " + ex.Message);
            }
        }

        async void Initialize()
        {
            if (initialized || initializing || !Allowed) return;
            initializing = true;
            try
            {
                await UnityServices.InitializeAsync();
                initialized = true;
                ApplyConsent(Allowed);
                if (!Allowed) { pending.Clear(); return; }
                RecordLoadIfNeeded();
                while (pending.Count > 0) AnalyticsService.Instance.RecordEvent(pending.Dequeue());
                AnalyticsService.Instance.Flush();
                Log("Analytics initialized; queued events submitted to SDK.");
            }
            catch (Exception ex)
            {
                // Keep events queued for retry; a failed initialization should
                // not permanently erase the only romanceiq_loaded event.
                Debug.LogWarning("[RomanceIQ Analytics] Initialization failed; retry on focus/menu: " + ex);
            }
            finally { initializing = false; }
        }

        void RecordLoadIfNeeded()
        {
            if (!Allowed || !menuSeen || loadRecorded) return;
            loadRecorded = true;
            Send("romanceiq_loaded", new CustomEvent("romanceiq_loaded"), true);
        }

        static void Send(string name, CustomEvent evt, bool flush = false)
        {
            var service = GetInstance();
            if (!Allowed) return;
            if (!service.initialized)
            {
                service.pending.Enqueue(evt);
                Log("Queued " + name + " until SDK initialization.");
                service.Initialize();
                return;
            }
            try
            {
                AnalyticsService.Instance.RecordEvent(evt);
                if (flush) AnalyticsService.Instance.Flush();
                Log("Recorded " + name + " in SDK (server delivery not yet confirmed).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RomanceIQ Analytics] Unable to record " + name + ": " + ex.Message);
            }
        }

        // A "stage" means decisions completed + 1: 1 on entry, 4 after the authored opening.
        // Resuming a saved story is a new play session, but does not replay historical choices.
        public static void EnsureStoryStarted()
        {
            if (!GetInstance().playing) StoryStarted();
        }

        public static void StoryStarted()
        {
            var service = GetInstance();
            service.playing = true;
            service.playStartTime = Time.realtimeSinceStartup;
            service.reachedStages.Clear();
            service.trackedRunId = null;
            Send("romanceiq_started", new CustomEvent("romanceiq_started"), true);
        }

        public static void StageReached(int stage, string runId)
        {
            var service = GetInstance();
            if (!service.playing) return;
            if (service.trackedRunId != runId)
            {
                service.trackedRunId = runId;
                service.reachedStages.Clear();
            }
            if (!service.reachedStages.Add(stage)) return;
            Send("romanceiq_stage_reached", new CustomEvent("romanceiq_stage_reached") { { "stage", stage } }, true);
        }

        public static void ChoiceMade(int stage, string choiceId)
        {
            if (!GetInstance().playing) return;
            Send("romanceiq_choice_made", new CustomEvent("romanceiq_choice_made")
            {
                { "stage", stage },
                { "choice_id", choiceId }
            }, true);
        }

        public static void StoryCompleted(string endingId)
        {
            var service = GetInstance();
            if (!service.playing) return;
            Send("romanceiq_completed", new CustomEvent("romanceiq_completed") { { "ending_id", endingId } }, true);
            service.playing = false;
        }

        // A deliberate exit is reliably observable; tab closure may not deliver events in WebGL.
        public static void StoryAbandoned(int stage)
        {
            var service = GetInstance();
            if (!service.playing) return;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(Time.realtimeSinceStartup - service.playStartTime));
            Send("romanceiq_abandoned", new CustomEvent("romanceiq_abandoned")
            {
                { "stage", stage },
                { "seconds_played", seconds }
            }, true);
            service.playing = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused && Allowed && !initialized && !initializing)
                Initialize();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        void OnApplicationQuit()
        {
            // Best-effort only: browsers may shut down WebGL before asynchronous delivery.
            if (playing) StoryAbandoned(GetCurrentStage());
            Flush();
        }

        int currentStage = 1;
        public static void CurrentStage(int stage) { GetInstance().currentStage = stage; }
        int GetCurrentStage() { return currentStage; }

        void Flush()
        {
            if (!initialized || !Allowed) return;
            try { AnalyticsService.Instance.Flush(); }
            catch (Exception ex) { Log("Flush failed: " + ex.Message); }
        }
    }
}
