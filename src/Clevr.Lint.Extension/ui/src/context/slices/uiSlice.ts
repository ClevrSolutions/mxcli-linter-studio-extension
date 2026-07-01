export type SettingsTab = "modules" | "rules" | "configuration" | "sources" | "about";

export interface UIState {
  toast: { text: string; isError: boolean; id: number } | null;
  logoDataUri: string;
  settingsVisible: boolean;
  settingsActiveTab: SettingsTab;
}

export type UIAction =
  | { type: "SHOW_TOAST"; text: string; isError: boolean }
  | { type: "CLEAR_TOAST" }
  | { type: "SET_LOGO"; dataUri: string }
  | { type: "SHOW_SETTINGS" }
  | { type: "HIDE_SETTINGS" }
  | { type: "SET_SETTINGS_TAB"; tab: SettingsTab }
  | { type: "SCAN_ERROR"; error: string };

export const initialUIState: UIState = {
  toast: null,
  logoDataUri: "",
  settingsVisible: false,
  settingsActiveTab: "modules",
};

let toastId = 0;

export function uiReducer(state: UIState, action: UIAction): UIState {
  switch (action.type) {
    case "SHOW_TOAST":
      return { ...state, toast: { text: action.text, isError: action.isError, id: ++toastId } };
    case "CLEAR_TOAST":
      return { ...state, toast: null };
    case "SET_LOGO":
      return { ...state, logoDataUri: action.dataUri };
    case "SHOW_SETTINGS":
      return { ...state, settingsVisible: true };
    case "HIDE_SETTINGS":
      return { ...state, settingsVisible: false };
    case "SET_SETTINGS_TAB":
      return { ...state, settingsActiveTab: action.tab };
    case "SCAN_ERROR":
      return { ...state, toast: { text: action.error, isError: true, id: ++toastId } };
    default:
      return state;
  }
}
