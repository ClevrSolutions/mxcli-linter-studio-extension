export interface Draft<T> {
  saved: T;
  pending: T;
}

export function draftOf<T>(value: T): Draft<T> {
  return { saved: value, pending: value };
}

export function startEdit<T>(draft: Draft<T>): Draft<T> {
  return { ...draft, pending: draft.saved };
}

export function cancelEdit<T>(draft: Draft<T>): Draft<T> {
  return { ...draft, pending: draft.saved };
}

export function commit<T>(draft: Draft<T>, next: T): Draft<T> {
  return { saved: next, pending: next };
}

export function editPending<T>(draft: Draft<T>, pending: T): Draft<T> {
  return { ...draft, pending };
}
