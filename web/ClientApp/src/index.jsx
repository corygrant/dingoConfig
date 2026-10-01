import '@xyflow/react/dist/style.css';
import './flow.css';
import { createRoot } from 'react-dom/client';
import FlowEditor from './FlowEditor.jsx';
import { createValueStore } from './valueStore.js';

// Loads the stylesheet emitted next to this module, carrying over its cache busting query.
// (Held in a variable so Vite doesn't try to resolve it as a build time asset.)
const stylesheetName = 'flow-editor.css';
let stylesheet;

function loadStylesheet() {
  stylesheet ??= new Promise((resolve) => {
    const href = new URL(stylesheetName, import.meta.url);
    href.search = new URL(import.meta.url).search;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href.toString();
    link.onload = link.onerror = () => resolve();
    document.head.appendChild(link);
  });
  return stylesheet;
}

// Entry point used by Blazor (FlowEditorTab.razor) through JS interop.
// Returns a handle whose methods are invoked via IJSObjectReference.
export async function create(element, dotnet, graph) {
  await loadStylesheet();

  const store = createValueStore();
  const api = {};
  const root = createRoot(element);
  root.render(<FlowEditor dotnet={dotnet} initialGraph={graph} api={api} store={store} />);

  return {
    setGraph(next) {
      if (api.setGraph) api.setGraph(next);
      else api.pendingGraph = next;
    },
    setValues(values) {
      store.set(values);
    },
    clearValues() {
      store.clear();
    },
    setPropertiesNode(id) {
      api.setPropertiesNode?.(id);
    },
    selectNode(id) {
      api.selectNode?.(id);
    },
    fitView() {
      api.fitView?.();
    },
    getViewportCenter() {
      return api.getViewportCenter?.() ?? { x: 0, y: 0 };
    },
    dispose() {
      root.unmount();
    },
  };
}
