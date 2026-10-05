import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { isApiError } from '@/api/problem';

//turning an api refusal into words on a form

//a 400's field errors put against the form's own fields. fieldFor maps the api's key (which can
//be "lines[2].quantity") to a field, or to nothing when the form has no such field. true if any
//landed, so the form knows whether it still needs a message of its own
export function applyFieldErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fieldFor: (key: string) => Path<T> | undefined,
): boolean {
  if (!isApiError(error) || error.status !== 400) return false;
  let applied = false;
  for (const [key, messages] of Object.entries(error.problem.errors ?? {})) {
    const field = fieldFor(key);
    if (field && messages[0]) {
      setError(field, { message: messages[0] });
      applied = true;
    }
  }
  return applied;
}

//for anything that didn't land on a field: the api's own explanation where it gave one, in
//plain words otherwise. failed says what didn't happen, like "It wasn't saved."
export function problemMessage(error: unknown, failed: string, forbidden = "Your role can't do that.") {
  if (!isApiError(error)) return `Something went wrong. ${failed} Try again.`;
  if (error.status === 0) return `Couldn't reach Carbonate. ${failed} Try again.`;
  if (error.status === 403) return forbidden;
  if (error.status === 501) return "That part of Carbonate isn't switched on yet.";
  return error.problem.detail ?? `Something went wrong. ${failed} Try again.`;
}

//a stale row version: someone saved in between (NFR-15). a 409 can also be a move that isn't
//allowed, which isn't this
export const isConflict = (error: unknown) =>
  isApiError(error) && error.status === 409 && Boolean(error.problem.type?.endsWith('/concurrency-conflict'));
