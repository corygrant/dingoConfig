import { createContext, useContext, useSyncExternalStore } from 'react';

export const ValueStoreContext = createContext(null);

export function useLiveValue(key) {
  const store = useContext(ValueStoreContext);
  return useSyncExternalStore(store.subscribe, () => store.get(key));
}

// Editor level state/actions for nodes: which node's properties panel is open, and how to open one
export const EditorContext = createContext({ propertiesNodeId: null, openProperties: () => {} });

export const useEditor = () => useContext(EditorContext);
