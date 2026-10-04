import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { KanbanColumn } from './KanbanColumn';

//pretends the screen is phone width, jsdom has no matchMedia of its own
function phoneWidth() {
  vi.stubGlobal(
    'matchMedia',
    (query: string) =>
      ({
        matches: query === '(max-width: 760px)',
        media: query,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
      }) as unknown as MediaQueryList,
  );
}

afterEach(() => vi.unstubAllGlobals());

function renderColumn(startsOpenOnPhone: boolean) {
  render(
    <KanbanColumn title="Finished" count={1} startsOpenOnPhone={startsOpenOnPhone}>
      <li>Delacroix Corporate Golf Day</li>
    </KanbanColumn>,
  );
}

describe('KanbanColumn', () => {
  it('shows its cards with no fold toggle on wider screens', () => {
    renderColumn(false);

    expect(screen.getByText('Delacroix Corporate Golf Day')).toBeVisible();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('folds away on phones until its heading is tapped', async () => {
    phoneWidth();
    renderColumn(false);

    const toggle = screen.getByRole('button', { name: /Finished/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('Delacroix Corporate Golf Day')).not.toBeVisible();

    await userEvent.click(toggle);

    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Delacroix Corporate Golf Day')).toBeVisible();
  });

  it('can start open on phones', () => {
    phoneWidth();
    renderColumn(true);

    expect(screen.getByRole('button', { name: /Finished/ })).toHaveAttribute('aria-expanded', 'true');
  });

  it('says so when the column is empty', () => {
    render(
      <KanbanColumn title="In Progress" count={0} emptyText="No events">
        {null}
      </KanbanColumn>,
    );

    expect(screen.getByText('No events')).toBeInTheDocument();
  });
});
