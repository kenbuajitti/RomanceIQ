ROMANCEIQ — NEW THREE-CHAPTER ROMANTIC SUSPENSE OPENING
October 22, 2026

WHAT CHANGED
- Same existing cast and Marquee cinema setting: Maya Chen, Noah Bennett, Eli Rivera.
- Sunday 11:47 p.m. cold open: police, locked booth, two knocks, Eli's camera,
  Noah's stained shirt, and a missing-memory key. Rewind to Tuesday.
- Three authored decisions: Tuesday, Thursday, Saturday.
- 12 original handoff scenes instead of the old pre-generated outcomes.
- Clear CHAPTER ONE / TWO / THREE reader headers.
- Updated menu tease and unchanged artwork, layout, analytics and navigation.
- The already-prepared ensemble image still appears during live generation.
- Updated shared Supabase gateway's story data and writing instructions:
  romantic suspense, emotional realism, continuity, character agency, earned
  relationships and eventual logical payoff of the opening mystery.
- No changes to Supabase database schema, connection credentials, or analytics event names.

CRITICAL: LOCAL UNITY FILE AND DEPLOYED EDGE FUNCTION MUST MATCH
The story version is now vn-flashforward-2, previously vn-release-1.
Old local bookmarks automatically start anew. Old DB entries remain untouched;
new paths receive separate cache keys. No manual database deletion or SQL needed.

DEPLOY THE OVERLAY PATCH (RECOMMENDED)
1. Commit current RomanceIQ repo in GitHub Desktop for a rollback point.
2. Close Unity.
3. Extract the ZIP to a temporary folder.
4. Copy its Assets and supabase folders into the ROOT of the existing
   RomanceIQ repo; merge folders and replace only files in this patch.
   Do NOT delete, replace, or recreate .git, Library, your entire Assets folder,
   existing Supabase project, or the StoryServer.
5. Open Unity and verify it compiles. Test the first three choices and use
   the next arrow after the authored third-decision scene.
6. Commit/push these source changes in GitHub Desktop.
7. In Supabase, UPDATE and REDEPLOY the existing 'romanceiq-story' Edge
   Function using the WHOLE supabase/functions/romanceiq-story folder.
   The function must deploy both story.json AND logic.mjs with index.ts.
   Leave 'dynamic-function' and the existing secrets alone.
8. In browser visit the CURRENT Edge Function /health endpoint and verify:
       "storyVersion":"vn-flashforward-2"
9. Playtest again, making 3 choices and then a FOURTH online choice.
   Cached paths should repeat the exact same online scene.
10. Rebuild WebGL in Unity and upload the NEW build to itch.io.
    GitHub push and Supabase redeploy do not update itch.io's old build.
11. Test once on itch.io, then watch analytics over the following days.

NOTE: Deploy Unity and Supabase close together; an old WebGL build will be
rejected by the new function because it sends the obsolete story version.
Visitors with a cached older WebGL build may need to reload once you publish.

VALIDATION COMPLETED
- All 12 authored branches resolve with exactly three choices before handoff.
- Old storyVersion is rejected and independent DB path key is used.
- Node tests and existing StoryServer tests passed using Node.js.
- No Unity Editor, WebGL hosted or live Supabase test was available here:
  the above deployment/playthrough validation is still required.

If you want to compare retention with the previous narrative, keep notes of
when this version became public. Old analytics events are unchanged, so
before/after analytics need an explicit release timestamp to interpret.
