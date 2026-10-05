import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './msw/server';

//the first test to hit a lazy route waits for vite to compile that chunk, which can take
//over the default 1s on a cold run or a slow ci runner
configure({ asyncUtilTimeout: 5000 });

//jsdom knows <dialog> but not showModal or close yet, this is just enough for Dialog to work
if (typeof HTMLDialogElement.prototype.showModal !== 'function') {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.removeAttribute('open');
    this.dispatchEvent(new Event('close'));
  };
}

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

//vitest globals are off, so testing library can't hook up its own cleanup
afterEach(() => {
  server.resetHandlers();
  cleanup();
});

afterAll(() => server.close());
