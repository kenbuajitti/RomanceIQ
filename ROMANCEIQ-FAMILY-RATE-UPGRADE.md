# RomanceIQ – Family Rate-style upgrade

## Features in this Unity source

1. **Fullscreen toggle:** `FULL SCREEN` / `EXIT FULL SCREEN` from the main menu and story toolbar. The toggle calls `UnityEngine.Screen.fullScreen` inside the user's click handler; reader pages automatically reflow when browser size changes. Browser fullscreen requires the host iframe to allow the fullscreen API (test on itch.io after publishing).
2. **One subject on screen:** PLAY displays a single portrait, selected from the current scene's `focus` / speaker, using `RomanceIQVisualNovel.BuildVNStage(..., singlePortrait: true)`. The older optional live reader also uses a single portrait.
3. **Contextual backgrounds:** the scene selects among `Lobby`, `LobbyEvening`, `LobbyMorning`, `Projection`, and `StreetNight`, using scene metadata where supplied and narrative context otherwise. Reusable art is bundled in `Assets/Resources/RomanceIQ/VisualNovel`.
4. **Three authored decisions, then online continuation:** all authored material remains in `Story.json`; after decision 3, the game requests shared online decisions/scenes from the `romanceiq-story` Supabase Edge Function. Generated scenes begin with the fourth decision; new choices can continue to the 30-decision limit.
5. **Shared path cache:** `romanceiq-story` keys each record from the exact ordered choices in the existing Supabase `story_nodes` table, under the `ri-v1-` namespace. A reused path serves saved content instead of regenerating. Private API credentials stay on the server; only a public function URL is shipped in Unity. Local player bookmarks remain device-specific.
6. **Analytics retained:** existing `romanceiq_*` events and consent flow are unchanged.

## Deploy before publishing

The **Unity source is updated**, but Supabase and itch.io are **not deployed by this ZIP**. To activate continuation:

1. In the **existing Family Rate Supabase project**, run `supabase/migrations/20261020_romanceiq_story_budget.sql` in SQL Editor. This sets a separate 200-per-UTC-day *new generation* cap; reuse is not counted.
2. Deploy `supabase/functions/romanceiq-story` as the new Edge Function `romanceiq-story` with JWT verification **disabled**. Keep `FAMILYRATE_PRIVATE_API_KEY` and server service-role credentials only in Supabase, and ensure the preexisting `dynamic-function` and `story_nodes` used by Family Rate work. Never add secrets to Unity assets.
3. Configure `ROMANCEIQ_ALLOWED_ORIGINS` to match the **actual browser Origin** for the itch.io hosted game (plus any other sites used), then verify the public `/health` endpoint returns `storyVersion: vn-release-1`.
4. In Unity 6 (`6000.5.7f1` is the project's recorded editor version), open RomanceIQ and test: PLAY, three decisions, next choices, fourth decision, back navigation, fullscreen entry/exit, and analytics. Exit and replay the *same* decision path to verify the same scene and choices appear. A different branch should produce its own cached scene.
5. Build **WebGL** and upload that build to the existing itch.io RomanceIQ page. Source ZIPs are **not** itch.io playable WebGL build artifacts.

## Tests

Run `node --test supabase/test/*.test.mjs` to verify path validation and cache key stability. These server logic tests passed in the work environment. The Unity project was **not compiled or browser-playtested here**; a Unity/WebGL editor and the live Supabase deployment are necessary for end-to-end validation.

## Source notes

- The game remains **RomanceIQ**, and its distinct story/analytics IDs should not be replaced with StoryIQ or Family Rate identifiers.
- Content persists centrally by decision path; the player's local bookmark and optional feedback are **not** shared with other players.
- If the gateway is unavailable, the game offers **RETRY**, rather than silently generating a different local branch.
