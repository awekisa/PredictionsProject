export function toUtcIsoString(localDateTimeValue: string): string {
  return new Date(localDateTimeValue).toISOString();
}

export function toDatetimeLocalValue(value: Date | string): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 16);
}

export function isStarted(startTime: Date | string, now: Date = new Date()): boolean {
  const start = typeof startTime === 'string' ? new Date(startTime) : startTime;
  return now.getTime() >= start.getTime();
}
