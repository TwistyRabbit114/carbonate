import { http, HttpResponse } from 'msw';
import { signedInSession } from './fixtures/sessions';
import { users } from './fixtures/users';
import { accessTokenFor, handlers, resetMocks, type AccountKey } from './msw/handlers';
import { renderApp } from './renderApp';

//the real screens against the whole mock api, signed in as one of the demo accounts. the mock
//knows who's asking from the token, so each role gets back what the api would give it
export function renderMockedApp(account: AccountKey, route: string) {
  resetMocks();
  return renderApp({
    me: users[account],
    route,
    handlers: [
      http.post('/api/auth/refresh', () => HttpResponse.json(signedInSession(accessTokenFor(account)))),
      ...handlers,
    ],
  });
}
