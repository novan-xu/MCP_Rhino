// Pure runtime analysis of inspect_block_members results; no Rhino/file access.
export function quadSizeDifference(a, b) {
  const distance = (points, i, j) => Math.hypot(...points[i].map((x, k) => x - points[j][k]));
  if (a.length !== 4 || b.length !== 4 || [...a, ...b].some(p => p.length !== 3 || p.some(x => !Number.isFinite(x))))
    throw new Error('Expected four finite 3D corners.');
  let best = Infinity;
  for (const direction of [1, -1]) for (let shift = 0; shift < 4; shift++) {
    const map = i => (shift + direction * i + 8) % 4;
    let worst = 0;
    for (let i = 0; i < 4; i++) for (let j = i + 1; j < 4; j++)
      worst = Math.max(worst, Math.abs(distance(a, i, j) - distance(b, map(i), map(j))));
    best = Math.min(best, worst);
  }
  return best;
}

export function compareStoneSizes(members, tolerance) {
  if (!(tolerance > 0) || !Number.isFinite(tolerance)) throw new Error('Invalid tolerance.');
  const groups = new Map(), missingIds = [], nonSurfaces = [];
  for (const member of members) {
    if (!member.geometry.isSurface) { nonSurfaces.push(member); continue; }
    const id = Object.entries(member.userText).find(([key]) => key.toUpperCase() === 'STN_ID')?.[1]?.trim();
    if (!id) { missingIds.push(member); continue; }
    if (!groups.has(id)) groups.set(id, []);
    groups.get(id).push(member);
  }
  const result = [];
  for (const [stnId, occurrences] of groups) {
    const unsupported = occurrences.filter(m => m.geometry.status !== 'PlanarQuad');
    const supported = occurrences.filter(m => m.geometry.status === 'PlanarQuad');
    let maxDifference = 0, worstPair = null, mismatchPairCount = 0;
    const involved = new Set();
    for (let i = 0; i < supported.length; i++) for (let j = i + 1; j < supported.length; j++) {
      const difference = quadSizeDifference(supported[i].geometry.corners, supported[j].geometry.corners);
      if (difference > maxDifference) { maxDifference = difference; worstPair = [supported[i], supported[j]]; }
      if (difference > tolerance + 1e-8) { mismatchPairCount++; involved.add(supported[i]); involved.add(supported[j]); }
    }
    result.push({stnId, occurrenceCount:occurrences.length, status:mismatchPairCount ? 'Mismatch' : unsupported.length ? 'Unchecked' : occurrences.length === 1 ? 'Singleton' : 'Match',
      maxDifference, mismatchPairCount, unsupported, worstPair, involved:[...involved], occurrences});
  }
  result.sort((a,b) => a.stnId.localeCompare(b.stnId));
  return {surfaceCount:members.length - nonSurfaces.length,
    unsupportedSurfaceCount:members.filter(m=>m.geometry.isSurface&&m.geometry.status!=='PlanarQuad').length, groupCount:result.length,
    matchedGroups:result.filter(g=>g.status==='Match').length, mismatchedGroups:result.filter(g=>g.status==='Mismatch').length,
    uncheckedGroups:result.filter(g=>g.status==='Unchecked').length, singletonGroups:result.filter(g=>g.status==='Singleton').length,
    missingIds, nonSurfaces, groups:result};
}
