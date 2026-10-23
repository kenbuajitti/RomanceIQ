export const MAX_DEPTH = 30;
export const DEFAULT_ALLOWED_ORIGINS = [
  'https://itch.io', 'https://html.itch.zone',
  'https://iqgamesonline.com', 'https://www.iqgamesonline.com',
  'http://localhost:8080', 'http://127.0.0.1:8080',
];

export function choiceAt(story, from, id) {
  const nodes = new Map(story.nodes.map(n => [n.id,n]));
  let current = from;
  for (let i=0; i < 80; i++) {
    const node = nodes.get(current);
    if (!node) throw new Error('Invalid authored story node.');
    if (node.choices?.length) {
      const item = node.choices.find(c => c.id === id);
      if (!item) throw new Error('Invalid authored choice.');
      return item.next;
    }
    if (!node.next) throw new Error('Invalid authored continuation.');
    current = node.next;
  }
  throw new Error('Authored story has a cycle.');
}

export function authoredTrace(story, path) {
  if (path.length < 3) throw new Error('First three choices are required.');
  const nodes = new Map(story.nodes.map(n => [n.id,n]));
  let current = story.start;
  const chosen = [], snippets = [];
  for (let i=0;i<3;i++) {
    for (let k=0;k<80;k++) {
      const node=nodes.get(current);
      if(!node) throw new Error('Invalid authored story node.');
      if (node.text) snippets.push(`${node.speaker || 'MAYA'}: ${node.text}`);
      if (node.choices?.length) {
        const [from,id]=parseEntry(path[i]);
        if (from!==node.id) throw new Error('Invalid authored path prefix.');
        const selected=node.choices.find(c=>c.id===id);
        if(!selected) throw new Error('Invalid authored path choice.');
        chosen.push(selected.text);
        current=selected.next;
        break;
      }
      if(!node.next) throw new Error('Invalid authored path ending.');
      current=node.next;
      if(k===79) throw new Error('Authored story has a cycle.');
    }
  }
  const ending=nodes.get(current);
  if(!ending?.text) throw new Error('Invalid authored third-level scene.');
  snippets.push(`${ending.speaker || 'MAYA'}: ${ending.text}`);
  return {ending, chosen, snippets};
}
export function thirdNode(story,path) { return authoredTrace(story,path).ending; }
export function parseEntry(entry) {
  if (typeof entry !== 'string' || !/^[a-zA-Z0-9_-]{1,80}:[a-zA-Z0-9_-]{1,70}$/.test(entry))
    throw new Error('Invalid path choice token.');
  return entry.split(':');
}
export function validatePath(story,payload) {
  if (!payload || payload.storyId!==story.storyId || payload.storyVersion!==story.version)
    throw new Error('Unknown RomanceIQ story/version.');
  const path=payload.path;
  if (!Array.isArray(path) || path.length<3 || path.length>MAX_DEPTH)
    throw new Error('Invalid decision count.');
  path.forEach(parseEntry);
  const ending = thirdNode(story,path);
  for (let i=3;i<path.length;i++) {
    const [from] = parseEntry(path[i]);
    if (from !== (i === 3 ? ending.id : `live_${i}`))
      throw new Error('Invalid online path prefix.');
  }
  return {path,ending};
}
export async function pathKey(story,kind,path) {
  const input=JSON.stringify(['romanceiq',story.storyId,story.version,'continuation-v1',kind,path]);
  const dig=new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(input)));
  return 'ri-v1-'+kind+'-'+Array.from(dig,b=>b.toString(16).padStart(2,'0')).join('');
}
export function normalizeChoices(raw) {
  if (!Array.isArray(raw) || raw.length !== 3) throw new Error('Story generator returned an invalid set of decisions.');
  const used=new Set();
  return raw.map((item,i)=>{
    const text=typeof item==='string'?item:item?.text;
    if (typeof text!=='string' || text.trim().length<3 || text.length>400)
      throw new Error('Story generator returned a blank/oversize decision.');
    let id = typeof item==='string'? `choice_${i+1}` : item?.id;
    id=typeof id==='string'?id.toLowerCase().replace(/[^a-z0-9_-]/g,'_').slice(0,60):`choice_${i+1}`;
    if (!id || used.has(id)) id=`choice_${i+1}`;
    used.add(id);
    return {id,text:text.trim()};
  });
}
export function locationFor(text) {
  const t=(text||'').toLowerCase();
  if (/rooftop|pavement|sidewalk|outside|street|walk home|night air/.test(t)) return 'StreetNight';
  if (/projection booth|projection room|reels|projector room/.test(t)) return 'Projection';
  if (/morning|sunrise|breakfast/.test(t)) return 'LobbyMorning';
  if (/evening|twilight|late night/.test(t)) return 'LobbyEvening';
  return 'Lobby';
}
export function toScene(path,row) {
  if (!row || row.generation_status!=='ready' || typeof row.story_text!=='string' || !row.story_text.trim())
    throw new Error('Generated scene is not ready.');
  return {id:`live_${path.length}`, speaker:'MAYA', text:row.story_text,
    background:locationFor(row.story_text), choices:normalizeChoices(row.choices)};
}
export function buildContext(story,mode,path,prior=null,selected=null) {
  const trace=authoredTrace(story,path);
  const ending=trace.ending;
  const source= `RomanceIQ: The Last First Night — original, emotionally sophisticated interactive romantic suspense (not a parody or imitation of any existing show/book). Set at The Marquee independent cinema in Asheville. FIRST-PERSON PRESENT as Maya Chen, 25, witty designer recently laid off in Atlanta with a Monday Chicago interview she may still be hiding. Noah Bennett, 27, old friend and projectionist/manager, almost kissed Maya years ago; he used a refundable $4,000 projector deposit to pay overdue staff wages and faces Monday collection. Eli Rivera, 28, sensitive documentary filmmaker with a redevelopment-backed commission originally demanding a farewell film; he raised objections before meeting Maya but kept the contract quiet. Trust/romance are earned, never dictated.
THE ESTABLISHED FLASH-FORWARD PROMISE: On SUNDAY at 11:47 P.M., police lights outside the Marquee, Eli's camera on the wet lobby floor still recording, a dark red smear on Noah's shirt, two knocks from behind a locked projection-booth door followed by silence, Eli saying 'don't play the last minute', Noah asking Maya for the key, which she cannot remember picking up. These facts are CANON and must eventually receive a plausible, causally earned explanation. The scene does NOT prove murder, guilt, or even that the smear is blood. Do not force a predetermined culprit. The narrative is currently on SATURDAY after three player decisions; advance naturally into Sunday and deliver consequences.
Craft: begin with a concrete consequence of the last decision, not a recap. Sensual but non-explicit tension; sharp subtext; awkward wit; flawed adults; genuine attraction; romantic choices that matter independently of the cinema's fate. Scene-by-scene progress rather than filler, generic moralizing, contrived misunderstanding, repeated cliffhangers or sudden villains. Maya retains agency, no unchosen consent or confessions, maintain continuity and distinct fallout. End scenes with three meaningful actions including a boundary or independent option. Integrate the Sunday mystery progressively: clues must be seeded before payoffs, and the flash-forward resolved on-screen rather than forgotten. Never lift plotlines, phrasing or dialogue from existing works.`;
  const recap=`Recent authored dialogue (in order):\n${trace.snippets.slice(-8).join('\n\n')}`;
  const decisions=`Player's three authored decisions (in order): ${JSON.stringify(trace.chosen)}`;
  const history=`Ordered player decisions: ${JSON.stringify(path)}`;
  const previous=prior?.story_text ? `Prior SAVED continuation: ${prior.story_text.slice(-2700)}` : '';
  const request=mode==='intro'
    ? 'This third-decision scene is already authored; do not rewrite it. Return exactly THREE distinct, specific next responses for Maya, each with short text. story_text is a stub and is NOT displayed at this stage.'
    : `The player has just decided: ${selected?.text || '(unknown)'}. Write a NEW continuation: 5-12 short paragraphs separated by blank lines, vivid but concise, with grounded dialogue and clear consequences. Continue from the previous scene; do not reset the story. Offer exactly THREE distinct next decisions for Maya (with id/text). Never repeat a prior scene.`;
  return [source,decisions,recap,history,previous,request].filter(Boolean).join('\n\n').slice(-11000);
}
