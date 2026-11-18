import { defineConfig } from 'vite';

// Lua editor (CodeMirror 6) as one ES module for Blazor JS interop, output committed like the flow editor
export default defineConfig(({ mode }) => ({
  build: {
    outDir: '../wwwroot/js/lua',
    emptyOutDir: true,
    sourcemap: mode === 'development',
    minify: mode !== 'development',
    lib: { entry: 'src/lua-editor.js', formats: ['es'], fileName: () => 'lua-editor.js' },
    rolldownOptions: { output: { minify: mode !== 'development' } },
  },
}));
