import { expect, test } from '@playwright/test'
import { fakeApi } from './fake-api.ts'

test.describe('a comparison from the start page to the report', () => {
  test('the sample leads to a report whose evidence is tied to its requirements', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/')

    await page.getByRole('button', { name: 'Run a sample comparison' }).click()

    await expect(page).toHaveURL(/\/analyses\/analysis-1$/)
    await expect(page.getByRole('heading', { level: 1 })).toHaveAccessibleName('2 of 3 requirements proven')

    // The first requirement with evidence is chosen: its line is marked and a thread drawn.
    const resume = page.getByRole('list', { name: 'Resume text' })
    await expect(resume.locator('li.is-cited')).toHaveText('Built a payments API with ASP.NET Core and PostgreSQL')
    await expect(page.locator('main svg path.thread')).toHaveCount(1)
  })

  test('choosing another requirement moves the evidence and the vacancy line with it', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/analyses/analysis-1')

    await page.getByRole('button', { name: /^React with TypeScript/ }).click()

    await expect(page.getByRole('list', { name: 'Resume text' }).locator('li.is-cited')).toHaveText(
      'Wrote the React and TypeScript admin console',
    )
    await expect(page.getByRole('list', { name: 'Vacancy text' }).locator('li.is-source')).toHaveText('- React with TypeScript')
  })

  test('a requirement with no evidence draws no thread', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/analyses/analysis-1')

    await page.getByRole('button', { name: /^Kubernetes/ }).last().click()

    await expect(page.locator('main svg path.thread')).toHaveCount(0)
    await expect(page.getByRole('list', { name: 'Resume text' }).locator('li.is-cited')).toHaveCount(0)
  })
})

test('edited requirements are sent as edited and the new report says so', async ({ page }) => {
  const api = await fakeApi(page)
  await page.goto('/analyses/analysis-1')

  await page.getByRole('button', { name: 'Edit the requirements' }).click()
  await page.getByRole('button', { name: 'Remove requirement 3' }).click()
  await page.getByRole('button', { name: 'Add a requirement' }).click()
  await page.getByRole('textbox', { name: 'Requirement 3' }).fill('Python')
  await page.getByRole('combobox', { name: 'Importance of requirement 3' }).selectOption('niceToHave')
  await page.getByRole('button', { name: 'Compare again' }).click()

  await expect(page).toHaveURL(/\/analyses\/analysis-2$/)
  await expect(page.getByText('These requirements were corrected by hand, not read from the vacancy.')).toBeVisible()
  expect(api.reruns).toEqual([
    {
      requirements: [
        { text: 'ASP.NET Core', importance: 'mustHave', vacancyLine: 2 },
        { text: 'React with TypeScript', importance: 'mustHave', vacancyLine: 3 },
        { text: 'Python', importance: 'niceToHave', vacancyLine: null },
      ],
    },
  ])
})

test('deleting a report asks first, then removes it and returns to the start', async ({ page }) => {
  const api = await fakeApi(page)
  await page.goto('/analyses/analysis-1')

  await page.getByRole('button', { name: 'Delete now' }).click()
  await expect(page.getByText('Delete this report, the resume and the vacancy? This cannot be undone.')).toBeVisible()
  expect(api.deleted).toEqual([])

  await page.getByRole('button', { name: 'Delete', exact: true }).click()

  await expect(page).toHaveURL(/\/$/)
  expect(api.deleted).toEqual(['analysis-1'])
})

test('a fact from GitHub links to the file that shows it', async ({ page }) => {
  await fakeApi(page)
  await page.goto('/analyses/analysis-github')

  await page.getByRole('button', { name: /^Kubernetes/ }).last().click()

  const file = page.getByRole('link', { name: 'k8s/deployment.yaml' })
  await expect(file).toHaveAttribute('href', 'https://github.com/olena-dev/infra/blob/main/k8s/deployment.yaml')
  await expect(page.getByText(', last pushed March 2026')).toBeVisible()
})

test('an unknown report says so and offers a way back', async ({ page }) => {
  await fakeApi(page)
  await page.goto('/analyses/does-not-exist')

  await expect(page.getByRole('heading', { name: 'This comparison is not available' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Start a new comparison' })).toBeVisible()
})

test.describe('at phone width', () => {
  test.use({ viewport: { width: 375, height: 812 } })

  test('neither page scrolls sideways', async ({ page }) => {
    await fakeApi(page)

    for (const path of ['/', '/analyses/analysis-1']) {
      await page.goto(path)
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
      expect(overflow, `horizontal overflow on ${path}`).toBe(0)
    }
  })
})

test.describe('with reduced motion', () => {
  test.use({ reducedMotion: 'reduce' })

  test('the example on the start page waits to be played', async ({ page }) => {
    await fakeApi(page)
    await page.goto('/')

    await expect(page.getByRole('button', { name: 'Play' })).toBeVisible()
  })
})
