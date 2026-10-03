import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

//vitest globals are off, so testing library can't hook up its own cleanup
afterEach(() => cleanup());
