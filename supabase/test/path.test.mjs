import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {thirdNode,validatePath,pathKey,normalizeChoices} from '../functions/romanceiq-story/logic.mjs';
const story=JSON.parse(readFileSync(new URL('../functions/romanceiq-story/story.json',import.meta.url)));
const local=['permission:film','tag:ask','honesty_known:honest'];
test('first three choices resolve authored scene',()=>{
 const {path,ending}=validatePath(story,{storyId:story.storyId,storyVersion:story.version,path:local});
 assert.equal(path.length,3);assert.ok(ending.text);
});
test('bad authored prefix rejected',()=>{
 assert.throws(()=>validatePath(story,{storyId:story.storyId,storyVersion:story.version,path:['permission:film','tag:invalid','honesty_known:honest']}));
});
test('valid dynamic prefix uses third node then live_4',()=>{
 const ending=thirdNode(story,local);
 const p=[...local,`${ending.id}:choice_1`,'live_4:choice_2'];
 assert.equal(validatePath(story,{storyId:story.storyId,storyVersion:story.version,path:p}).path.length,5);
 assert.throws(()=>validatePath(story,{storyId:story.storyId,storyVersion:story.version,path:[...local,'wrong:choice_1']}));
});
test('same path always has same key, different path does not',async()=>{
 const x=await pathKey(story,'intro',local);
 assert.equal(x,await pathKey(story,'intro',local));
 assert.notEqual(x,await pathKey(story,'scene',local));
 assert.ok(x.startsWith('ri-v1-'));
});
test('three distinct decisions and no duplicate ids',()=>{
 const v=normalizeChoices([{text:'Wait for the audience'},{text:'Tell Noah tonight'},{text:'Ask Eli about the film'}]);
 assert.equal(v.length,3);assert.equal(new Set(v.map(x=>x.id)).size,3);
});
