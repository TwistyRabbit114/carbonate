import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';

//self-hosted so the content security policy needs no external font host.
//only the weights the design uses: manrope 600/700 for headings, inter 400/500/600 for body
import '@fontsource/manrope/600.css';
import '@fontsource/manrope/700.css';
import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@/styles/global.scss';

//`npm run dev:mocks` swaps the api for msw in the browser. the condition is fixed at build
//time, so a production build drops this branch and the mock code with it
if (import.meta.env.DEV && import.meta.env.MODE === 'mocks') {
  const { worker } = await import('./test/msw/browser');
  await worker.start({ onUnhandledRequest: 'bypass' });
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
