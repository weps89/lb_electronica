import type { FocusEvent } from 'react';

export function selectAllOnFocus(e: FocusEvent<HTMLInputElement>) {
  e.currentTarget.select();
}
