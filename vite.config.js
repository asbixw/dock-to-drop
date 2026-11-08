import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  server: { host: '0.0.0.0', port: 4173, strictPort: true },
  preview: { host: '0.0.0.0', port: 4173, strictPort: true },
  build: {
    target: 'es2020',
    cssMinify: true,
    sourcemap: false,
    assetsInlineLimit: 4096,
    chunkSizeWarningLimit: 1600
  }
});
