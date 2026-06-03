const HAS_TIMEZONE_SUFFIX = /(?:Z|[+-]\d{2}:?\d{2})$/i;

export function parseApiDateTime(value: Date | string): Date {
  if (value instanceof Date) return value;
  const utcValue = HAS_TIMEZONE_SUFFIX.test(value) ? value : `${value}Z`;
  return new Date(utcValue);
}

export function toUtcIsoString(localDateTimeValue: string): string {
  return new Date(localDateTimeValue).toISOString();
}

export function toDatetimeLocalValue(value: Date | string): string {
  const date = parseApiDateTime(value);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 16);
}

export function isStarted(startTime: Date | string, now: Date = new Date()): boolean {
  const start = parseApiDateTime(startTime);
  return now.getTime() >= start.getTime();
}
