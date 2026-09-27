import { test, expect, type Page } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'

/** Fails the test on any page error or failed API response. */
function watchForProblems(page: Page) {
  const problems: string[] = []
  page.on('pageerror', (error) => problems.push(`page error: ${error.message}`))
  page.on('response', (response) => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      problems.push(`HTTP ${response.status()} ${response.url()}`)
    }
  })
  return problems
}

async function enterAs(page: Page, persona: 'sarah' | 'mike' | 'emily') {
  await page.goto('/')
  await page.getByTestId(`persona-${persona}`).click()
  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(page.locator('article').first()).toBeVisible()
}

const cards = (page: Page) => page.locator('main article')

test('a visitor explores two personas, reads the reasons, and books a class', async ({ page }) => {
  const problems = watchForProblems(page)

  await enterAs(page, 'sarah')
  await expect(page.getByRole('heading', { name: 'Recommended for Sarah' })).toBeVisible()
  const sarahTop = await cards(page).locator('h3').allTextContents()
  expect(sarahTop.length).toBeGreaterThanOrEqual(3)
  await expect(cards(page).first()).toContainText('preferred class types')

  // The explanation lists the computed factors and their points.
  await cards(page).first().getByRole('button', { name: /Why #1/ }).click()
  const breakdown = cards(page).first().getByRole('table')
  await expect(breakdown).toContainText('Instructor')
  await expect(breakdown).toContainText('+20')
  await expect(breakdown).toContainText('Total score')

  // Booking changes the card immediately. The follow-up re-rank may reorder the
  // list, so follow the booked class by name rather than by position.
  const bookedName = sarahTop[0]!
  const first = cards(page).filter({ has: page.getByRole('heading', { name: bookedName, exact: true }) })
  const [spotsBefore, capacity] = (await first.getByText(/spots left/).textContent())!.match(/\d+/g)!.map(Number)
  const spots = (n: number) => first.getByText(`${n} of ${capacity} spots left`, { exact: true })
  await first.getByRole('button', { name: 'Book', exact: true }).click()
  await expect(first.getByText('Booked', { exact: true })).toBeVisible()
  await expect(spots(spotsBefore - 1)).toBeVisible()
  await expect(page.getByRole('status').filter({ hasText: /booked/i })).toBeVisible()

  // Switching persona re-ranks the same catalog differently.
  await page.getByTestId('switch-mike').click()
  await expect(page.getByRole('heading', { name: 'Recommended for Mike' })).toBeVisible()
  await expect(page.getByTestId('switch-mike')).toHaveAttribute('aria-pressed', 'true')
  const mikeTop = await cards(page).locator('h3').allTextContents()
  expect(mikeTop.slice(0, 3)).not.toEqual(sarahTop.slice(0, 3))

  // Coming back as Sarah resets her: the earlier booking is undone.
  await enterAs(page, 'sarah')
  await expect(first.getByText('Booked', { exact: true })).toHaveCount(0)
  await expect(spots(spotsBefore)).toBeVisible()

  expect(problems).toEqual([])
})

test('a failed re-rank after booking keeps the booking and warns quietly', async ({ page }) => {
  await enterAs(page, 'emily')
  await page.route('**/api/recommendations/**', (route) => route.abort())

  const first = cards(page).first()
  await first.getByRole('button', { name: 'Book', exact: true }).click()

  await expect(first.getByText('Booked', { exact: true })).toBeVisible()
  await expect(page.getByText('Showing earlier results')).toBeVisible()
  await expect(page.getByRole('alert').filter({ hasText: /.+/ })).toHaveCount(0)

  await page.unroute('**/api/recommendations/**')
  await page.getByRole('button', { name: 'Retry' }).click()
  await expect(page.getByText('Showing earlier results')).toHaveCount(0)
})

test('the demo is usable from the keyboard', async ({ page, isMobile }) => {
  test.skip(isMobile, 'Keyboard journey is checked on desktop')
  await page.goto('/')

  await page.keyboard.press('Tab')
  const skip = page.getByRole('link', { name: 'Skip to main content' })
  await expect(skip).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.locator('main#main')).toBeFocused()

  const explore = page.getByTestId('persona-mike')
  await explore.focus()
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/\/dashboard$/)
  // Client-side navigation moves focus to the new page's main landmark.
  await expect(page.locator('main#main')).toBeFocused()

  // The disclosure renames itself ("Hide score") once open; locate it by what it controls.
  const why = cards(page).first().locator('button[aria-controls]')
  await why.focus()
  await page.keyboard.press('Enter')
  await expect(why).toHaveAttribute('aria-expanded', 'true')
})

test('the mobile menu exposes its state and closes with Escape', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'Mobile navigation only')
  await enterAs(page, 'emily')
  const toggle = page.getByRole('button', { name: 'Open menu' })
  await toggle.click()
  await expect(page.getByRole('button', { name: 'Close menu' })).toHaveAttribute('aria-expanded', 'true')
  await expect(page.locator('#mobile-menu').getByRole('link', { name: 'Classes' })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.locator('#mobile-menu')).toBeHidden()
})

for (const [name, path] of [
  ['home', '/'],
  ['dashboard', '/dashboard'],
  ['classes', '/classes'],
] as const) {
  test(`${name} has no serious or critical accessibility violations`, async ({ page }) => {
    if (path !== '/') await enterAs(page, 'sarah')
    await page.goto(path)
    await expect(page.locator('main')).toBeVisible()
    await page.waitForLoadState('networkidle')
    if (path === '/dashboard') await cards(page).first().getByRole('button', { name: /Why #1/ }).click()

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()
    const blocking = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical')
    expect(blocking.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`)).toEqual([])
  })
}
