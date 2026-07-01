import type { BaselineEntry } from "../../types";

export interface BaselineState {
  baselines: BaselineEntry[];
  selectedBaselineId: string | null;
}

export type BaselineAction =
  | { type: "SET_BASELINES"; baselines: BaselineEntry[] }
  | { type: "SELECT_BASELINE"; id: string | null }
  | { type: "SCAN_FINISHED"; completedAt?: number };

export const initialBaselineState: BaselineState = {
  baselines: [],
  selectedBaselineId: null,
};

export function baselineReducer(state: BaselineState, action: BaselineAction): BaselineState {
  switch (action.type) {
    case "SET_BASELINES": {
      const ids = new Set(action.baselines.map((b) => b.id));
      const selectedId = state.selectedBaselineId && ids.has(state.selectedBaselineId)
        ? state.selectedBaselineId
        : action.baselines[0]?.id ?? null;
      return { ...state, baselines: action.baselines, selectedBaselineId: selectedId };
    }
    case "SELECT_BASELINE":
      return { ...state, selectedBaselineId: action.id };
    case "SCAN_FINISHED": {
      const autoSelect = state.selectedBaselineId ?? state.baselines[0]?.id ?? null;
      return { ...state, selectedBaselineId: autoSelect };
    }
    default:
      return state;
  }
}
