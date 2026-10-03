import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './msw/server';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

//vitest globals are off, so testing library can't hook up its own cleanup
afterEach(() => {
  server.resetHandlers();
  cleanup();
});

afterAll(() => server.close());
