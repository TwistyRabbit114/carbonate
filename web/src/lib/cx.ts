//joins class names and drops the falsy ones, so conditional classes read cleanly
export function cx(...parts: Array<string | false | null | undefined>) {
  return parts.filter(Boolean).join(' ');
}
