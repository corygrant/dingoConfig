import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ReactFlow,
  ReactFlowProvider,
  Background,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  useReactFlow,
} from '@xyflow/react';
import FunctionNode from './FunctionNode.jsx';
import LiveEdge from './LiveEdge.jsx';
import { EditorContext, ValueStoreContext } from './context.js';

const nodeTypes = { function: FunctionNode };
const edgeTypes = { live: LiveEdge };
const deleteKeys = ['Backspace', 'Delete'];

// The graph is owned by .NET: every structural change is sent to Blazor, which
// updates the device config and pushes the resulting graph back via setGraph.
// Local state only carries UI concerns (selection, dragging, measured sizes).
function toNodes(graph, prevById) {
  return graph.nodes.map((n) => {
    const prev = prevById?.get(n.id);
    return {
      ...prev,
      id: n.id,
      type: 'function',
      position: prev?.dragging ? prev.position : { x: n.x, y: n.y },
      deletable: n.deletable,
      selected: prev?.selected ?? false,
      data: {
        label: n.label,
        subtitle: n.subtitle,
        category: n.category,
        enabled: n.enabled,
        inputs: n.inputs,
        outputs: n.outputs,
      },
    };
  });
}

function toEdges(graph, prevById) {
  return graph.edges.map((e) => ({
    id: e.id,
    type: 'live',
    source: e.source,
    sourceHandle: e.sourceHandle,
    target: e.target,
    targetHandle: e.targetHandle,
    selected: prevById?.get(e.id)?.selected ?? false,
    data: { var: e.var },
  }));
}

const byId = (items) => new Map(items.map((i) => [i.id, i]));

function Editor({ dotnet, initialGraph, api }) {
  const flow = useReactFlow();
  const wrapper = useRef(null);
  const [propertiesNodeId, setPropertiesNodeId] = useState(null);
  const [nodes, setNodes, onNodesChange] = useNodesState(toNodes(initialGraph));
  const [edges, setEdges, onEdgesChange] = useEdgesState(toEdges(initialGraph));

  const call = useCallback(
    (method, ...args) =>
      dotnet.invokeMethodAsync(method, ...args).catch((err) => {
        console.warn(`Flow editor: ${method} failed`, err);
        return undefined;
      }),
    [dotnet],
  );

  useEffect(() => {
    const setGraph = (graph) => {
      setNodes((prev) => toNodes(graph, byId(prev)));
      setEdges((prev) => toEdges(graph, byId(prev)));
    };

    api.setGraph = setGraph;
    api.fitView = () => flow.fitView({ padding: 0.15, duration: 200 });
    api.getViewportCenter = () => {
      const rect = wrapper.current.getBoundingClientRect();
      return flow.screenToFlowPosition({ x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 });
    };
    api.setPropertiesNode = setPropertiesNodeId;
    api.selectNode = (id) => {
      setNodes((ns) => ns.map((n) => ({ ...n, selected: n.id === id })));
      setEdges((es) => es.map((e) => (e.selected ? { ...e, selected: false } : e)));
    };

    if (api.pendingGraph) {
      setGraph(api.pendingGraph);
      api.pendingGraph = null;
    }

    return () => {
      api.setGraph = null;
    };
  }, [api, flow, setNodes, setEdges]);

  const handleInfo = useCallback(
    (nodeId, handleId, key) => flow.getNode(nodeId)?.data[key].find((h) => h.id === handleId),
    [flow],
  );

  const isValidConnection = useCallback(
    (c) => {
      const source = handleInfo(c.source, c.sourceHandle, 'outputs');
      const target = handleInfo(c.target, c.targetHandle, 'inputs');
      return !!source && !!target && source.dataTypes.some((t) => target.dataTypes.includes(t));
    },
    [handleInfo],
  );

  const onConnect = useCallback(
    (c) => call('OnConnect', c.source, c.sourceHandle, c.target, c.targetHandle),
    [call],
  );

  const onBeforeDelete = useCallback(
    async ({ nodes: deleting }) => {
      if (deleting.length === 0) return true;
      return (await call('ConfirmRemoveNodes', deleting.map((n) => n.id))) === true;
    },
    [call],
  );

  const onDelete = useCallback(
    ({ nodes: deleted, edges: deletedEdges }) =>
      call(
        'OnDeleted',
        deleted.map((n) => n.id),
        deletedEdges.map((e) => ({ target: e.target, targetHandle: e.targetHandle })),
      ),
    [call],
  );

  const onNodeDragStop = useCallback(
    (_event, _node, dragged) =>
      call('OnNodesMoved', dragged.map((n) => ({ id: n.id, x: n.position.x, y: n.position.y }))),
    [call],
  );

  const editor = useMemo(
    () => ({ propertiesNodeId, openProperties: (id) => call('OnOpenProperties', id) }),
    [propertiesNodeId, call],
  );

  return (
    <EditorContext.Provider value={editor}>
    <div ref={wrapper} className="df-root">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        isValidConnection={isValidConnection}
        onBeforeDelete={onBeforeDelete}
        onDelete={onDelete}
        onNodeDragStop={onNodeDragStop}
        deleteKeyCode={deleteKeys}
        colorMode="dark"
        minZoom={0.1}
        fitView
        fitViewOptions={{ padding: 0.15 }}
      >
        <Background gap={20} />
        <Controls showInteractive={false} />
        <MiniMap pannable zoomable nodeClassName={(n) => `df-mini-${n.data.category}`} />
      </ReactFlow>
    </div>
    </EditorContext.Provider>
  );
}

export default function FlowEditor({ dotnet, initialGraph, api, store }) {
  return (
    <ValueStoreContext.Provider value={store}>
      <ReactFlowProvider>
        <Editor dotnet={dotnet} initialGraph={initialGraph} api={api} />
      </ReactFlowProvider>
    </ValueStoreContext.Provider>
  );
}
