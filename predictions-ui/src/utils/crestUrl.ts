/**
 * Naming convention: lowercase, spaces → hyphens, & → and, diacritics stripped
 */
const ALIASES: Record<string, string> = {
  'ivory coast': 'cote-d-ivoire',
  'korea republic': 'south-korea',
  "côte d'ivoire": 'cote-d-ivoire',
};

const FLAG_COUNTRY_CODES: Record<string, string> = {
  'algeria': 'dz',
  'argentina': 'ar',
  'australia': 'au',
  'austria': 'at',
  'belgium': 'be',
  'brazil': 'br',
  'canada': 'ca',
  'cape verde': 'cv',
  'colombia': 'co',
  'croatia': 'hr',
  'côte d\'ivoire': 'ci',
  'curacao': 'cw',
  'curaçao': 'cw',
  'ecuador': 'ec',
  'egypt': 'eg',
  'england': 'gb-eng',
  'france': 'fr',
  'germany': 'de',
  'ghana': 'gh',
  'haiti': 'ht',
  'iran': 'ir',
  'ivory coast': 'ci',
  'japan': 'jp',
  'jordan': 'jo',
  'korea republic': 'kr',
  'mexico': 'mx',
  'morocco': 'ma',
  'netherlands': 'nl',
  'new zealand': 'nz',
  'norway': 'no',
  'panama': 'pa',
  'paraguay': 'py',
  'portugal': 'pt',
  'qatar': 'qa',
  'saudi arabia': 'sa',
  'scotland': 'gb-sct',
  'senegal': 'sn',
  'south africa': 'za',
  'spain': 'es',
  'switzerland': 'ch',
  'tunisia': 'tn',
  'uruguay': 'uy',
  'usa': 'us',
  'united states': 'us',
  'united states of america': 'us',
  'uzbekistan': 'uz',
};

function normalize(name: string): string {
  const lower = name.toLowerCase();
  if (ALIASES[lower]) return ALIASES[lower];
  return lower
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .replace(/&/g, 'and')
    .replace(/\s+/g, '-');
}

export function localFlagPath(teamName: string): string | undefined {
  const countryCode = FLAG_COUNTRY_CODES[teamName.toLowerCase()];
  return countryCode ? `/flags/4x3/${countryCode}.svg` : undefined;
}

/** Local team crest path: prefer uploaded 4x3 flags for national teams, then /crests/{normalized-name}.svg */
export function localCrestPath(teamName: string): string {
  const flagPath = localFlagPath(teamName);
  return flagPath ?? `/crests/${normalize(teamName)}.svg`;
}

/** Local tournament emblem path: /emblems/{normalized-name}.svg (with optional dark variant) */
export function localEmblemPath(tournamentName: string, theme?: 'light' | 'dark'): string {
  const base = normalize(tournamentName);
  return theme === 'dark' ? `/emblems/${base}-dark.svg` : `/emblems/${base}.svg`;
}
