import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { Field } from './Field';

describe('Field', () => {
  it('links the hint and error to the control so screen readers read them out', () => {
    render(
      <Field label="Email" hint="Your work address" error="Enter an email address">
        {(control) => <input type="email" {...control} />}
      </Field>,
    );

    const input = screen.getByLabelText('Email');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('Your work address Enter an email address');
  });

  it('marks required fields in words, not just colour', () => {
    render(
      <Field label="Pack size" required>
        {(control) => <input {...control} />}
      </Field>,
    );

    expect(screen.getByLabelText('Pack size (required)')).toBeInTheDocument();
  });

  it('leaves the control valid when there is no error', () => {
    render(<Field label="Venue">{(control) => <input {...control} />}</Field>);

    expect(screen.getByLabelText('Venue')).not.toHaveAttribute('aria-invalid');
  });

  it('has no accessibility violations', async () => {
    const { container } = render(
      <Field label="Email" hint="Your work address" error="Enter an email address">
        {(control) => <input type="email" {...control} />}
      </Field>,
    );

    expect(await axeViolations(container)).toEqual([]);
  });
});
