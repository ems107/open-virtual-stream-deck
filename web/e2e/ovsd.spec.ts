import { expect, test, type Page } from '@playwright/test'

const tile = (page: Page, text: string | RegExp) => page.locator('.deck-cell').filter({ hasText: text })

test.describe.configure({ mode: 'serial' })

/** The e2e host runs in dry-run mode: actions are recorded in variables instead of executed. */
async function lastDryRun(request: import('@playwright/test').APIRequestContext): Promise<string> {
  return (await (await request.get('/api/variables')).json())['dryrun.last']
}

test('deck shows the sample profile, taps run actions and folders navigate', async ({ page, request }) => {
  await page.goto('/')
  await expect(page.locator('.deck-cell')).toHaveCount(12)
  await expect(tile(page, /^\d\d:\d\d/)).toBeVisible()

  const counter = page.locator('.deck-cell').nth(8)
  const before = Number(await counter.textContent())
  await counter.click()
  await expect(counter).toHaveText(String(before + 1))

  await tile(page, 'Task Mgr').click()
  await expect.poll(() => lastDryRun(request)).toBe('chord Ctrl+Shift+Esc')

  await tile(page, 'Apps').click()
  await tile(page, 'Calc').click()
  await expect.poll(() => lastDryRun(request)).toBe('launch calc.exe')
  await page.locator('[data-tile="__back"]').click()
  await expect(tile(page, 'Apps')).toBeVisible()
})

test('slider drags send values', async ({ page, request }) => {
  await page.goto('/')
  const slider = page.locator('.deck-cell').filter({ has: page.locator('.tile-slider') })
  const box = (await slider.boundingBox())!
  await page.mouse.move(box.x + box.width / 2, box.y + box.height - 2)
  await page.mouse.down()
  await page.mouse.move(box.x + box.width / 2, box.y + box.height * 0.25, { steps: 8 })
  await page.mouse.up()
  await expect.poll(() => lastDryRun(request)).toMatch(/^volume Master (7[0-9]|8[0-9])$/)
  await expect(slider).toContainText(/(7[0-9]|8[0-9])%/)
})

test('toggle buttons switch appearance', async ({ page }) => {
  await page.goto('/')
  const toggle = tile(page, /^(Toggle|ON)$/)
  const initial = await toggle.textContent()
  await toggle.click()
  await expect(toggle).not.toHaveText(initial!)
  await toggle.click()
  await expect(toggle).toHaveText(initial!)
})

test('editor changes reach an open deck immediately', async ({ browser }) => {
  const deck = await browser.newPage()
  await deck.goto('/')
  await expect(deck.locator('.deck-cell')).toHaveCount(12)

  const editor = await browser.newPage()
  await editor.goto('/editor')
  await editor.locator('.editor-tile').filter({ hasText: 'Task Mgr' }).click()
  await editor.locator('.inspector textarea').first().fill('Hola E2E {{1+1}}')
  await expect(editor.locator('.save-state')).toHaveText(/Saved|Guardado/, { timeout: 5000 })

  await expect(tile(deck, 'Hola E2E 2')).toBeVisible({ timeout: 5000 })

  // Undo restores the previous text (and the deck follows); redo brings it back
  await editor.locator('.inspector-panel').click({ position: { x: 5, y: 5 } })
  await editor.keyboard.press('Control+z')
  await expect(tile(deck, 'Task Mgr')).toBeVisible({ timeout: 5000 })
  await editor.keyboard.press('Control+y')
  await expect(tile(deck, 'Hola E2E 2')).toBeVisible({ timeout: 5000 })

  // New tile in the Apps folder (the home page is full)
  await editor.locator('.pages-panel li').filter({ hasText: 'Apps' }).click()
  await editor.locator('.editor-cell:not(.back)').first().click()
  await editor.locator('.empty-cell-actions button').first().click()
  await editor.locator('.inspector textarea').first().fill('Nueva')
  await expect(editor.locator('.save-state')).toHaveText(/Saved|Guardado/, { timeout: 5000 })
  await tile(deck, 'Apps').click()
  await expect(tile(deck, 'Nueva')).toBeVisible({ timeout: 5000 })
  await deck.locator('[data-tile="__back"]').click()
  await expect(tile(deck, 'Apps')).toBeVisible()
})

test('remote devices must pair with the PIN shown on the PC', async ({ browser, request }) => {
  const info = await (await request.get('/api/server')).json()
  const remote = await browser.newPage()
  await remote.goto(info.url)
  await expect(remote.locator('.pin-input')).toBeVisible()

  const pair = await (await request.post('/api/pair/new')).json()
  await remote.locator('.pin-input').fill(pair.code)
  await remote.locator('.pair-card input').nth(1).fill('E2E tablet')
  await remote.getByRole('button', { name: /Connect|Conectar/ }).click()
  await expect(remote.locator('.deck-cell').first()).toBeVisible()

  const devices = await (await request.get('/api/devices')).json()
  expect(devices.map((d: { name: string }) => d.name)).toContain('E2E tablet')

  // Revoking the device sends it back to the pairing screen
  const device = devices.find((d: { name: string }) => d.name === 'E2E tablet')
  await request.delete(`/api/devices/${device.id}`)
  await expect(remote.locator('.pin-input')).toBeVisible({ timeout: 10000 })
})

test('quick edit on the device changes a tile', async ({ page }) => {
  await page.goto('/')
  await page.locator('.deck-menu-button').click()
  await page.locator('.menu-item').filter({ hasText: /Edit|Editar/ }).first().click()
  await tile(page, 'GitHub').click()
  await expect(page.locator('.quick-edit')).toBeVisible()
  await page.locator('.quick-edit textarea').first().fill('Web')
  await page.locator('.quick-edit button.primary').click()
  await expect(page.locator('.quick-edit')).toHaveCount(0)
  await expect(tile(page, /^Web$/)).toBeVisible({ timeout: 5000 })
})

test.describe('touch screens', () => {
  test.use({ hasTouch: true, isMobile: true, viewport: { width: 450, height: 900 } })

  // Regression: the sheet opened on pointerup and the click synthesized after the touch closed it again.
  test('tapping a tile in edit mode keeps the quick edit sheet open', async ({ page }) => {
    await page.goto('/')
    await page.locator('.deck-menu-button').tap()
    await page.locator('.menu-item').filter({ hasText: /Edit|Editar/ }).first().tap()
    await tile(page, 'Apps').tap()
    await page.waitForTimeout(500)
    await expect(page.locator('.quick-edit')).toBeVisible()
  })
})
