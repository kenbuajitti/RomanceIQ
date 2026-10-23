# RomanceIQ: three authored decisions, then shared live stories

This release upgrades the **PLAY** flow, not the legacy live prototype or FamilyRate. The first three decisions and all their authored dialogue are kept in Unity. When the player finishes the third-decision scene, the reader asks for the next three choices from a Supabase gateway. Paths at depth 4 and beyond retrieve or create a scene, keyed by the **exact ordered decisions**. Previously generated scenes and their three choices are reused for all players. The local PlayerPrefs bookmark records which path to resume.

## Server deployment (required before web release)

Use the **same Supabase project that already runs FamilyRate**, but create a NEW function named `romanceiq-story`. It uses the existing private `dynamic-function`, the existing service-role credentials, and the shared `story_nodes` table. Its hashed path keys begin `ri-v1-` so they do not collide with FamilyRate.

1. Back up RomanceIQ. Open the existing Supabase SQL Editor and execute `supabase/migrations/20261020_romanceiq_story_budget.sql`. This introduces a **separate** 200-new-generations-per-UTC-day safety cap; cached requests are free.
2. Deploy `supabase/functions/romanceiq-story/` as a **public** Edge Function (JWT verification disabled), just like `familyrate-story`. Set or retain the server-side `FAMILYRATE_PRIVATE_API_KEY` for the existing `dynamic-function` (this is not put into Unity) plus the existing Supabase service role secret or managed secret key. Set `ROMANCEIQ_ALLOWED_ORIGINS` to the exact site origins you use (default includes itch.io's hosting origins).
3. Verify `<SUPABASE_URL>/functions/v1/romanceiq-story/health` returns `storyVersion: vn-flashforward-2`. If the project is different from the FamilyRate project, set `vnSharedStoryUrl` on the `RomanceIQ Controller` Inspector component to the new public function URL. Never place private API keys in Unity.
4. In Unity test PLAY: make three decisions, read the third scene, click to get the online choices, make a fourth decision, then return and repeat the same path. The next scene and its choices should be identical. Verify `public.story_nodes` contains `ri-v1-` keys marked `ready`.
5. Build a new WebGL release and publish it to itch.io. Updating the source alone does not change the live game. Retest on the hosted browser origin.

If the gateway is not deployed or configured, the game displays an explanation and RETRY; it does **not** invent a live continuation. The source was not compiled in Unity or tested against a live Supabase instance here; deployment and full playtesting remain necessary.

## Design and asset changes

- The stage now shows **one character**, normally the current speaking character, instead of all three at once.
- Scene backgrounds are selected from Lobby, LobbyEvening, LobbyMorning, Projection, and StreetNight.
- The first three authored decisions are twelve brand-new authored outcome paths; remote continuation begins afterward.
- Gameplay analytics remains in the source; server path caching is independent of consent-based gameplay analytics.
- The local bookmark is personal; the durable **shared** content is in Supabase `story_nodes`. The new release version intentionally starts fresh for old prototype bookmarks.

## Quick verification

From project root run `node --test supabase/test/*.test.mjs`. No extra npm dependencies are needed for these tests.
