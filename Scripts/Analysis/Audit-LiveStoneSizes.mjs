// Uses only the installed Router's canonical MCP stdio transport and live read tools.
// Usage: node Scripts/Analysis/Audit-LiveStoneSizes.mjs <saved-file-path> <new-output.json>
import {spawn} from 'node:child_process';
import {createInterface} from 'node:readline';
import {writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {compareStoneSizes} from './Compare-StoneSizes.mjs';

const [filePath,output] = process.argv.slice(2);
if (!filePath || !output) throw new Error('Supply exact saved document path and new JSON output path.');
const router=spawn(join(process.env.LOCALAPPDATA,'MCP_Rhino','bin','MCP_Rhino.Router.exe'),[],{windowsHide:true,stdio:['pipe','pipe','pipe']});
const pending=new Map(); let next=0,stderr='';
router.stderr.on('data',d=>stderr=(stderr+d).slice(-4000));
router.on('error',error=>{for(const p of pending.values())p.reject(error)});
router.on('exit',code=>{for(const p of pending.values()){clearTimeout(p.timer);p.reject(new Error(`Router exited ${code}: ${stderr}`))}pending.clear()});
createInterface({input:router.stdout}).on('line',line=>{
  const message=JSON.parse(line), p=pending.get(message.id);
  if(p){clearTimeout(p.timer);pending.delete(message.id);message.error?p.reject(new Error(JSON.stringify(message.error))):p.resolve(message.result)}
});
const rpc=(method,params={})=>new Promise((resolve,reject)=>{
  const id=++next,timer=setTimeout(()=>{pending.delete(id);reject(new Error('MCP timeout: '+method))},45000);
  pending.set(id,{resolve,reject,timer});router.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');
});
const toolsUsed=new Set();
async function call(name,args={}) {
  toolsUsed.add(name);
  const response=await rpc('tools/call',{name,arguments:args});
  const body=response.structuredContent??JSON.parse(response.content.find(c=>c.type==='text').text);
  if(response.isError||body.success===false)throw new Error(name+': '+JSON.stringify(body));
  return body.data??body;
}
const samePath=(a,b)=>resolve(a).toLowerCase()===resolve(b).toLowerCase();
const hash=value=>createHash('sha256').update(JSON.stringify(value)).digest('hex');
try {
  await rpc('initialize',{protocolVersion:'2024-11-05',capabilities:{},clientInfo:{name:'STN size audit',version:'1.0'}});
  router.stdin.write(JSON.stringify({jsonrpc:'2.0',method:'notifications/initialized'})+'\n');
  const docs=await call('rhino_router_list_documents');
  const matches=docs.documents.filter(d=>d.routable&&d.filePath&&samePath(d.filePath,filePath));
  if(matches.length!==1)throw new Error('Expected exactly one routable open document for this path.');
  await call('rhino_router_select_document',{sessionId:matches[0].sessionId});
  const catalog=await rpc('tools/list');
  if(!catalog.tools.some(t=>t.name==='inspect_block_members'))throw new Error('Updated plug-in is not loaded. Save/close Rhino, install the staged package in host PowerShell, reopen Rhino and this document.');
  const summary=await call('get_document_summary',{filePath,maxObjectSummaries:0,maxLayerSummaries:0,maxMaterials:0,maxNamedViews:0});
  if(summary.unitSystem!=='Inches')throw new Error('This audit recipe expects an inch document; explicit unit conversion required.');
  const instanceData=await call('list_block_instances',{filePath,includeHidden:true});
  const instances=instanceData.instances;
  if(!instances.length)throw new Error('No block instances found.');
  const loose=await call('filter_objects',{filePath,objectTypes:['Surface','Brep','Extrusion']});
  if(loose.matchedCount)throw new Error('Standalone surfaces are present; extend audit scope before claiming complete coverage.');
  async function readMembers() {
    const members=[];
    for(let i=0;i<instances.length;i+=50) {
      const ids=instances.slice(i,i+50).map(o=>o.objectId);
      const data=await call('inspect_block_members',{filePath,objectIds:ids});
      if(data.unitSystem!=='Inches'||data.rootInstanceCount!==ids.length||data.leafCount!==data.members.length)throw new Error('Inconsistent inspection coverage or units.');
      members.push(...data.members);
    }
    return members;
  }
  const members=await readMembers();
  // Re-read the complete scope to reject edits observed while collecting batches.
  if(hash(members)!==hash(await readMembers())||hash(instanceData)!==hash(await call('list_block_instances',{filePath,includeHidden:true})))
    throw new Error('Live model changed during inspection; rerun while edits are paused.');
  const analysis=compareStoneSizes(members,1/32);
  const report={filePath,sessionId:matches[0].sessionId,inspectedAtUtc:new Date().toISOString(),units:'Inches',tolerance:1/32,
    method:'All pairs per trimmed STN_ID; best cyclic/reversed correspondence; each of four edge lengths and two diagonals differs by at most tolerance. Mirror allowed. Unsupported shapes remain unchecked.',
    rootInstanceCount:instances.length,leafCount:members.length,...analysis};
  await writeFile(output,JSON.stringify(report,null,2),{flag:'wx'});
  await call('append_activity_log',{logRoot:resolve(import.meta.dirname,'../../Runtime_Log'),task:'Compare live stone surface sizes by STN_ID at 1/32 inch tolerance using block-member inspection.',tools:[...toolsUsed,'append_activity_log']});
  console.log(JSON.stringify({output,surfaces:analysis.surfaceCount,groups:analysis.groupCount,matched:analysis.matchedGroups,mismatched:analysis.mismatchedGroups,
    unchecked:analysis.uncheckedGroups,unsupportedSurfaces:analysis.unsupportedSurfaceCount,singletons:analysis.singletonGroups,missingIds:analysis.missingIds.length},null,2));
} finally {router.stdin.end();}
