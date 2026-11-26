// Renders card.html into the images used when ProofFit is shared:
//   frontend/public/og.png             1200x630, for Open Graph and Twitter cards
//   docs/social-preview/github.png     1280x640, to upload as the repository's social preview
// Run from the repository root after `npm ci` in frontend: node docs/social-preview/render.mjs
import { createRequire } from 'node:module'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.join(here, '../..')
const { chromium } = createRequire(path.join(here, '../../frontend/package.json'))('playwright')

const sizes = [
  { width: 1200, height: 630, out: path.join(here, '../../frontend/public/og.png') },
  { width: 1280, height: 640, out: path.join(here, 'github.png') },
]

const browser = await chromium.launch()
for (const { width, height, out } of sizes) {
  const page = await browser.newPage({ viewport: { width, height } })
  // Served from the repository through an http origin: Chromium will not load the fonts
  // card.html links to from a file:// page.
  await page.route('http://card.local/**', (route) =>
    route.fulfill({ path: path.join(root, decodeURIComponent(new URL(route.request().url()).pathname)) }))
  await page.goto(`http://card.local/docs/social-preview/card.html?w=${width}&h=${height}`)
  // The thread is measured from the layout, so it is drawn again once the fonts are in.
  await page.evaluate(() => document.fonts.ready)
  await page.reload()
  await page.evaluate(() => document.fonts.ready)
  await page.screenshot({ path: out })
  console.log(`${path.relative(process.cwd(), out)} (${width}x${height})`)
}
await browser.close()
