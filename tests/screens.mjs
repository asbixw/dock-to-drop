import { chromium } from 'playwright';
const exe = process.env.HOME + '/Library/Caches/ms-playwright/chromium-1248/chrome-mac-arm64/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing';
for (const [w,h,name] of [[360,800,'small'],[430,932,'large'],[844,390,'landscape']]) {
  const browser = await chromium.launch({ headless:true, executablePath:exe, args:['--use-gl=angle','--use-angle=swiftshader','--enable-webgl','--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport:{width:w,height:h}, deviceScaleFactor:2, isMobile:true, hasTouch:true });
  await page.goto('http://127.0.0.1:4173/', {waitUntil:'networkidle'});
  await page.waitForSelector('.loader',{state:'detached',timeout:20000});
  await page.waitForTimeout(600);
  await page.screenshot({path:`/tmp/screen-${name}-start.png`});
  await page.getByRole('button',{name:/Start shift/}).click();
  await page.waitForTimeout(1600);
  if (await page.locator('#call-accept').count()) await page.locator('#call-accept').click({force:true});
  await page.waitForTimeout(1000);
  await page.screenshot({path:`/tmp/screen-${name}-call.png`});
  await browser.close();
}
