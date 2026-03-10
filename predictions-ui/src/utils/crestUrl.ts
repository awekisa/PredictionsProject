/**
 * Naming convention: lowercase, spaces → hyphens, & → and, diacritics stripped
 */
const ALIASES: Record<string, string> = {
  'ivory coast': 'cote-d-ivoire',
  'korea republic': 'south-korea',
  "côte d'ivoire": 'cote-d-ivoire',
};

function normalize(name: string): string {
  const lower = name.toLowerCase();
  if (ALIASES[lower]) return ALIASES[lower];
  return lower
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .replace(/&/g, 'and')
    .replace(/\s+/g, '-');
}

/** Local team crest path: /crests/{normalized-name}.svg */
export function localCrestPath(teamName: string): string {
  return `/crests/${normalize(teamName)}.svg`;
}

/** Local tournament emblem path: /emblems/{normalized-name}.svg (with optional dark variant) */
export function localEmblemPath(tournamentName: string, theme?: 'light' | 'dark'): string {
  const base = normalize(tournamentName);
  return theme === 'dark' ? `/emblems/${base}-dark.svg` : `/emblems/${base}.svg`;
}
