import { setupServer } from 'msw/node';

//tests add the handlers they need with server.use, anything unhandled fails the test
export const server = setupServer();
