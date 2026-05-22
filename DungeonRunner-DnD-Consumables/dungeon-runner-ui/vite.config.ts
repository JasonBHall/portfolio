import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
    proxy: {
      '/gamehub': {
            target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
      },
      // Uploaded item icons are served by the ASP.NET host from Data/icons.
      // Proxying here keeps them same-origin with the app in dev so an
      // <img src="/icons/{id}.png" /> just works.
      '/icons': {
          target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
