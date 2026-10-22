# RomanceIQ — gameplay analytics integration

This Unity project includes opt-in Unity Gaming Services Analytics wired to the **illustrated visual novel reached from PLAY** in RomanceIQ. It does not alter StoryIQ / Last Resort and does not instrument unused legacy twenty-choice live-story code.

## Unity Cloud project check

The original RomanceIQ project archive contains:

- Cloud project ID: `6505043d-1ade-4fda-a80a-a3b58079f0e5`
- Unity Organization: `ken_buajitti`
- Product name: `RomanceIQ`

**Before play-testing or publishing, verify this exact ID matches the Unity Cloud project where you created/enabled the six events.** The project linkage is left unchanged; a cloned project can still point at an older Cloud project.

## Events and parameters

| Event | Parameters | When |
| --- | --- | --- |
| `romanceiq_loaded` | none | Once each browser game load, when the main menu appears and consent is allowed |
| `romanceiq_started` | none | On PLAY for an unfinished chapter, or starting another path |
| `romanceiq_choice_made` | `stage` Integer, `choice_id` String | When a decision is made; stage 1 through 6; choice_id is `<node-id>:<choice-id>` |
| `romanceiq_stage_reached` | `stage` Integer | First arrival at a choice-progress stage during the current story run. Stage = choices completed + 1, 1 to 7 |
| `romanceiq_completed` | `ending_id` String | At the first arrival at a chapter ending, with the terminal node ID |
| `romanceiq_abandoned` | `stage` Integer, `seconds_played` Integer | On deliberate MENU exit while playing, or best-effort application shutdown |

Stage 1 is entry (zero choices made); stage 2 follows decision 1; ...; stage 7 follows decision 6. Opening a saved game records the current stage, but does not replay historical choices.

## Setup and smoke test

1. Open the project with Unity `6000.5.7f1` (or compatible). Unity should resolve the `com.unity.services.analytics` package (`6.3.0`).
2. Confirm **Project Settings / Services** or Unity Cloud Dashboard has the expected project ID above, or relink to the actual RomanceIQ analytics project.
3. Confirm the six event names/parameters in **Analytics / Event Manager** are assigned and enabled, with exactly the field types in the table.
4. Enter Play mode. At the menu select **GAMEPLAY ANALYTICS**, then **ALLOW GAMEPLAY ANALYTICS** on this device. Analytics is opt-in, and is off until consent is given. You can decline or turn it off at any time.
5. Back at the menu select **PLAY**, advance a scene and make a decision, then choose **MENU**. You should generate `loaded`, `started`, `stage_reached`, `choice_made`, and `abandoned` records. Finish a chapter to generate `completed` with `ending_id`.
6. Check Unity Analytics **Debug Panel / Event Browser** for the records. Editor Console logs prefixed `[RomanceIQ Analytics]` indicate SDK submission attempts, **not** server acceptance.
7. Build with **IQ Games > Build RomanceIQ for itch.io**, then upload that new WebGL build ZIP to itch.io. A Unity project ZIP is **not** an itch.io WebGL build.

## Interpretation and limitations

- itch.io views alone don't prove that someone started the game; these events begin collecting only in the newly published build, not retrospectively.
- Analytics is optional. Players who do not opt in are excluded from Unity Analytics event counts, so compare funnels among **consenting** players only.
- `romanceiq_abandoned` is reliable for clicking MENU while a chapter is unfinished. On WebGL browser/tab shutdown the event may not upload; use stage reach/choice funnel to estimate where other players disengage. `OnApplicationPause` tries to flush pending data without labeling a temporary tab switch as a permanent abandonment.
- Only IDs, stage numbers, and elapsed seconds are sent by our custom events, not story text or voluntary feedback.
- Previous `vnRun` saves remain compatible, and the game continues to work without Analytics initialization.

