/**
 * Naming convention: lowercase, spaces → hyphens, & → and, diacritics stripped
 */
const ALIASES: Record<string, string> = {
  'ivory coast': 'cote-d-ivoire',
  'korea republic': 'south-korea',
  "côte d'ivoire": 'cote-d-ivoire',
};

const FLAG_COUNTRY_CODES: Record<string, string> = {
  'albania': 'al',
  'algeria': 'dz',
  'andorra': 'ad',
  'argentina': 'ar',
  'armenia': 'am',
  'australia': 'au',
  'austria': 'at',
  'azerbaijan': 'az',
  'belarus': 'by',
  'belgium': 'be',
  'bosnia and herzegovina': 'ba',
  'bosnia-h.': 'ba',
  'brazil': 'br',
  'bulgaria': 'bg',
  'cabo verde': 'cv',
  'canada': 'ca',
  'cape verde': 'cv',
  'colombia': 'co',
  'congo dr': 'cd',
  'croatia': 'hr',
  'curacao': 'cw',
  'curaçao': 'cw',
  'cyprus': 'cy',
  'czechia': 'cz',
  'côte d\'ivoire': 'ci',
  'denmark': 'dk',
  'dr congo': 'cd',
  'ecuador': 'ec',
  'egypt': 'eg',
  'england': 'gb-eng',
  'estonia': 'ee',
  'faroe islands': 'fo',
  'finland': 'fi',
  'france': 'fr',
  'georgia': 'ge',
  'germany': 'de',
  'ghana': 'gh',
  'gibraltar': 'gi',
  'greece': 'gr',
  'haiti': 'ht',
  'hungary': 'hu',
  'iceland': 'is',
  'iran': 'ir',
  'iraq': 'iq',
  'israel': 'il',
  'italy': 'it',
  'ivory coast': 'ci',
  'japan': 'jp',
  'jordan': 'jo',
  'kazakhstan': 'kz',
  'korea republic': 'kr',
  'kosovo': 'xk',
  'latvia': 'lv',
  'liechtenstein': 'li',
  'lithuania': 'lt',
  'luxembourg': 'lu',
  'malta': 'mt',
  'mexico': 'mx',
  'moldova': 'md',
  'montenegro': 'me',
  'morocco': 'ma',
  'netherlands': 'nl',
  'new zealand': 'nz',
  'north macedonia': 'mk',
  'northern ireland': 'gb-nir',
  'norway': 'no',
  'panama': 'pa',
  'paraguay': 'py',
  'poland': 'pl',
  'portugal': 'pt',
  'qatar': 'qa',
  'republic of ireland': 'ie',
  'romania': 'ro',
  'san marino': 'sm',
  'saudi arabia': 'sa',
  'scotland': 'gb-sct',
  'senegal': 'sn',
  'serbia': 'rs',
  'slovakia': 'sk',
  'slovenia': 'si',
  'south africa': 'za',
  'south korea': 'kr',
  'spain': 'es',
  'sweden': 'se',
  'switzerland': 'ch',
  'tunisia': 'tn',
  'turkey': 'tr',
  'türkiye': 'tr',
  'ukraine': 'ua',
  'united states of america': 'us',
  'united states': 'us',
  'uruguay': 'uy',
  'usa': 'us',
  'uzbekistan': 'uz',
  'wales': 'gb-wls',
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
  // The season slash is part of the display name, not an asset directory.
  const base = /^uefa nations league(?: \d{4}(?:[/-]\d{2,4})?)?$/i.test(tournamentName)
    ? 'uefa-nations-league'
    : normalize(tournamentName);
  return theme === 'dark' ? `/emblems/${base}-dark.svg` : `/emblems/${base}.svg`;
}
