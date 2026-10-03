import { ApiError } from './problem';

//----------------------------------------------------------\\
//                              TOKEN STORE
//----------------------------------------------------------\\

//the access token lives in memory only, never in browser storage.
//a page reload gets a fresh one from the HttpOnly refresh cookie
let accessToken: string | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

//----------------------------------------------------------\\
//                              SESSION EXPIRY
//----------------------------------------------------------\\

let sessionExpiredHandler: (() => void) | null = null;

//the auth provider registers here so a dead session lands the user back on login
export function setSessionExpiredHandler(handler: (() => void) | null) {
  sessionExpiredHandler = handler;
}

//----------------------------------------------------------\\
//                              REFRESH
//----------------------------------------------------------\\

//refresh tokens are single-use and reusing one revokes the whole chain, so only one
//refresh may be in flight. every caller that lands meanwhile waits on the same promise
let refreshing: Promise<boolean> | null = null;

export function refreshSession(): Promise<boolean> {
  refreshing ??= fetch('/api/auth/refresh', { method: 'POST' })
    .then(async (res) => {
      if (!res.ok) {
        setAccessToken(null);
        return false;
      }
      const body = (await res.json()) as { accessToken: string };
      setAccessToken(body.accessToken);
      return true;
    })
    .catch(() => {
      setAccessToken(null);
      return false;
    })
    .finally(() => {
      refreshing = null;
    });

  return refreshing;
}

//----------------------------------------------------------\\
//                              FETCH
//----------------------------------------------------------\\

export type ApiRequest = Omit<RequestInit, 'body'> & {
  json?: unknown; //serialised for you, with the content type set
  body?: FormData; //uploads, the browser sets the multipart boundary itself
};

//paths are relative to /api, so apiFetch('/me') calls /api/me on the same origin
export async function apiFetch<T>(path: string, request: ApiRequest = {}): Promise<T> {
  const res = await send(path, request);

  //auth endpoints answer 401 for wrong credentials, so only other calls try a refresh
  if (res.status === 401 && !path.startsWith('/auth/')) {
    if (await refreshSession()) return read<T>(await send(path, request));

    sessionExpiredHandler?.();
    throw await ApiError.fromResponse(res);
  }

  return read<T>(res);
}

async function send(path: string, { json, headers, ...init }: ApiRequest) {
  const merged = new Headers(headers);
  if (accessToken) merged.set('Authorization', `Bearer ${accessToken}`);
  if (json !== undefined) merged.set('Content-Type', 'application/json');

  try {
    return await fetch(`/api${path}`, {
      ...init,
      headers: merged,
      body: json !== undefined ? JSON.stringify(json) : init.body,
    });
  } catch {
    throw ApiError.network();
  }
}

async function read<T>(res: Response): Promise<T> {
  if (!res.ok) throw await ApiError.fromResponse(res);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
