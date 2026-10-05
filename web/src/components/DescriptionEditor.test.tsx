import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { DescriptionEditor } from './DescriptionEditor';

//the editor with a label, holding its own value the way react-hook-form would
function Harness({ start = '' }: { start?: string }) {
  const [value, setValue] = useState(start);
  return (
    <>
      <label htmlFor="description">Description</label>
      <DescriptionEditor id="description" value={value} onChange={setValue} />
    </>
  );
}

const textbox = () => screen.getByLabelText<HTMLTextAreaElement>('Description');

function select(start: number, end: number) {
  textbox().focus();
  textbox().setSelectionRange(start, end);
}

describe('DescriptionEditor', () => {
  it('wraps the selected text in bold, and keeps it selected', async () => {
    render(<Harness start="call security first" />);

    select(5, 13);
    await userEvent.click(screen.getByRole('button', { name: 'Bold' }));

    expect(textbox()).toHaveValue('call **security** first');
    expect(textbox()).toHaveFocus();
    expect(textbox().value.slice(textbox().selectionStart, textbox().selectionEnd)).toBe('security');
  });

  it('formats from the keyboard too', async () => {
    render(<Harness start="before load-in" />);

    select(0, 6);
    await userEvent.keyboard('{Control>}i{/Control}');

    expect(textbox()).toHaveValue('_before_ load-in');
  });

  it('turns lines into a list and puts a link in with the address ready to type', async () => {
    render(<Harness start={'ice\ncups'} />);

    select(0, 8);
    await userEvent.click(screen.getByRole('button', { name: 'Bulleted list' }));
    expect(textbox()).toHaveValue('- ice\n- cups');

    select(13, 13);
    await userEvent.click(screen.getByRole('button', { name: 'Link' }));
    expect(textbox()).toHaveValue('- ice\n- cups[link text](https://)');
  });

  it('previews the description the way the card will show it, and back again', async () => {
    render(<Harness start={'Call **security**\n\n- ice'} />);

    await userEvent.click(screen.getByRole('button', { name: 'Preview' }));

    expect(screen.getByRole('button', { name: 'Preview' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByText('security').tagName).toBe('STRONG');
    expect(screen.getByRole('listitem')).toHaveTextContent('ice');
    expect(screen.getByRole('button', { name: 'Bold' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Preview' }));
    expect(textbox()).toBeVisible();
  });

  it('never runs markup typed into it', async () => {
    const { container } = render(<Harness start={'<img src=x onerror="alert(1)">'} />);

    await userEvent.click(screen.getByRole('button', { name: 'Preview' }));

    expect(container.querySelector('img')).toBeNull();
    expect(screen.getByText('<img src=x onerror="alert(1)">', { selector: 'p' })).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<Harness start="text" />);
    expect(await axeViolations(container)).toEqual([]);
  });
});
