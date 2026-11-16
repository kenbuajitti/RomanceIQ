// RomanceIQ Phase A: traceable gateway and bounded pending-job recovery.
// IMPORTANT: This preserves the existing story.json, logic.mjs and dynamic-function.
// Do not expose server keys or unredacted story text in logs.
import story from './story.json' with { type: 'json' };
import {
  DEFAULT_ALLOWED_ORIGINS, buildContext, normalizeChoices, pathKey,
  toScene, validatePath, parseEntry
} from './logic.mjs';

const supabaseUrl = (Deno.env.get('SUPABASE_URL') || '').replace(/\/$/, '');
const allSecrets = Deno.env.get('SUPABASE_SECRET_KEYS');
let secretMap: Record<string, string> = {};
try { secretMap = allSecrets ? JSON.parse(allSecrets) : {}; } catch { /* no map */ }
const dbKey = secretMap.default || Deno.env.get('SUPABASE_SERVICE_ROLE_KEY') || '';
const privateKey = Deno.env.get('FAMILYRATE_PRIVATE_API_KEY') || '';
const allowedOrigins = (Deno.env.get('ROMANCEIQ_ALLOWED_ORIGINS') || DEFAULT_ALLOWED_ORIGINS.join(','))
  .split(',').map(s => s.trim()).filter(Boolean);

