import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv, type Plugin } from 'vite'

const api = 'http://localhost:5080'

/**
 * Adds a Content-Security-Policy to the built page: scripts, styles and fonts only from the
 * site itself, and requests only to the site and the API. It is left out of the dev server,
 * whose hot reload injects inline scripts the policy would block.
 */
function contentSecurityPolicy(apiUrl: string): Plugin {
  const apiOrigin = apiUrl ? new URL(apiUrl).origin : ''
  const policy = [
    "default-src 'self'",
    "script-src 'self'",
    // React sets a few style attributes (the proof bar's segment widths).
    "style-src 'self' 'unsafe-inline'",
    "font-src 'self'",
    "img-src 'self' data:",
    `connect-src 'self' ${apiOrigin}`.trim(),
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
  ].join('; ')

  return {
    name: 'content-security-policy',
    apply: 'build',
    transformIndexHtml: () => [
      { tag: 'meta', attrs: { 'http-equiv': 'Content-Security-Policy', content: policy }, injectTo: 'head-prepend' },
    ],
  }
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  plugins: [react(), tailwindcss(), contentSecurityPolicy(loadEnv(mode, process.cwd(), '').VITE_API_URL ?? '')],
  server: {
    // The dev server forwards API calls, so the browser only ever talks to one origin.
    proxy: { '/api': api, '/health': api },
  },
}))
