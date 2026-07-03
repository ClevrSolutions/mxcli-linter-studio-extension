export interface FilterState {
  categoryEnabled: Set<string>;
  severityEnabled: Set<string>;
  moduleFilterEnabled: Set<string>;
  appStoreVisible: boolean;
  uncommittedDocumentIds: Set<string>;
  uncommittedQualifiedNames: Set<string>;
  uncommittedAvailable: boolean;
  uncommittedFilterActive: boolean;
  uncommittedStatus: string;
  showExcluded: boolean;
  filterQuery: string;
  baselineFilter: "new" | "outside" | "fixed" | null;
}

export type FilterAction =
  | { type: "TOGGLE_CATEGORY"; category: string }
  | { type: "TOGGLE_SEVERITY"; severity: string }
  | { type: "TOGGLE_MODULE_FILTER"; moduleName: string }
  | { type: "TOGGLE_APPSTORE" }
  | { type: "SET_UNCOMMITTED_DOCUMENTS"; documentIds: string[]; qualifiedNames: string[]; available: boolean; status: string }
  | { type: "TOGGLE_UNCOMMITTED_FILTER" }
  | { type: "TOGGLE_SHOW_EXCLUDED" }
  | { type: "SET_FILTER_QUERY"; query: string }
  | { type: "RESET_FILTERS" }
  | { type: "SET_BASELINE_FILTER"; filter: "new" | "outside" | "fixed" | null }
  | { type: "SELECT_BASELINE"; id: string | null };

export const initialFilterState: FilterState = {
  categoryEnabled: new Set(),
  severityEnabled: new Set(),
  moduleFilterEnabled: new Set(),
  appStoreVisible: true,
  uncommittedDocumentIds: new Set<string>(),
  uncommittedQualifiedNames: new Set<string>(),
  uncommittedAvailable: false,
  uncommittedFilterActive: false,
  uncommittedStatus: "",
  showExcluded: false,
  filterQuery: "",
  baselineFilter: null,
};

function toggleSet(set: Set<string>, key: string): Set<string> {
  const next = new Set(set);
  if (next.has(key)) next.delete(key); else next.add(key);
  return next;
}

export function filterReducer(state: FilterState, action: FilterAction): FilterState {
  switch (action.type) {
    case "TOGGLE_CATEGORY":
      return { ...state, categoryEnabled: toggleSet(state.categoryEnabled, action.category) };
    case "TOGGLE_SEVERITY":
      return { ...state, severityEnabled: toggleSet(state.severityEnabled, action.severity) };
    case "TOGGLE_MODULE_FILTER":
      return { ...state, moduleFilterEnabled: toggleSet(state.moduleFilterEnabled, action.moduleName) };
    case "TOGGLE_APPSTORE":
      return { ...state, appStoreVisible: !state.appStoreVisible };
    case "SET_UNCOMMITTED_DOCUMENTS":
      return {
        ...state,
        uncommittedDocumentIds: new Set(action.documentIds.map((id) => id.toLowerCase())),
        uncommittedQualifiedNames: new Set(action.qualifiedNames.map((qn) => qn.toLowerCase())),
        uncommittedAvailable: action.available,
        uncommittedFilterActive: action.available ? state.uncommittedFilterActive : false,
        uncommittedStatus: action.status,
      };
    case "TOGGLE_UNCOMMITTED_FILTER":
      return { ...state, uncommittedFilterActive: !state.uncommittedFilterActive };
    case "TOGGLE_SHOW_EXCLUDED":
      return { ...state, showExcluded: !state.showExcluded };
    case "SET_FILTER_QUERY":
      return { ...state, filterQuery: action.query };
    case "RESET_FILTERS":
      return {
        ...state,
        categoryEnabled: new Set(),
        severityEnabled: new Set(),
        moduleFilterEnabled: new Set(),
        filterQuery: "",
        uncommittedFilterActive: false,
        baselineFilter: null,
      };
    case "SET_BASELINE_FILTER":
      return { ...state, baselineFilter: action.filter };
    case "SELECT_BASELINE":
      return { ...state, baselineFilter: null };
    default:
      return state;
  }
}
