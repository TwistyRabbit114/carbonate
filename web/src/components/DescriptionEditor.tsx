import { useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react';
import { Bold, Italic, Link as LinkIcon, List, ListOrdered, type LucideIcon } from 'lucide-react';
import { insertLink, markupToHtml, toggleList, wrapSelection, type MarkupEdit } from '@/lib/richText';
import type { FieldControlProps } from './Field';
import { SanitisedDescription } from './SanitisedDescription';
import styles from './DescriptionEditor.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type DescriptionEditorProps = FieldControlProps & {
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  ref?: (element: HTMLTextAreaElement | null) => void; //react-hook-form's, so an error can focus the field
  name?: string;
  maxLength?: number;
};

export const formattingHint =
  'Select some text and use the buttons to format it. Preview shows how it will look.';

type Tool = {
  label: string;
  shortcut?: string; //the letter used with ctrl or cmd
  icon: LucideIcon;
  edit: (text: string, start: number, end: number) => MarkupEdit;
};

//bold, italic, lists and links, the formatting the plan allows on a card (section 7.5)
const tools: readonly Tool[] = [
  { label: 'Bold', shortcut: 'b', icon: Bold, edit: (t, s, e) => wrapSelection(t, s, e, '**', 'bold text') },
  {
    label: 'Italic',
    shortcut: 'i',
    icon: Italic,
    edit: (t, s, e) => wrapSelection(t, s, e, '_', 'italic text'),
  },
  { label: 'Bulleted list', icon: List, edit: (t, s, e) => toggleList(t, s, e, 'bullet') },
  { label: 'Numbered list', icon: ListOrdered, edit: (t, s, e) => toggleList(t, s, e, 'number') },
  { label: 'Link', shortcut: 'k', icon: LinkIcon, edit: insertLink },
];

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

//a plain textarea with buttons that type the markup for you, and a preview that shows it the way
//the card will. the textarea stays in the page during preview so its label and undo history do too
export function DescriptionEditor({
  value,
  onChange,
  onBlur,
  ref,
  name,
  maxLength,
  ...control
}: DescriptionEditorProps) {
  const textarea = useRef<HTMLTextAreaElement | null>(null);
  const selectAfter = useRef<MarkupEdit | null>(null);
  const [previewing, setPreviewing] = useState(false);

  //put the cursor back where the edit left it, once the new text is in the textarea
  useLayoutEffect(() => {
    const element = textarea.current;
    const edit = selectAfter.current;
    if (!edit || !element || element.value !== edit.text) return;
    selectAfter.current = null;
    element.focus();
    element.setSelectionRange(edit.start, edit.end);
  }, [value]);

  function apply(tool: Tool) {
    const element = textarea.current;
    if (!element) return;
    const edit = tool.edit(element.value, element.selectionStart, element.selectionEnd);
    if (edit.text === element.value) {
      element.focus();
      element.setSelectionRange(edit.start, edit.end);
      return;
    }
    selectAfter.current = edit;
    onChange(edit.text);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) return;
    const tool = tools.find((candidate) => candidate.shortcut === event.key.toLowerCase());
    if (!tool) return;
    event.preventDefault();
    apply(tool);
  }

  const preview = previewing ? markupToHtml(value) : null;

  return (
    <div className={styles.editor}>
      <div className={styles.bar}>
        <div className={styles.tools} role="group" aria-label="Formatting">
          {tools.map((tool) => {
            const keys = tool.shortcut && `Ctrl+${tool.shortcut.toUpperCase()}`;
            return (
              <button
                key={tool.label}
                type="button"
                className={styles.tool}
                aria-label={tool.label}
                aria-keyshortcuts={keys ? `Control+${tool.shortcut!.toUpperCase()}` : undefined}
                title={keys ? `${tool.label} (${keys})` : tool.label}
                disabled={previewing}
                onClick={() => apply(tool)}
              >
                <tool.icon aria-hidden="true" />
              </button>
            );
          })}
        </div>
        <button
          type="button"
          className={styles.mode}
          aria-pressed={previewing}
          onClick={() => setPreviewing((on) => !on)}
        >
          Preview
        </button>
      </div>

      <textarea
        {...control}
        ref={(element) => {
          textarea.current = element;
          ref?.(element);
        }}
        name={name}
        className={styles.textarea}
        value={value}
        rows={5}
        maxLength={maxLength}
        hidden={previewing}
        onChange={(event) => onChange(event.target.value)}
        onBlur={onBlur}
        onKeyDown={handleKeyDown}
      />

      {previewing && (
        <div className={styles.preview}>
          {preview ? (
            <SanitisedDescription html={preview} />
          ) : (
            <p className={styles.empty}>Nothing to preview yet.</p>
          )}
        </div>
      )}
    </div>
  );
}
