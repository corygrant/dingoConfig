// CodeMirror 6 Lua editor for the device Lua tab. Text is owned by .NET (FwDevice.LuaScript):
// edits are pushed back debounced, setValue replaces the text after a read from the device.
import { EditorView, basicSetup } from 'codemirror';
import { keymap } from '@codemirror/view';
import { indentWithTab } from '@codemirror/commands';
import { StreamLanguage } from '@codemirror/language';
import { autocompletion, completeFromList } from '@codemirror/autocomplete';
import { lua } from '@codemirror/legacy-modes/mode/lua';
import { oneDark } from '@codemirror/theme-one-dark';

// Firmware script API, dingoFW core/lua_script.cpp + lua_prelude.h
const api = [
  ['readVar(i)', 'Var map value i'],
  ['setLuaOut(slot, v)', 'Set Lua output slot (0-31)'],
  ['txCan(bus, id, ext, {bytes})', 'Transmit a CAN frame, bus from 1'],
  ['canRxAdd(id)', 'Receive id in onCanRx'],
  ['setTickRate(hz)', 'onTick rate'],
  ['millis()', 'Uptime in ms'],
  ['luaLog(msg)', 'Message read back with Read Message'],
  ['Timer.new()', 'Timer, t:reset() / t:getElapsedSeconds()'],
  ['function onTick()\n\nend', 'Called at the tick rate'],
  ['function onCanRx(bus, id, dlc, data)\n\nend', 'Called for ids added with canRxAdd'],
].map(([apply, detail]) => ({
  label: apply.replace(/^function /, '').split('(')[0],
  apply,
  detail,
  type: 'function',
}));

const luaLang = StreamLanguage.define(lua);

export function create(host, dotnet, text) {
  let timer;
  const view = new EditorView({
    parent: host,
    doc: text ?? '',
    extensions: [
      basicSetup,
      keymap.of([indentWithTab]),
      luaLang,
      luaLang.data.of({ autocomplete: completeFromList(api) }),
      autocompletion(),
      oneDark,
      EditorView.theme({ '&': { height: '100%' }, '.cm-scroller': { fontFamily: 'monospace' } }),
      EditorView.updateListener.of((u) => {
        if (!u.docChanged) return;
        clearTimeout(timer);
        timer = setTimeout(() => dotnet.invokeMethodAsync('SetScript', view.state.doc.toString()), 300);
      }),
    ],
  });

  return {
    getValue: () => view.state.doc.toString(),
    setValue: (t) => view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: t ?? '' } }),
    dispose: () => {
      clearTimeout(timer);
      view.destroy();
    },
  };
}
