import assert from 'node:assert/strict';
import {quadSizeDifference,compareStoneSizes} from '../../Scripts/Analysis/Compare-StoneSizes.mjs';
const rect=(width,height=5)=>[[0,0,0],[width,0,0],[width,height,0],[0,height,0]];
const a=rect(10), mirrored=a.map(([x,y,z])=>[-y+83,-x-40,z+5]);
assert.ok(quadSizeDifference(a,mirrored)<1e-9);
assert.ok(quadSizeDifference(a,[a[2],a[3],a[0],a[1]])<1e-9);
assert.ok(quadSizeDifference(a,[...a].reverse())<1e-9);
assert.ok(quadSizeDifference(a,rect(10.1))>.03125);
const member=(id,corners,status='PlanarQuad')=>({userText:{STN_ID:id},geometry:{isSurface:true,status,corners}});
// A and C both pass against central B, but A/C must fail the whole group.
const chain=compareStoneSizes([member('A',rect(10)),member('A',rect(10.03)),member('A',rect(10.06))],.03125);
assert.equal(chain.mismatchedGroups,1); assert.equal(chain.groups[0].mismatchPairCount,1);
assert.equal(compareStoneSizes([member('A',a),member('A',rect(10.03125))],.03125).matchedGroups,1);
assert.equal(compareStoneSizes([member('A',a),member('A',rect(10.0313))],.03125).mismatchedGroups,1);
assert.equal(compareStoneSizes([member('A',a),member('A',a,'Unsupported')],.03125).uncheckedGroups,1);
assert.equal(compareStoneSizes([member('A',a)],.03125).singletonGroups,1);
assert.equal(compareStoneSizes([member('  ',a)],.03125).missingIds.length,1);
assert.throws(()=>quadSizeDifference([],a));
console.log('PASS: size comparison tests (rigid transforms, corner order, tolerance boundary, all-pairs chaining, incomplete groups).');
