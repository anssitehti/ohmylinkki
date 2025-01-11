import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'

// https://vite.dev/config/
export default defineConfig(() => {
  return {
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: "http://localhost:5074",
        changeOrigin: true
      },
    }
  }
}
})
