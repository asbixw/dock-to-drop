import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true, executablePath: process.env.HOME + '/Library/Caches/ms-playwright/chromium-1248/chrome-mac-arm64/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing', args:['--use-gl=angle','--use-angle=swiftshader','--enable-webgl','--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
await page.goto('http://127.0.0.1:4173/', { waitUntil: 'networkidle' });
await page.waitForSelector('.loader', { state: 'detached', timeout: 20000 });
await page.waitForTimeout(800);
await page.screenshot({ path: '/tmp/d2d-start4.png' });
console.log('start boxes', await page.evaluate(()=>{const b=(s)=>{const e=document.querySelector(s);const r=e.getBoundingClientRect();return [r.x,r.y,r.width,r.height]};return {screen:b('.start-screen'),title:b('.start-title'),actions:b('.start-actions'),footer:b('.start-footer')}}));
await browser.close();
