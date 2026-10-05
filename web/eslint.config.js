import js from '@eslint/js';
import { defineConfig } from 'eslint/config';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import noUnsanitized from 'eslint-plugin-no-unsanitized';
import react from 'eslint-plugin-react';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

//spaced-comment stays off on purpose, comments here are written //like this

export default defineConfig(
  { ignores: ['dist', 'coverage', 'public/mockServiceWorker.js', 'src/api/schema.gen.ts'] },

  js.configs.recommended,
  tseslint.configs.recommended,
  reactHooks.configs.flat['recommended-latest'],
  jsxA11y.flatConfigs.recommended,

  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: { globals: globals.browser },
    settings: { react: { version: 'detect' } },
    plugins: { react, 'no-unsanitized': noUnsanitized },
    rules: {
      //xss rules from the security section of the plan
      'react/no-danger': 'error',
      'react/jsx-no-target-blank': 'error',
      'react/jsx-no-script-url': 'error',
      'no-unsanitized/method': 'error',
      'no-unsanitized/property': 'error',
      'no-eval': 'error',
      'no-implied-eval': 'error',
      'no-new-func': 'error',

      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      'no-console': 'error',
    },
  },

  //the one place raw html is allowed, the description goes through dompurify first
  {
    files: ['src/components/SanitisedDescription.tsx'],
    rules: { 'react/no-danger': 'off' },
  },

  {
    files: ['*.{js,ts}'],
    languageOptions: { globals: globals.node },
  },
);
