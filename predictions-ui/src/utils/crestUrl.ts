/**
 * Naming convention: lowercase, spaces → hyphens, & → and
 */
function normalize(name: string): string {
  return name.toLowerCase().replace(/&/g, 'and').replace(/\s+/g, '-');
}

/** Local team crest path: /crests/{normalized-name}.svg */
export function localCrestPath(teamName: string): string {
  return `/crests/${normalize(teamName)}.svg`;
}

/** Local tournament emblem path: /emblems/{normalized-name}.svg */
export function localEmblemPath(tournamentName: string): string {
  return `/emblems/${normalize(tournamentName)}.svg`;
}
