// Tiny external store for live values so badge/edge updates only re-render
// the components subscribed to a changed key, not the whole graph.
export function createValueStore() {
  let values = {};
  const listeners = new Set();

  return {
    get: (key) => (key == null ? undefined : values[key]),
    set(next) {
      values = { ...values, ...next };
      listeners.forEach((l) => l());
    },
    clear() {
      values = {};
      listeners.forEach((l) => l());
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}
