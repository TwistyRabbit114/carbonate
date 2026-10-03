import { render } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import type { MeResponse } from '@/api/types';
import { Providers } from '@/app/Providers';
import { routes } from '@/app/router';
import { server } from './msw/server';

type RenderAppOptions = {
  me: MeResponse | null; //null means no live session, so the refresh cookie is rejected
  route?: string;
};

//renders the real route table and providers, with the session coming from msw the same way
//it would from the api: refresh first, then /me
export function renderApp({ me, route = '/' }: RenderAppOptions) {
  server.use(
    http.post('/api/auth/refresh', () =>
      me ? HttpResponse.json({ accessToken: 'test-token' }) : new HttpResponse(null, { status: 401 }),
    ),
    http.get('/api/me', () => (me ? HttpResponse.json(me) : new HttpResponse(null, { status: 401 }))),
    http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
  );

  const router = createMemoryRouter(routes, { initialEntries: [route] });
  const view = render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );

  return { ...view, router };
}
