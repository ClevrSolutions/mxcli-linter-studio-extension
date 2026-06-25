import { createContext, Dispatch, useContext } from "react";
import type { AppAction, AppState } from "./AppReducer";

export const AppContext = createContext<AppState>(null!);
export const AppDispatch = createContext<Dispatch<AppAction>>(null!);

export function useAppState(): AppState {
  return useContext(AppContext);
}

export function useAppDispatch(): Dispatch<AppAction> {
  return useContext(AppDispatch);
}
