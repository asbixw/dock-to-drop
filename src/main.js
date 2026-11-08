import './style.css';
import { World } from './world.js';
import { Game } from './game.js';
import { AudioEngine } from './audio.js';

const loader = document.createElement('div');
loader.className = 'loader';
loader.innerHTML = `
  <div class="load-mark">D2D</div>
  <div class="load-bar"><i></i></div>
  <p id="load-status">Warming up the tractor</p>
`;
document.body.appendChild(loader);

async function boot() {
  const status = loader.querySelector('#load-status');
  const bar = loader.querySelector('.load-bar i');
  try {
    bar.style.width = '24%';
    status.textContent = 'Checking the yard';
    await document.fonts.ready;
    bar.style.width = '62%';
    status.textContent = 'Building the load';
    const canvas = document.getElementById('scene');
    const world = new World(canvas);
    const audio = new AudioEngine();
    const game = new Game(world, audio);
    window.__D2D__ = game;
    window.__D2D_WORLD__ = world;
    bar.style.width = '100%';
    status.textContent = 'Ready to roll';
    setTimeout(() => {
      loader.classList.add('out');
      setTimeout(() => loader.remove(), 800);
    }, 500);
  } catch (err) {
    console.error(err);
    status.textContent = 'WebGL could not start on this device';
    bar.style.width = '100%';
    bar.style.background = '#ff5b5b';
    loader.insertAdjacentHTML('beforeend', `<p style="max-width:280px;text-align:center;line-height:1.6;color:#ffb3b3;font-family:Inter,sans-serif;font-size:11px;letter-spacing:0">${String(err.message || err)}</p>`);
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', boot);
} else {
  boot();
}

document.addEventListener('gesturestart', (e) => e.preventDefault(), { passive: false });

if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  });
}
