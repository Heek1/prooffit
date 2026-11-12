import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const api = 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // The dev server forwards API calls, so the browser only ever talks to one origin.
    proxy: { '/api': api, '/health': api },
  },
})
