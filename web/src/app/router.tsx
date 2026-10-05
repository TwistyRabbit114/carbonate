import { lazy, type ReactNode } from 'react';
import { Navigate, type RouteObject } from 'react-router';
import { usePermissions } from '@/auth/AuthContext';
import { AuthLayout } from '@/auth/AuthLayout';
import type { Permission } from '@/auth/permissions';
import { RequireAuth } from './guards/RequireAuth';
import { RequirePermission } from './guards/RequirePermission';
import { AppShell } from './layouts/AppShell';
import { access, landingPath } from './navigation';
import { NotFoundPage } from './pages/NotFoundPage';
import { RouteErrorPage } from './pages/RouteErrorPage';

//----------------------------------------------------------\\
//                              PAGES
//----------------------------------------------------------\\

//every screen is its own chunk, so crew never download finance or stock code (NFR-06).
//login is split out too, since most visits restore the session and never show it
const LoginPage = lazy(() => import('@/auth/LoginPage'));
const MfaVerifyPage = lazy(() => import('@/auth/MfaVerifyPage'));
const MfaEnrolPage = lazy(() => import('@/auth/MfaEnrolPage'));

const EventsBoardPage = lazy(() => import('@/features/events/EventsBoardPage'));
const EventFormPage = lazy(() => import('@/features/events/EventFormPage'));
const EventDetailPage = lazy(() => import('@/features/events/EventDetailPage'));
const EventTaskBoardPage = lazy(() => import('@/features/event-board/EventTaskBoardPage'));
const EventCardDetailPage = lazy(() => import('@/features/event-board/EventCardDetailPage'));
const AdminTasksPage = lazy(() => import('@/features/admin-tasks/AdminTasksPage'));
const AdminTaskDetailPage = lazy(() => import('@/features/admin-tasks/AdminTaskDetailPage'));
const StockPage = lazy(() => import('@/features/stock/StockPage'));
const FinancePage = lazy(() => import('@/features/finance/FinancePage'));
const CalendarPage = lazy(() => import('@/features/calendar/CalendarPage'));
const SettingsPage = lazy(() => import('@/features/settings/SettingsPage'));
const MyEventsPage = lazy(() => import('@/features/crew/MyEventsPage'));
const CrewEventPage = lazy(() => import('@/features/crew/CrewEventPage'));
const MyTasksPage = lazy(() => import('@/features/crew/MyTasksPage'));
const ReportPage = lazy(() => import('@/features/crew/ReportPage'));
const ReportIncidentPage = lazy(() => import('@/features/incidents/ReportIncidentPage'));

//----------------------------------------------------------\\
//                              HELPERS
//----------------------------------------------------------\\

function guard(anyOf: readonly Permission[], page: ReactNode) {
  return <RequirePermission anyOf={anyOf}>{page}</RequirePermission>;
}

function LandingRedirect() {
  return <Navigate to={landingPath(usePermissions())} replace />;
}

//----------------------------------------------------------\\
//                              ROUTES
//----------------------------------------------------------\\

export const routes: RouteObject[] = [
  {
    errorElement: <RouteErrorPage />,
    children: [
      {
        path: 'login',
        element: <AuthLayout />,
        children: [
          { index: true, element: <LoginPage /> },
          { path: 'mfa', element: <MfaVerifyPage /> },
          { path: 'mfa-setup', element: <MfaEnrolPage /> },
        ],
      },
      {
        element: <RequireAuth />,
        children: [
          {
            element: <AppShell />,
            children: [
              { index: true, element: <LandingRedirect /> },
              { path: 'events', element: guard(access.events, <EventsBoardPage />) },
              { path: 'events/new', element: guard(access.newEvent, <EventFormPage />) },
              { path: 'events/:eventId', element: guard(access.eventDetail, <EventDetailPage />) },
              { path: 'events/:eventId/edit', element: guard(access.editEvent, <EventFormPage />) },
              { path: 'events/:eventId/board', element: guard(access.eventDetail, <EventTaskBoardPage />) },
              {
                path: 'events/:eventId/tasks/:cardId',
                element: guard(access.eventDetail, <EventCardDetailPage />),
              },
              {
                path: 'events/:eventId/incidents/new',
                element: guard(access.report, <ReportIncidentPage />),
              },
              { path: 'admin-tasks', element: guard(access.adminTasks, <AdminTasksPage />) },
              { path: 'admin-tasks/:cardId', element: guard(access.adminTasks, <AdminTaskDetailPage />) },
              { path: 'stock', element: guard(access.stock, <StockPage />) },
              { path: 'finance', element: guard(access.finance, <FinancePage />) },
              { path: 'calendar', element: guard(access.calendar, <CalendarPage />) },
              { path: 'settings', element: guard(access.settings, <SettingsPage />) },
              { path: 'my/events', element: guard(access.myEvents, <MyEventsPage />) },
              { path: 'my/events/:eventId', element: guard(access.myEvents, <CrewEventPage />) },
              { path: 'my/tasks', element: guard(access.myTasks, <MyTasksPage />) },
              { path: 'my/report', element: guard(access.report, <ReportPage />) },
            ],
          },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
