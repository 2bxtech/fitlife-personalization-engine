/** Class start time in the visitor's own locale and time zone. */
export function formatClassTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}

export function spotsLeft(capacity: number, enrolled: number): number {
  return Math.max(0, capacity - enrolled)
}
