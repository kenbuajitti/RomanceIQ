import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {thirdNode,validatePath,pathKey,normalizeChoices} from '../functions/romanceiq-story/logic.mjs';
const story=JSON.parse(readFileSync(new URL('../functions/romanceiq-story/story.json',import.meta.url)));
const local=['opening_choice:film','clip_choice:ask','decision_film_press:noah'];
test('first three choices resolve authored scene',()=>{
 const {path,ending}=validatePath(story,{storyId:story.storyId,storyVersion:story.version,path:local});
 assert.equal(path.length,3);assert.ok(ending.text);
});
test('bad authored prefix rejected',()=>{
 assert.throws(()=>validatePath(story,{storyId:story.storyId,storyVersion:story.version,path:['opening_choice:film','clip_choice:invalid','decision_film_press:noah']}));
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

test('all twelve prewritten branch endings are reachable and valid', () => {
  const found = new Set();
  const walk = (id,path=[]) => {
    const node = story.nodes.find(n => n.id === id);
    assert.ok(node, `Missing authored node ${id}`);
    if (path.length===3) {
      const ending=thirdNode(story,path);
      assert.equal(ending.id,id);
      assert.ok(ending.text.length>=150);
      assert.equal(validatePath(story,{storyId:story.storyId,storyVersion:story.version,path}).ending.id,id);
      found.add(id);return;
    }
    if(node.choices?.length)for(const choice of node.choices) walk(choice.next,[...path,`${id}:${choice.id}`]);
    else {assert.ok(node.next, `Premature ending: ${id}`);walk(node.next,path);}
  };
  walk(story.start);
  assert.equal(found.size,12);
  assert.equal(story.nodes.filter(n=>n.id.startsWith('ending_')).length,12);
});

test('older deployed story version is rejected, so cached stories cannot mix', () => {
  assert.throws(()=>validatePath(story,{storyId:story.storyId,storyVersion:'vn-release-1',path:local}));
});
