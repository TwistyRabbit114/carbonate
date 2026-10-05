import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SanitisedDescription } from './SanitisedDescription';

//the one place html from the api reaches the page, so these are the xss cases from the plan

describe('SanitisedDescription', () => {
  it('shows the formatting a description is allowed', () => {
    render(<SanitisedDescription html="<p>Call <strong>security</strong> first</p><ul><li>ice</li></ul>" />);

    expect(screen.getByText('security').tagName).toBe('STRONG');
    expect(screen.getByRole('listitem')).toHaveTextContent('ice');
  });

  it('renders an <img onerror> without the image or its handler', () => {
    const { container } = render(<SanitisedDescription html={'<p>Hi<img src=x onerror="alert(1)"></p>'} />);

    expect(container.querySelector('img')).toBeNull();
    expect(container.innerHTML).not.toContain('onerror');
    expect(screen.getByText('Hi')).toBeInTheDocument();
  });

  it('drops scripts and javascript links, and opens real links safely in a new tab', () => {
    const { container } = render(
      <SanitisedDescription
        html={
          '<script>alert(1)</script><a href="javascript:alert(1)">bad</a> <a href="https://example.com">map</a>'
        }
      />,
    );

    expect(container.querySelector('script')).toBeNull();
    expect(container.innerHTML).not.toContain('javascript:');
    expect(screen.getByRole('link', { name: 'map' })).toHaveAttribute('rel', 'noopener noreferrer');
    expect(screen.getByRole('link', { name: 'map' })).toHaveAttribute('target', '_blank');
  });
});
