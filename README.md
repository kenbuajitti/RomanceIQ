# RomanceIQ Phase A — Missing-parent hotfix

**Status:** Candidate patch; not applied to your GitHub repository or live game.
**Baseline:** The separate Phase A patch `RomanceIQ-Phase-A-Observability-Stabilization.zip`, NOT an independently tested latest GitHub revision. Compare each file to current HEAD before merging.

## What this fixes

- After `dynamic-function` says `ready`, gateway now reads `story_nodes` and returns HTTP 200 only when the exact generated node is persisted with `generation_status=ready`. A transient/uncommitted response stays 202 pending instead of displaying choices that disappear on the next request.
- `parent_unavailable` HTTP 409 logs the expected parent key and database state under the request's trace ID.
- Error message for a missing Stage 3 generated-choice parent tells users to return to the authored boundary, request new choices, and choose again. It never silently reinterprets an old choice ID as a newly generated choice.
- Unity hides the useless `RETRY STORY` button for this specific error, showing `PREVIOUS DECISION`. All other errors retain retry buttons. The three authored stages remain untouched.

## Installation

1. Verify existing Phase A patch was merged against latest GitHub HEAD. If not, review before applying either patch.
2. Merge only the changes in `hotfix-against-phase-a.patch` or carefully compare replacement files; do not overwrite newer code.
3. Redeploy the **romanceiq-story** gateway only. Do not deploy or change `dynamic-function` as part of this hotfix.
4. Rebuild WebGL with `RomanceIQOnline.cs`, then test the transition from the authored Stage 3 boundary through generated choices to the first live scene.
5. Check Supabase gateway logs for `generator_ready_but_uncommitted` or `missing_parent` correlated using the support-reference prefix. The short support reference is only 8 hex characters; use the full `trace_id` from logs to disambiguate.

## Read-only Supabase SQL diagnostics

```sql
SELECT level, generation_status, count(*) AS paths
FROM public.story_nodes
WHERE path_key LIKE 'ri-v2-%'
GROUP BY level, generation_status
ORDER BY level, generation_status;

SELECT path_key, level, generation_status, parent_path_key
FROM public.story_nodes
WHERE path_key LIKE 'ri-v2-intro-%'
ORDER BY path_key;
```

**Do not delete cached records** to repair this error before checking why they are missing.

## Limitations

- An already-lost parent scene cannot be reconstructed faithfully without a stored snapshot of the original choices. The last generated decision may have to be selected again.
- If `dynamic-function` never persists `ready` records, the gateway will now stay pending and eventually present a timeout instead of misleadingly succeeding. That identifies the missing generator-side fix; it doesn't fix the generator itself.
- Test with a staging database before production deployment. No live Supabase operation has been performed here.
