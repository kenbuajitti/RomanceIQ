
export const DECISION_LIMIT = 20;
export class StoryError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}
const fail = (status, message) => { throw new StoryError(status, message); };
const idPattern = /^[a-f0-9]{32}$/i;

export { instructions } from './romance.mjs';
import { canon, beats } from './romance.mjs';
export function chooseCanon() { return structuredClone(canon); }

export function validateGenerated(value, ending) {
  const text = (v, max) => typeof v === 'string' && v.trim().length > 0 && v.length <= max;
  if (!value || !text(value.title, 160) || !text(value.narrative, 6000)
    || !Array.isArray(value.choices) || typeof value.epilogue !== 'string')
    fail(502, 'The storyteller returned an incomplete scene. Please retry.');
  if (ending) {
    if (value.choices.length !== 0 || !text(value.epilogue, 5000))
      fail(502, 'The ending was incomplete. Please retry.');
  } else if (value.epilogue !== '' || value.choices.length !== 5
    || value.choices.some(c => !text(c, 220))
    || new Set(value.choices.map(c => c.trim().toLowerCase())).size !== 5) {
    fail(502, 'The storyteller did not return five distinct choices. Please retry.');
  }
  return value;
}

export class StoryEngine {
  constructor(generate, { ttlMs = 24 * 60 * 60 * 1000, maxSessions = 100, now = Date.now, store = null } = {}) {
    this.generate = generate;
    this.store = store;
    this.sessions = new Map((store?.read("sessions") || []).map(([id, s]) => [id, { ...s, cache: new Map(s.cache), pending: null }]));
    this.ttlMs = ttlMs;
    this.maxSessions = maxSessions;
    this.now = now;
  }

  async advance(input) {
    if (!input || typeof input.sessionId !== 'string' || typeof input.requestId !== 'string'
      || !idPattern.test(input.sessionId) || !idPattern.test(input.requestId)
      || !Number.isInteger(input.expectedTurn) || input.expectedTurn < 0 || input.expectedTurn > 20
      || typeof input.choiceId !== 'string' || input.choiceId.length > 30)
      fail(400, 'Invalid story request. Begin a new story.');
    const { sessionId, requestId, expectedTurn, choiceId } = input;
    for (const [id, s] of this.sessions)
      if (!s.pending && this.now() - s.touched > this.ttlMs) this.sessions.delete(id);
    let session = this.sessions.get(sessionId);
    if (!session) {
      if (expectedTurn !== 0 || choiceId !== '') fail(410, 'This story has expired or the server restarted. Begin a new story.');
      if (this.sessions.size >= this.maxSessions) fail(429, 'The story server is full. Please try again later.');
      session = { fixedCanon: chooseCanon(), history: [], current: null, touched: this.now(), cache: new Map(), pending: null };
      this.sessions.set(sessionId, session);
    }
    session.touched = this.now();
    const signature = JSON.stringify([expectedTurn, choiceId]);
    const cached = session.cache.get(requestId);
    if (cached) {
      if (cached.signature !== signature) fail(409, 'This request was already used for a different choice.');
      return structuredClone(cached.response);
    }
    if (session.pending) {
      if (session.pending.id === requestId && session.pending.signature === signature)
        return structuredClone(await session.pending.promise);
      fail(409, 'Another story request is still running. Retry shortly.');
    }
    let chosen = null;
    if (session.current) {
      if (session.current.isEnding) fail(409, 'This story has ended. Begin a new story.');
      if (session.current.completed !== expectedTurn) fail(409, 'That choice belongs to an earlier scene.');
      chosen = session.current.choices.find(c => c.id === choiceId);
      if (!chosen) fail(400, 'Choose one of the five actions in the current scene.');
    } else if (expectedTurn !== 0 || choiceId !== '') {
      fail(400, 'The opening scene must load first.');
    }
    const completed = session.current ? session.current.completed + 1 : 0;
    const day = Math.min(5, Math.floor(completed / 4) + 1);
    const isEnding = completed === DECISION_LIMIT;
    const pending = { id: requestId, signature, promise: null };
    session.pending = pending;
    pending.promise = (async () => {
      // Generate first. No accepted history, turn or choice changes on failure.
      const generated = validateGenerated(await this.generate({
        completed, day, isEnding, fixedCanon: structuredClone(session.fixedCanon),
        sceneNumber: isEnding ? 20 : completed + 1, sceneDirection: beats[completed],
        history: structuredClone(session.history), acceptedChoice: chosen ? { ...chosen } : null
      }), isEnding);
      const response = { sessionId, requestId, completed, day, isEnding,
        title: generated.title.trim(), narrative: generated.narrative.trim(),
        choices: generated.choices.map((text, i) => ({ id: `t${completed}_c${i + 1}`, text: text.trim() })),
        epilogue: generated.epilogue.trim() };
      const previous = { history: [...session.history], current: session.current, cache: new Map(session.cache) };
      session.history.push({ completed, acceptedChoice: chosen, scene: generated });
      session.current = response;
      session.cache.set(requestId, { signature, response });
      session.touched = this.now();
      try {
        this.store?.write("sessions", [...this.sessions].map(([id, s]) => [id, { ...s, cache: [...s.cache], pending: null }]));
      } catch (error) { Object.assign(session, previous); throw error; }
      return response;
    })();
    try { return structuredClone(await pending.promise); }
    finally { if (session.pending === pending) session.pending = null; }
  }
}
