import { baselineReducer, initialBaselineState, type BaselineAction, type BaselineState } from "./slices/baselineSlice";
import { configReducer, initialConfigState, type ConfigAction, type ConfigState } from "./slices/configSlice";
import { filterReducer, initialFilterState, type FilterAction, type FilterState } from "./slices/filterSlice";
import { initialScanState, scanReducer, type ScanAction, type ScanState } from "./slices/scanSlice";
import { initialUIState, uiReducer, type UIAction, type UIState } from "./slices/uiSlice";

export type { ScanDescribePayload, ScanFastPayload } from "./slices/scanSlice";

export interface AppState {
  scan: ScanState;
  config: ConfigState;
  filters: FilterState;
  baseline: BaselineState;
  ui: UIState;
}

export type AppAction = ScanAction | ConfigAction | FilterAction | BaselineAction | UIAction;

export const initialState: AppState = {
  scan: initialScanState,
  config: initialConfigState,
  filters: initialFilterState,
  baseline: initialBaselineState,
  ui: initialUIState,
};

export function appReducer(state: AppState, action: AppAction): AppState {
  return {
    scan: scanReducer(state.scan, action as ScanAction),
    config: configReducer(state.config, action as ConfigAction),
    filters: filterReducer(state.filters, action as FilterAction),
    baseline: baselineReducer(state.baseline, action as BaselineAction),
    ui: uiReducer(state.ui, action as UIAction),
  };
}
