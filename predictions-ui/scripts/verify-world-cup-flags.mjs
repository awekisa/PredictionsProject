import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const worldCupFlagCases = [
  ['Algeria', 'dz'],
  ['Argentina', 'ar'],
  ['Australia', 'au'],
  ['Austria', 'at'],
  ['Belgium', 'be'],
  ['Brazil', 'br'],
  ['Canada', 'ca'],
  ['Cape Verde', 'cv'],
  ['Colombia', 'co'],
  ['Croatia', 'hr'],
  ['Côte d\'Ivoire', 'ci'],
  ['Curacao', 'cw'],
  ['Ecuador', 'ec'],
  ['Egypt', 'eg'],
  ['England', 'gb-eng'],
  ['France', 'fr'],
  ['Germany', 'de'],
  ['Ghana', 'gh'],
  ['Haiti', 'ht'],
  ['Iran', 'ir'],
  ['Ivory Coast', 'ci'],
  ['Japan', 'jp'],
  ['Jordan', 'jo'],
  ['Korea Republic', 'kr'],
  ['Mexico', 'mx'],
  ['Morocco', 'ma'],
  ['Netherlands', 'nl'],
  ['New Zealand', 'nz'],
  ['Norway', 'no'],
  ['Panama', 'pa'],
  ['Paraguay', 'py'],
  ['Portugal', 'pt'],
  ['Qatar', 'qa'],
  ['Saudi Arabia', 'sa'],
  ['Scotland', 'gb-sct'],
  ['Senegal', 'sn'],
  ['South Africa', 'za'],
  ['Spain', 'es'],
  ['Switzerland', 'ch'],
  ['Tunisia', 'tn'],
  ['Uruguay', 'uy'],
  ['USA', 'us'],
  ['Uzbekistan', 'uz'],
];

const repoRoot = new URL('../', import.meta.url).pathname;
const crestUrlSource = readFileSync(join(repoRoot, 'src/utils/crestUrl.ts'), 'utf8');
const missingAssets = [];
const missingMappings = [];

for (const [teamName, countryCode] of worldCupFlagCases) {
  const flagPath = join(repoRoot, 'public/flags/4x3', `${countryCode}.svg`);
  if (!existsSync(flagPath)) {
    missingAssets.push(`${teamName} -> ${countryCode}.svg`);
  }
  const lowerName = teamName.toLowerCase();
  const singleQuoted = `'${lowerName.replaceAll("'", "\\'")}': '${countryCode}'`;
  const doubleQuoted = `"${lowerName}": '${countryCode}'`;
  if (!crestUrlSource.includes(singleQuoted) && !crestUrlSource.includes(doubleQuoted)) {
    missingMappings.push(`${teamName} -> ${countryCode}`);
  }
}

if (!crestUrlSource.includes('export function localFlagPath')) {
  throw new Error('crestUrl.ts must export localFlagPath for country-code based flag assets.');
}

if (!crestUrlSource.includes('return flagPath ?? `/crests/${normalize(teamName)}.svg`;')) {
  throw new Error('localCrestPath must prefer uploaded 4x3 flag assets before falling back to crests.');
}

if (missingAssets.length || missingMappings.length) {
  throw new Error([
    missingAssets.length ? `Missing 4x3 flag assets:\n${missingAssets.join('\n')}` : '',
    missingMappings.length ? `Missing country-code mappings:\n${missingMappings.join('\n')}` : '',
  ].filter(Boolean).join('\n\n'));
}

console.log(`Verified ${worldCupFlagCases.length} World Cup team flag mappings and 4x3 SVG assets.`);
