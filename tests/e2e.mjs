import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true, executablePath: process.env.HOME + '/Library/Caches/ms-playwright/chromium-1248/chrome-mac-arm64/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing', args:['--use-gl=angle','--use-angle=swiftshader','--enable-webgl','--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
page.on('pageerror', e => console.log('PAGEERROR', e.message));
page.on('console', m => { if(m.type()==='error') console.log('CONSOLE_ERR', m.text()); });
const stage = async (label) => console.log('STAGE', label, '=>', await page.locator('#stage-name').textContent());
try {
  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'networkidle' });
  await page.waitForSelector('.loader', { state: 'detached', timeout: 20000 });
  await page.getByRole('button', { name: /Start shift/ }).click();
  await page.waitForTimeout(1800);
  await page.locator('#call-accept').click({ force: true });
  await page.waitForTimeout(500);
  for (let i=0;i<5;i++) {
    await page.locator('#call-next').click({ force: true });
    await page.waitForTimeout(100);
    if (await page.locator('#call-next').count()) await page.locator('#call-next').click({ force: true });
    await page.waitForTimeout(320);
  }
  await page.waitForTimeout(1500);
  await stage('after call');
  if (await page.locator('#verify-load').count()) await page.locator('#verify-load').click({ force: true });
  else await page.getByRole('button', { name: /Verify details/ }).click();
  await page.waitForTimeout(500);
  for (const answer of ['07:30', '482913', '$2,450']) {
    await page.locator('.quiz-opt').filter({ hasText: answer }).click();
    await page.waitForTimeout(250);
    await page.locator('#quiz-next').click();
    await page.waitForTimeout(700);
  }
  await page.waitForTimeout(1800);
  await stage('after quiz');
  while (await page.locator('.hotspot:not(.done)').count()) {
    await page.locator('.hotspot:not(.done)').first().click({ force: true });
    await page.waitForTimeout(450);
    await page.locator('#inspect-ok').click({ force: true });
    await page.waitForTimeout(900);
  }
  await page.waitForTimeout(500);
  await page.getByRole('button', { name: /Enter cab/ }).click();
  await page.waitForTimeout(2200);
  await stage('gate');
  await page.getByRole('button', { name: /Approach guard/ }).click();
  await page.waitForTimeout(300);
  await page.locator('[data-gate="0"]').click();
  await page.waitForTimeout(900);
  await page.getByRole('button', { name: /Roll through/ }).click();
  await page.waitForTimeout(2200);
  await stage('couple');
  await page.evaluate(()=>{ const w=window.__D2D_WORLD__; w.backing.z=-1.6; w.backing.speed=0; });
  await page.waitForTimeout(1000);
  for (const re of [/Run tug test/, /Raise landing gear/, /Connect air lines/, /Test trailer brakes/, /Continue to seal/]) {
    await page.getByRole('button', { name: re }).click();
    await page.waitForTimeout(550);
  }
  await page.waitForTimeout(2000);
  await stage('seal');
  await page.getByRole('button', { name: /Open proof camera/ }).click();
  await page.waitForTimeout(3500);
  await page.locator('#photo-capture').click({force:true});
  await page.waitForTimeout(900);
  await page.locator('#photo-send').click({force:true});
  await page.waitForTimeout(1200);
  await page.getByRole('button', { name: /Depart facility/ }).click();
  await page.waitForTimeout(2600);
  await stage('road');
  await page.evaluate(()=>{ window.__D2D_WORLD__.road.time = 27.8; });
  await page.waitForTimeout(3500);
  await page.locator('#to-delivery').click();
  await page.waitForTimeout(3000);
  await stage('delivery');
  await page.evaluate(()=>{ const w=window.__D2D_WORLD__; w.backing.z=-15.4; w.backing.speed=0; });
  await page.waitForTimeout(2200);
  await stage('pod');
  await page.getByRole('button', { name: /Cut seal/ }).click();
  await page.waitForTimeout(1000);
  await page.getByRole('button', { name: /Open trailer doors/ }).click();
  await page.waitForTimeout(1400);
  await page.getByRole('button', { name: /Unload 6 pallets/ }).click();
  await page.waitForTimeout(2600);
  await page.getByRole('button', { name: /Photograph POD/ }).click();
  await page.waitForTimeout(3800);
  await page.locator('#photo-capture').click({force:true});
  await page.waitForTimeout(900);
  await page.locator('#photo-send').click({force:true});
  await page.waitForTimeout(2200);
  await stage('debrief');
  console.log('DEBRIEF', (await page.locator('#modal-root').innerText()).slice(0,120).replace(/\n/g,' | '));
  await page.screenshot({ path: '/tmp/e2e-debrief.png' });
  console.log('E2E PASS');
} catch (e) {
  console.log('E2E FAIL', e.message);
  await page.screenshot({ path: '/tmp/e2e-fail.png' });
  process.exitCode = 1;
}
await browser.close();
