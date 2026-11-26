import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { fakeApi } from './fake-api.ts'

// WCAG 2.1 A and AA, the level the project aims for, plus axe's own best practices such as
// keeping every part of a page inside a landmark.
async function violations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice']).analyze()
  return results.violations.map((violation) => ({
    rule: violation.id,
    impact: violation.impact,
    targets: violation.nodes.map((node) => node.target.join(' ')),
  }))
}

test.describe('meets WCAG 2.1 AA as far as automated checks can tell', () => {
  test('the start page', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

    expect(await violations(page)).toEqual([])
  })

  test('a report', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/analyses/analysis-1')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

    expect(await violations(page)).toEqual([])
  })

  test('a report with evidence from GitHub', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/analyses/analysis-github')
    await page.getByRole('button', { name: /^Kubernetes/ }).last().click()

    expect(await violations(page)).toEqual([])
  })

  test('a report with its requirements open for editing', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/analyses/analysis-1')
    await page.getByRole('button', { name: 'Edit the requirements' }).click()

    expect(await violations(page)).toEqual([])
  })

  test('an unknown report', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/analyses/does-not-exist')
    await expect(page.getByRole('heading', { name: 'This comparison is not available' })).toBeVisible()

    expect(await violations(page)).toEqual([])
  })
})

test('a report can be read with the keyboard alone', async ({ page }) => {
  await fakeApi(page)
  await page.goto('/analyses/analysis-1')
  const react = page.getByRole('button', { name: /^React with TypeScript/ })

  // Tab to the requirement rather than clicking it, then choose it with Enter.
  for (let stop = 0; stop < 20 && !(await react.evaluate((el) => el === document.activeElement)); stop++) {
    await page.keyboard.press('Tab')
  }
  await expect(react).toBeFocused()
  await page.keyboard.press('Enter')

  await expect(react).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByRole('list', { name: 'Resume text' }).locator('li.is-cited mark')).toHaveText(
    'Wrote the React and TypeScript admin console',
  )
})