type AnyObj = Record<string, any>;
type Trace = { id: string; start: number; stage?: number; pathKey?: string };
function makeTrace(req: Request): Trace {
  const incoming = req.headers.get('x-romanceiq-trace-id') || '';
  return { id: /^[a-f0-9]{32}$/i.test(incoming) ? incoming.toLowerCase() : crypto.randomUUID().replaceAll('-', ''), start: performance.now() };
}
function log(t: Trace, event: string, fields: AnyObj = {}) {
  console.log(JSON.stringify({ service: 'romanceiq-story', event, trace_id: t.id,
    elapsed_ms: Math.round(performance.now() - t.start), stage: t.stage ?? null,
    path_key: t.pathKey ?? null, ...fields }));
}
function reply(req: Request, data: AnyObj, status = 200, t?: Trace): Response {
  const origin = req.headers.get('origin');
  const headers: Record<string, string> = {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store', 'vary': 'Origin',
    'access-control-allow-methods': 'GET,POST,OPTIONS',
    'access-control-allow-headers': 'content-type,x-romanceiq-trace-id',
    'access-control-expose-headers': 'x-romanceiq-trace-id'
  };
  if (origin && allowedOrigins.includes(origin)) headers['access-control-allow-origin'] = origin;
  if (t) headers['x-romanceiq-trace-id'] = t.id;
  if (status === 202) headers['retry-after'] = '3';
  return new Response(JSON.stringify(t ? { ...data, trace_id: t.id } : data), { status, headers });
}
function fail(req: Request, status: number, code: string, message: string, t?: Trace): Response {
  if (t) log(t, 'request_failed', { http_status: status, error_code: code });
  return reply(req, { error: code, message }, status, t);
}
async function rest(path: string, options: RequestInit = {}, timeoutMs = 12000): Promise<Response> {
  return fetch(`${supabaseUrl}/rest/v1/${path}`, {
    ...options, signal: AbortSignal.timeout(timeoutMs),
    headers: { apikey: dbKey, accept: 'application/json', ...(options.headers || {}) }
  });
}
async function readNode(key: string): Promise<any | null> {
  const url = new URL(`${supabaseUrl}/rest/v1/story_nodes`);
  url.searchParams.set('path_key', 'eq.' + key);
  url.searchParams.set('select', 'path_key,parent_path_key,level,story_text,choices,generation_status');
  const res = await fetch(url.toString(), { headers: { apikey: dbKey, accept: 'application/json' }, signal: AbortSignal.timeout(12000) });
  if (!res.ok) throw new Error(`DB_READ_${res.status}`);
  const rows = await res.json();
  return rows?.[0] || null;
}
async function guardPending(key: string, t: Trace): Promise<string> {
  const res = await rest('rpc/romanceiq_watch_pending', {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ p_path_key: key })
  });
  if (!res.ok) throw new Error(`WATCHDOG_${res.status}`);
  const state: string = await res.json();
  if (state === 'reset' || state === 'stalled') log(t, 'pending_watchdog', { state });
  return state;
}
async function generate(key: string, parentKey: string | null, level: number, context: string, t: Trace) {
  const begin = performance.now();
  log(t, 'generator_called', { context_chars: context.length });
  let resp: Response;
  try {
    resp = await fetch(`${supabaseUrl}/functions/v1/dynamic-function`, {
      method: 'POST',
      headers: { apikey: privateKey, 'content-type': 'application/json',
        'x-romanceiq-trace-id': t.id },
      signal: AbortSignal.timeout(90000),
      body: JSON.stringify({ path_key: key, parent_path_key: parentKey, level,
        story_context: context })
    });
  } catch (e) {
    log(t, 'generator_transport_failed', { duration_ms: Math.round(performance.now() - begin),
      error_kind: e instanceof Error ? e.name : 'unknown' });
    throw new Error('GENERATOR_TRANSPORT');
  }
  // Do not put raw response/story content in logs.
  const raw = await resp.text();
  let data: any = {};
  try { data = JSON.parse(raw); } catch { /* invalid JSON checked below */ }
  log(t, 'generator_responded', { http_status: resp.status,
    duration_ms: Math.round(performance.now() - begin), status: String(data?.status || 'unknown') });
  if (!resp.ok && resp.status !== 202) throw new Error(`GENERATOR_HTTP_${resp.status}`);
  if (resp.status === 202 || data?.status === 'pending' || data?.status === 'generating')
    return { pending: true };
  if (data?.status !== 'ready' || typeof data.story_text !== 'string' || !Array.isArray(data.choices))
    throw new Error('GENERATOR_INVALID_RESPONSE');
  // A successful generator response is NOT proof that it committed the scene.
  // Never show the player choices we cannot retrieve on their next request.
  // If a write is still finishing, the next poll will read it normally.
  const saved = await readNode(key);
  if (saved?.generation_status !== 'ready') {
    log(t, 'generator_ready_but_uncommitted', { db_status: saved?.generation_status || 'missing' });
    return { pending: true };
  }
  log(t, 'generation_persisted');
  return { pending: false, row: saved, cached: false };
}
async function ready(key: string, parent: string | null, level: number, context: string, t: Trace): Promise<any> {
  let row = await readNode(key);
  if (row?.generation_status === 'ready') {
    log(t, 'cache_hit');
    return { pending: false, row, cached: true };
  }
  if (row?.generation_status === 'pending') {
    const state = await guardPending(key, t);
    if (state === 'stalled') throw new Error('PENDING_STALLED');
    if (state !== 'reset') { log(t, 'cache_pending'); return { pending: true }; }
    // The watchdog safely removed an orphaned pending row (at most once).
    row = await readNode(key);
    if (row?.generation_status === 'ready') return { pending: false, row, cached: true };
    if (row?.generation_status === 'pending') return { pending: true };
  }
  // Atomically claim the uncached path so parallel requests and polling do not
  // repeatedly dispatch the private generator or consume budget.
  const claimResponse = await rest('rpc/romanceiq_claim_generation', {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ p_path_key: key })
  });
  if (!claimResponse.ok) throw new Error(`CLAIM_${claimResponse.status}`);
  const claim: string = await claimResponse.json();
  if (claim === 'pending') { log(t, 'claim_already_active'); return { pending: true }; }
  if (claim === 'stalled') throw new Error('PENDING_STALLED');
  if (claim === 'ready') {
    const saved = await readNode(key);
    if (saved?.generation_status === 'ready') return { pending: false, row: saved, cached: true };
    return { pending: true }; // concurrent writer, retry safely
  }
  if (claim !== 'claimed') throw new Error('CLAIM_UNKNOWN');
  const token = await rest('rpc/romanceiq_consume_generation_budget', {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: '{}'
  });
  if (!token.ok || (await token.json()) !== true) {
    // This claim never invoked the generator, so it can be released safely.
    await rest('rpc/romanceiq_release_unstarted_claim', {
      method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ p_path_key: key })
    });
    if (!token.ok) throw new Error('BUDGET_RPC_MISSING');
    throw new Error('BUDGET_EXHAUSTED');
  }
  log(t, 'cache_miss_start_generation');
  return generate(key, parent, level, context, t);
}
async function scene(req: Request, payload: any, t: Trace): Promise<Response> {
  const { path, ending } = validatePath(story, payload);
  t.stage = path.length + 1;
  log(t, 'path_validated', { depth: path.length, mode: path.length === 3 ? 'intro_choices' : 'live_scene' });
  if (path.length === 3) {
    const key = await pathKey(story, 'intro', path); t.pathKey = key;
    const result = await ready(key, null, 4, buildContext(story, 'intro', path), t);
    if (result.pending) return reply(req, { status: 'pending', phase: 'generating_choices' }, 202, t);
    const choices = normalizeChoices(result.row.choices);
    log(t, 'scene_ready', { cached: result.cached, choice_count: choices.length });
    // Intentional existing behavior: return authored ending text with AI choices.
    return reply(req, { scene: { id: ending.id, speaker: ending.speaker, text: ending.text,
      choices }, cached: result.cached }, 200, t);
  }
  const parentPath = path.slice(0, -1);
  const parentKey = await pathKey(story, parentPath.length === 3 ? 'intro' : 'scene', parentPath);
  const parent = await readNode(parentKey);
  if (parent?.generation_status === 'pending') {
    const state = await guardPending(parentKey, t);
    if (state === 'stalled') throw new Error('PENDING_STALLED');
    if (state === 'reset') return fail(req, 409, 'previous_choices_expired',
      'Previous decisions must be loaded again. Return to the previous decision.', t);
    log(t, 'parent_pending');
    return reply(req, { status: 'pending', phase: 'waiting_parent' }, 202, t);
  }
  if (parent?.generation_status !== 'ready') {
    log(t, 'missing_parent', { parent_key: parentKey, parent_status: parent?.generation_status || 'missing' });
    // A saved choice cannot be silently matched to newly generated options:
    // the choice ID may refer to different text after regeneration.
    const guidance = path.length === 4
      ? 'The generated options after Chapter Three are missing. Choose PREVIOUS DECISION, advance to request fresh options, then select a new choice.'
      : 'The prior generated chapter is missing. Choose PREVIOUS DECISION and reload it before making another choice.';
    return fail(req, 409, 'parent_unavailable', guidance, t);
  }
  const available = normalizeChoices(parent.choices);
  const [, choiceId] = parseEntry(path[path.length - 1]);
  const selected = available.find(c => c.id === choiceId);
  if (!selected) return fail(req, 400, 'choice_invalid', 'That response is not part of this chapter.', t);
  const key = await pathKey(story, 'scene', path); t.pathKey = key;
  const result = await ready(key, parentKey, path.length, buildContext(story, 'scene', path, parent, selected), t);
  if (result.pending) return reply(req, { status: 'pending', phase: 'generating_scene' }, 202, t);
  const nextScene = toScene(path, result.row);
  log(t, 'scene_ready', { cached: result.cached, chars: nextScene.text.length });
  return reply(req, { scene: nextScene, cached: result.cached }, 200, t);
}
Deno.serve(async (req: Request) => {
  const t = makeTrace(req);
  const origin = req.headers.get('origin');
  if (origin && !allowedOrigins.includes(origin))
    return fail(req, 403, 'origin_not_allowed', 'This site cannot access RomanceIQ.', t);
  if (req.method === 'OPTIONS') return reply(req, { ok: true }, 200, t);
  if (req.method === 'GET' && new URL(req.url).pathname.endsWith('/health'))
    return reply(req, { ok: true, service: 'romanceiq-story', storyVersion: story.version,
      cacheVersion: 'ri-v2', diagnosticVersion: 'phase-a-1' }, 200, t);
  if (req.method !== 'POST') return fail(req, 405, 'method_not_allowed', 'POST required.', t);
  if (!supabaseUrl || !dbKey || !privateKey)
    return fail(req, 503, 'configuration_error', 'Owner must configure the story gateway.', t);
  if (Number(req.headers.get('content-length') || 0) > 12000)
    return fail(req, 413, 'request_too_large', 'Request too large.', t);
  try {
    const raw = await req.text();
    if (raw.length > 12000) return fail(req, 413, 'request_too_large', 'Request too large.', t);
    const payload = JSON.parse(raw);
    if (new URL(req.url).pathname.endsWith('/api/scene')) return await scene(req, payload, t);
    return fail(req, 404, 'unknown_route', 'Unknown API route.', t);
  } catch (e) {
    if (e instanceof SyntaxError) return fail(req, 400, 'malformed_json', 'Malformed request.', t);
    const detail = e instanceof Error ? e.message : 'UNKNOWN';
    log(t, 'exception', { error_kind: detail.slice(0, 80) });
    if (/^(Invalid|Unknown RomanceIQ)/.test(detail))
      return fail(req, 400, 'invalid_path', detail, t);
    if (detail === 'BUDGET_EXHAUSTED')
      return fail(req, 429, 'budget_exhausted', 'Daily generation budget reached. Previously visited paths still work.', t);
    if (detail === 'PENDING_STALLED')
      return fail(req, 504, 'generation_stalled', 'This chapter is stuck. Your progress is saved; retry later.', t);
    if (detail.startsWith('WATCHDOG_') || detail.startsWith('CLAIM_'))
      return fail(req, 503, 'watchdog_unavailable', 'Story recovery is temporarily unavailable.', t);
    return fail(req, 502, 'upstream_error', 'Live continuation is temporarily unavailable. Retry shortly.', t);
  }
});
