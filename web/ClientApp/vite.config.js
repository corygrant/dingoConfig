import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Bundles the flow editor (React + @xyflow/react) into a single ES module that
// Blazor imports via JS interop. Output is committed so dotnet builds don't need Node.
export default defineConfig(({ mode }) => ({
  plugins: [react()],
  define: {
    'process.env.NODE_ENV': JSON.stringify(mode === 'development' ? 'development' : 'production'),
  },
  build: {
    outDir: '../wwwroot/js/flow',
    emptyOutDir: true,
    sourcemap: mode === 'development',
    minify: mode !== 'development',
    lib: {
      entry: 'src/index.jsx',
      formats: ['es'],
      fileName: () => 'flow-editor.js',
      cssFileName: 'flow-editor',
    },
    // Library mode skips whitespace minification for ES output; this is an app bundle, so force it.
    rolldownOptions: {
      output: { minify: mode !== 'development' },
    },
  },
}));
