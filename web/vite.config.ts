import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Forward API calls to the ASP.NET Core API during development.
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
})
