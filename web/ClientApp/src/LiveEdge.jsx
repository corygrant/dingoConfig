import { memo } from 'react';
import { BaseEdge, getBezierPath } from '@xyflow/react';
import { useLiveValue } from './context.js';

// Bezier edge that lights up while its source variable is a true boolean.
function LiveEdge({ id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, markerEnd, selected, data }) {
  const value = useLiveValue(data?.var);
  const [path] = getBezierPath({ sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition });

  const classes = ['df-edge', value === true ? 'df-edge-active' : '', selected ? 'df-edge-selected' : ''].join(' ');
  return <BaseEdge id={id} path={path} markerEnd={markerEnd} className={classes} />;
}

export default memo(LiveEdge);
