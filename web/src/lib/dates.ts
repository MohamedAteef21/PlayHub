export function startOfMonth(): string {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10);
}

export function today(): string {
  return new Date().toISOString().slice(0, 10);
}

export function toIsoDate(dateStr: string): string {
  return new Date(dateStr + 'T00:00:00').toISOString();
}

export function toIsoDateEnd(dateStr: string): string {
  return new Date(dateStr + 'T23:59:59').toISOString();
}
