import { memo } from 'react';
import { BaseEdge, getBezierPath } from '@xyflow/react';
import { useLiveValue } from './context.js';

// Bezier edge that lights up while its source variable is on: true, or any non-zero number.
// The firmware reads every var as a float and a bool input sees non-zero as on.
const isOn = (value) => value === true || (typeof value === 'number' && value !== 0);

function LiveEdge({ id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, markerEnd, selected, data }) {
  const value = useLiveValue(data?.var);
  const [path] = getBezierPath({ sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition });

  const classes = ['df-edge', isOn(value) ? 'df-edge-active' : '', selected ? 'df-edge-selected' : ''].join(' ');
  return <BaseEdge id={id} path={path} markerEnd={markerEnd} className={classes} />;
}

export default memo(LiveEdge);
