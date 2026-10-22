// Shared, persistent RomanceIQ chapter cache via the owner's existing Supabase
// story_nodes table and secret-authenticated private dynamic-function.
// Keep every secret server-side. Client requests never contain keys.
import story from './story.json' with { type:'json' };
import { DEFAULT_ALLOWED_ORIGINS, buildContext, normalizeChoices, pathKey,
         toScene, validatePath, parseEntry } from './logic.mjs';
const supabaseUrl=(Deno.env.get('SUPABASE_URL') || '').replace(/\/$/,'');
const allSecrets=Deno.env.get('SUPABASE_SECRET_KEYS');
let secretMap:Record<string,string>={};
try {secretMap=allSecrets?JSON.parse(allSecrets):{};} catch { /* no map */ }
const dbKey=secretMap.default || Deno.env.get('SUPABASE_SERVICE_ROLE_KEY') || '';
const privateKey=Deno.env.get('FAMILYRATE_PRIVATE_API_KEY') || '';
const allowedOrigins=(Deno.env.get('ROMANCEIQ_ALLOWED_ORIGINS') || DEFAULT_ALLOWED_ORIGINS.join(','))
    .split(',').map(s=>s.trim()).filter(Boolean);
type AnyObj=Record<string,any>;
function reply(req:Request,data:AnyObj,status=200):Response {
 const origin=req.headers.get('origin');
 const headers:Record<string,string>={'content-type':'application/json; charset=utf-8',
   'cache-control':'no-store',vary:'Origin',
   'access-control-allow-methods':'GET,POST,OPTIONS','access-control-allow-headers':'content-type'};
 if (origin && allowedOrigins.includes(origin)) headers['access-control-allow-origin']=origin;
 return new Response(JSON.stringify(data),{status,headers});
}
function fail(req:Request,status:number,msg:string){return reply(req,{error:'request_failed',message:msg},status);}
async function readNode(key:string):Promise<any|null> {
 const url=new URL(`${supabaseUrl}/rest/v1/story_nodes`);
 url.searchParams.set('path_key','eq.'+key);
 url.searchParams.set('select','path_key,parent_path_key,level,story_text,choices,generation_status');
 const res=await fetch(url.toString(),{headers:{apikey:dbKey,accept:'application/json'}});
 if(!res.ok) throw new Error(`Database read failed HTTP ${res.status}`);
 const rows=await res.json();return rows?.[0]||null;
}
async function generate(key:string,parentKey:string|null,level:number,context:string) {
 const resp=await fetch(`${supabaseUrl}/functions/v1/dynamic-function`,{
   method:'POST',headers:{apikey:privateKey,'content-type':'application/json'},
   body:JSON.stringify({path_key:key,parent_path_key:parentKey,level,story_context:context})});
 let data:any={};try{data=await resp.json();}catch{/* report below */}
 if(resp.status===202 || data.status==='pending' || data.status==='generating') return {pending:true};
 if(!resp.ok || data.status!=='ready' || typeof data.story_text!=='string' || !Array.isArray(data.choices))
   throw new Error(`Private story generator HTTP ${resp.status}`);
 return {pending:false,row:{...data,generation_status:'ready'},cached:false};
}
async function ready(key:string,parent:string|null,level:number,context:string) {
 const row=await readNode(key);
 if(row?.generation_status==='ready') return {pending:false,row,cached:true};
 if(row?.generation_status==='pending') return {pending:true};
 const token=await fetch(`${supabaseUrl}/rest/v1/rpc/romanceiq_consume_generation_budget`,{
   method:'POST',headers:{apikey:dbKey,'content-type':'application/json'},body:'{}'});
 if(!token.ok) throw new Error('RomanceIQ generation budget migration has not been installed.');
 if((await token.json())!==true) throw new Error('BUDGET_EXHAUSTED');
 return generate(key,parent,level,context);
}
async function scene(req:Request,payload:any) {
 const {path,ending}=validatePath(story,payload);
 if(path.length===3) {
   const key=await pathKey(story,'intro',path);
   const result=await ready(key,null,4,buildContext(story,'intro',path));
   if(result.pending) return reply(req,{status:'pending'},202);
   return reply(req,{scene:{id:ending.id,speaker:ending.speaker,text:ending.text,
     choices:normalizeChoices(result.row.choices)},cached:result.cached});
 }
 const parentPath=path.slice(0,-1);
 const parentKey=await pathKey(story,parentPath.length===3?'intro':'scene',parentPath);
 const parent=await readNode(parentKey);
 if(parent?.generation_status==='pending') return reply(req,{status:'pending'},202);
 if(parent?.generation_status!=='ready') return fail(req,409,'The previous chapter is unavailable. Return and retry.');
 const available=normalizeChoices(parent.choices);
 const [,choiceId]=parseEntry(path[path.length-1]);
 const selected=available.find(c=>c.id===choiceId);
 if(!selected) return fail(req,400,'That response is not part of this chapter.');
 const key=await pathKey(story,'scene',path);
 const cached=await readNode(key);
 if(cached?.generation_status==='ready') return reply(req,{scene:toScene(path,cached),cached:true});
 const result=await ready(key,parentKey,path.length,buildContext(story,'scene',path,parent,selected));
 if(result.pending) return reply(req,{status:'pending'},202);
 return reply(req,{scene:toScene(path,result.row),cached:result.cached});
}
Deno.serve(async(req:Request)=>{
 const origin=req.headers.get('origin');
 if(origin && !allowedOrigins.includes(origin)) return fail(req,403,'This site cannot access RomanceIQ.');
 if(req.method==='OPTIONS') return reply(req,{ok:true});
 if(req.method==='GET' && new URL(req.url).pathname.endsWith('/health'))
  return reply(req,{ok:true,service:'romanceiq-story',storyVersion:story.version});
 if(req.method!=='POST') return fail(req,405,'POST required.');
 if(!supabaseUrl || !dbKey || !privateKey) return fail(req,503,'Owner must configure the story gateway.');
 if(Number(req.headers.get('content-length')||0)>12000) return fail(req,413,'Request too large.');
 try {
   const raw=await req.text();if(raw.length>12000) return fail(req,413,'Request too large.');
   const payload=JSON.parse(raw);
   if(new URL(req.url).pathname.endsWith('/api/scene')) return await scene(req,payload);
   return fail(req,404,'Unknown API route.');
 } catch(e) {
   if(e instanceof SyntaxError) return fail(req,400,'Malformed request.');
   const detail=e instanceof Error?e.message:'Temporary error';
   console.error('RomanceIQ gateway:',detail);
   if(/^(Invalid|Unknown RomanceIQ)/.test(detail)) return fail(req,400,detail);
   if(detail==='BUDGET_EXHAUSTED') return fail(req,429,'Daily generation budget reached. Previously visited paths still work.');
   return fail(req,502,'Live continuation is temporarily unavailable. Retry shortly.');
 }
});
