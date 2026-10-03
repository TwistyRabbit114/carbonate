import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { Providers } from './app/Providers';
import { routes } from './app/router';

const router = createBrowserRouter(routes);

export function App() {
  return (
    <Providers>
      <RouterProvider router={router} />
    </Providers>
  );
}
