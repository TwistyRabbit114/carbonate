import { useMemo } from 'react';
import { sanitiseDescription } from '@/lib/richText';
import styles from './SanitisedDescription.module.scss';

type SanitisedDescriptionProps = {
  html: string;
};

//the only place in the app that puts html on the page, and the eslint config only lets this file
//do it. the api cleans descriptions on the way in and dompurify cleans them again here, against
//the same short list of tags, so a stored <img onerror> never runs (plan section 7.5)
export function SanitisedDescription({ html }: SanitisedDescriptionProps) {
  const clean = useMemo(() => sanitiseDescription(html), [html]);
  return <div className={styles.description} dangerouslySetInnerHTML={{ __html: clean }} />;
}
