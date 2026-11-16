# RomanceIQ Phase A: Diagnose and stabilize (November 8, 2026)

**Status:** Candidate integration patch, NOT deployed and NOT merged into GitHub.

## Source baseline and limits

Prepared from previously saved RomanceIQ source:
- Unity `RomanceIQ_Ask_Every_Visit_Full_Project.zip` plus later `RomanceIQ-Astra-Timeout-Fix.zip` (`RomanceIQOnline.cs` only)
- Later Supabase gateway `romanceiq-story-v2-clean-cache.zip` (`index.ts`)
- Story's latest saved `vn-flashforward-2`, `ri-v2-` cache, `dynamic-function` backend

Your newly pushed GitHub `RomanceIQ/main` commit was **not available** when preparing this patch. Compare all three edited source files to current GitHub HEAD before applying. Do not copy older `story.json` or `logic.mjs` into your repo. The patch deliberately does not include either of them, nor any changes to Stages 1–3, dynamic-function, or the existing model configuration.

## What changes

1. C# client sends an anonymous `X-RomanceIQ-Trace-Id` (32 hex characters) consistently across polls for a single scene.
2. Adds the **consent-gated** Unity Analytics custom event `romanceiq_scene_flow` at `request_started`, first `pending`, `response_received`, `choices_displayed` / `scene_displayed`, and `request_failed`. Existing six event names and consent flow are untouched.
3. Adds structured Supabase logs (`trace_id`, `stage`, `path_key`, `event`, `elapsed_ms`, error type; **no full story text or private key**).
4. Bounds Unity's polling to about 120 seconds instead of retrying forever; on failure shows existing retry UI, saved progress, and an eight-character support reference.
5. Adds a private Postgres watchdog and atomic claim for the `ri-v2-` namespace only. A persisted `pending` node seen for more than ten minutes is eligible for **one** automatic reset; a second prolonged stall returns a visible error rather than `202` forever. An uncached path can only be claimed for generation once at a time, avoiding duplicate launches on repeated polls.
6. Preserves the current behavior: `path.length===3` requests AI-generated **choice options** attached to the third authored branch-ending text; `path.length>=4` requests generated story scenes.

**Important:** Ten minutes is measured from the first time a pending row is observed by the gateway, not from its original creation. An already stale row may therefore need another ten minutes after the patch is deployed before automatic recovery. The 10-minute grace period is longer than Supabase's published worker wall-clock limits, but verify your own `dynamic-function` does not delegate generation to a separate long-running worker before enabling this in production. A reset deletes only a `generation_status='pending'` `ri-v2-` record, never an authored or ready scene. It cannot prevent every possible generator failure.

## Integration order

1. **Review branch diff** against today's GitHub `main`. Keep a backup, then create a feature branch `phase-a-observability`.
2. Review/apply `supabase/migrations/20261108_romanceiq_pending_watchdog.sql` via Supabase SQL Editor or CLI. This step must occur **before** deploying the new gateway. The migration creates three service-role-only RPCs: `romanceiq_watch_pending`, `romanceiq_claim_generation`, and `romanceiq_release_unstarted_claim`.
3. Merge new `supabase/functions/romanceiq-story/index.ts` into the repo's newest gateway version, preserving any later fixes. **Do not redeploy `dynamic-function`** as part of Phase A. Example command from repo root:

   ```bash
   supabase functions deploy romanceiq-story --project-ref vwrwgcqiixmqlzyuybbx
   ```

   Confirm `ROMANCEIQ_ALLOWED_ORIGINS`, `FAMILYRATE_PRIVATE_API_KEY`, and existing database secrets remain set in Supabase; do not copy actual values into source control.
4. Register Unity Analytics custom event `romanceiq_scene_flow` (standard/custom event schema) with these parameters:

   | Parameter | Type |
   |---|---|
   | `stage` | integer |
   | `phase` | string |
   | `trace_id` | string |
   | `duration_ms` | integer |
   | `http_status` | integer |

   It is emitted only after player consent. The reference can be matched against Supabase logs. No story text, complete choice history, or persistent player identifier is sent in this event.
5. Merge the two C# files into your current Unity project and compile. Build WebGL and test locally before uploading to itch.io. Phase A does **not** add the future progress bar; that is Phase C.

## Pre-deployment tests

- `node --test supabase/test/phase-a.test.mjs` — four tests check authored scenes, 12 authored endings, `ri-v2` cache keys, and choice validation.
- The supplied TypeScript was statically type-checked, but **no real Supabase invocation or Unity WebGL build was run** in this environment.
- `GET /functions/v1/romanceiq-story/health` should return `diagnosticVersion: phase-a-1` after deployment.
- Read-only SQL in the Supabase SQL Editor:

   ```sql
   SELECT level, generation_status, COUNT(*) AS paths
   FROM public.story_nodes
   WHERE path_key LIKE 'ri-v2-%'
   GROUP BY level, generation_status
   ORDER BY level, generation_status;

   SELECT path_key, first_observed_at, resets,
          now()-first_observed_at AS age
   FROM public.romanceiq_pending_watchdog
   ORDER BY first_observed_at;
   ```

## End-to-end smoke test

1. Open the new Unity development build, grant analytics consent, play through three authored choices, and press Next Decisions.
2. In browser developer tools, inspect requests to `/romanceiq-story/api/scene`; confirm `X-RomanceIQ-Trace-Id` is present (same value for all polls), request and response headers permit CORS, and responses are `202` pending followed by `200` scene, or `200` immediately from cache.
3. Confirm the third authored ending's text has not changed and **three** online decisions appear. This should emit `choices_displayed` (stage 4).
4. Select a generated decision: the new scene should appear and emit `scene_displayed` (stage 5). Check that `romanceiq_stage_reached` still works.
5. Match the trace ID to the `romanceiq-story` function logs. You should see `path_validated`, cache hit/miss, `generator_called`/`generator_responded` if applicable, and `scene_ready`. On failure, use the support reference shown to the player.
6. In a **staging database only**, test a deliberately pending `ri-v2` record and verify a ten-minute-old watchdog entry leads to no more than one recovery attempt. Do not manually insert/delete story rows on the production database for testing.
7. Press Menu during loading, resume/retry, and check that the game does not lose saved progress or show an indefinite spinner.

## Interpretation of stage measurements

`romanceiq_stage_reached` is emitted when `ShowVNNode` displays the authored boundary, not necessarily when generated choices are ready. The new `choices_displayed` and `scene_displayed` markers provide the missing distinction. Do not interpret Stage 4 reached as proof of successful generation.

## Known limits / further investigation

- This patch adds a correlation header to the private generator call **without changing its JSON payload**. Inspect `dynamic-function` source/logging later if you want it to record that header, too.
- A generator that errors before persisting its row may leave a claimed path until the ten-minute recovery lease expires. The client returns a visible failure instead of hanging; true worker-level durability can be added after reviewing the private generator.
- Existing story generation still may take tens of seconds. Phase A instruments and stabilizes the flow; Groq swap and pre-generated Stage 4 are later phases.
- Before production, inspect the `story_nodes` schema for foreign keys, triggers, and the generator's update behavior. The watchdog is designed to delete stale `pending` nodes, so it should be tested on staging against that schema.

## Rollback

- Revert the `index.ts` and two Unity C# files to the backed-up revision; redeploy the previous `romanceiq-story` function and WebGL build. Existing authored story and cached ready nodes are unaffected by the patch.
- Leave watchdog migration objects installed if you roll back code; they are passive when old gateway does not call them. Remove them later with a separate reviewed migration if desired.
