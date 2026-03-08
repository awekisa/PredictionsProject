/**
 * Resolves team crest URL: tries local SVG first, falls back to DB URL.
 * Local files live in /crests/{normalized-name}.svg
 * Naming convention: lowercase, spaces → hyphens, & → and
 */
export function localCrestPath(teamName: string): string {
  const normalized = teamName
    .toLowerCase()
    .replace(/&/g, 'and')
    .replace(/\s+/g, '-');
  return `/crests/${normalized}.svg`;
}
