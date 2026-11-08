import { LOAD, STAGES, CALL_LINES, QUIZ, INSPECTIONS, COUPLE_STEPS, TIPS } from './data.js';
import { Vector3 } from 'three';

const SAVE_KEY = 'd2d_save_v1';
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const avg = (arr) => arr.reduce((a, b) => a + b, 0) / arr.length;
const wait = (ms) => new Promise((res) => setTimeout(res, ms));
const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

const ICONS = {
  chat: '<svg viewBox="0 0 24 24"><path d="M4 4h16a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H9l-5 4v-4H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z"/></svg>',
  load: '<svg viewBox="0 0 24 24"><path d="M3 6h11v10H3V6Zm12 3h3l3 3v4h-6V9ZM7 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4Zm11 0a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z"/></svg>',
  photos: '<svg viewBox="0 0 24 24"><path d="M4 5h4l1.5-2h5L16 5h4a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Zm8 3.5A4.5 4.5 0 1 0 12 17.5a4.5 4.5 0 0 0 0-9Zm0 2a2.5 2.5 0 1 1 0 5 2.5 2.5 0 0 1 0-5Z"/></svg>',
  check: '<svg viewBox="0 0 24 24"><path d="m9 16.2-3.5-3.5L4 14.2l5 5L20.2 8 18.7 6.5 9 16.2Z"/></svg>',
  arrow: '<svg viewBox="0 0 24 24"><path d="m13 5 7 7-7 7-1.4-1.4L16.2 13H4v-2h12.2l-4.6-4.6L13 5Z"/></svg>',
  phone: '<svg viewBox="0 0 24 24"><path d="M6.6 10.8a15.1 15.1 0 0 0 6.6 6.6l2.2-2.2c.3-.3.7-.4 1.1-.3 1.2.4 2.5.6 3.8.6.6 0 1 .4 1 1V20c0 .6-.4 1-1 1C10.6 21 3 13.4 3 4c0-.6.4-1 1-1h3.5c.6 0 1 .4 1 1 0 1.3.2 2.6.6 3.8.1.4 0 .8-.3 1.1l-2.2 1.9Z"/></svg>',
  close: '<svg viewBox="0 0 24 24"><path d="M6.4 5 5 6.4 10.6 12 5 17.6 6.4 19l5.6-5.6 5.6 5.6 1.4-1.4-5.6-5.6L19 6.4 17.6 5 12 10.6 6.4 5Z"/></svg>'
};

export class Game {
  constructor(world, audio) {
    this.world = world;
    this.audio = audio;
    this.screenRoot = $('#screen-root');
    this.modalRoot = $('#modal-root');
    this.hud = $('#hud');
    this.actionDock = $('#action-dock');
    this.phoneFab = $('#phone-fab');
    this.photoUI = $('#photo-ui');
    this.roadHud = $('#road-hud');
    this.roadControls = $('#road-controls');
    this.dockControls = $('#dock-controls');
    this.fadeEl = $('#fade');
    this.save = this.loadSave();
    this.settings = Object.assign({ sound: true, voice: true }, this.save.settings || {});
    this.best = this.save.best || 0;
    this.completed = !!this.save.completed;
    this.stageIndex = 0;
    this.stage = STAGES[0];
    this.scores = { safety: 100, compliance: 100, communication: 100, time: 100 };
    this.messages = [];
    this.photos = [];
    this.pretripDone = new Set();
    this.dockInput = { left: false, right: false, reverse: false, forward: false };
    this.dockInterval = null;
    this.roadInterval = null;
    this.callTimer = null;
    this.stageTimers = [];
    this.transitioning = false;
    this.practice = null;
    this.modal = null;
    this.photoMode = null;
    this.photoData = null;
    this.phoneOpen = false;
    this.phoneTab = 'chat';
    this.unread = 0;
    this.roadFinished = false;
    this.callOpen = false;
    this.installPrompt = null;
    this.typed = null;
    this.audio.setMuted(!this.settings.sound);
    this.audio.setVoice(this.settings.voice);
    this.bindStatic();
    this.showStart();
  }

  loadSave() {
    try {
      return JSON.parse(localStorage.getItem(SAVE_KEY) || '{}') || {};
    } catch (e) {
      return {};
    }
  }

  persist() {
    if (this.practice) return;
    try {
      localStorage.setItem(SAVE_KEY, JSON.stringify({
        stage: this.stageIndex,
        scores: this.scores,
        best: this.best,
        completed: this.completed,
        settings: this.settings
      }));
    } catch (e) {}
  }

  bindStatic() {
    document.addEventListener('click', (e) => {
      const action = e.target.closest('[data-action]');
      if (!action) return;
      const name = action.dataset.action;
      if (name === 'new') this.startNewShift();
      else if (name === 'continue') this.continueShift();
      else if (name === 'how') this.showHowTo();
      else if (name === 'settings') this.showSettings();
      else if (name === 'drills') this.showDrills();
      else if (name === 'install' && this.installPrompt) this.promptInstall();
      else if (name === 'menu') this.showStart();
    });

    $('#hud-menu').addEventListener('click', () => this.showPause());
    $('#hud-sound').addEventListener('click', () => this.toggleSound());
    this.phoneFab.addEventListener('click', () => this.openPhone('chat'));
    $('#photo-close').addEventListener('click', () => this.closePhoto());
    $('#photo-capture').addEventListener('click', () => this.capturePhoto());
    $('#photo-zoom').addEventListener('input', (e) => this.world.setPhotoZoom(Number(e.target.value) / 100));

    const hold = (el, key) => {
      if (!el) return;
      const on = (e) => {
        e.preventDefault();
        this.dockInput[key] = true;
        this.world.setBackingInput(this.dockInput);
        el.setPointerCapture?.(e.pointerId);
      };
      const off = () => {
        this.dockInput[key] = false;
        this.world.setBackingInput(this.dockInput);
      };
      el.addEventListener('pointerdown', on);
      el.addEventListener('pointerup', off);
      el.addEventListener('pointercancel', off);
      el.addEventListener('pointerleave', off);
    };
    hold($('#dock-left'), 'left');
    hold($('#dock-right'), 'right');
    hold($('#dock-reverse'), 'reverse');
    hold($('#dock-forward'), 'forward');

    $('#road-left').addEventListener('click', () => this.changeLane(-1));
    $('#road-right').addEventListener('click', () => this.changeLane(1));

    window.addEventListener('beforeinstallprompt', (e) => {
      e.preventDefault();
      this.installPrompt = e;
      const startBtn = document.querySelector('[data-action="install"]');
      if (startBtn) startBtn.classList.remove('hidden');
    });

    window.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        if (this.callOpen) return;
        if (this.photoUI && !this.photoUI.classList.contains('hidden')) this.closePhoto();
        else if (this.modal) this.closeModal();
      }
      if (this.stage.key === 'road' && (e.key === 'ArrowLeft' || e.key === 'a')) this.changeLane(-1);
      if (this.stage.key === 'road' && (e.key === 'ArrowRight' || e.key === 'd')) this.changeLane(1);
    });

    window.addEventListener('resize', () => this.world.resize());
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) this.audio.stopSpeak();
    });

    this.world.onCouplingLocked = () => this.handleCoupled();
    this.world.onBackingWarning = (msg) => this.handleBackingWarning(msg);
    this.world.onDocked = () => this.handleDocked();
    this.world.onRoadCollision = (n) => this.handleRoadCollision(n);
    this.world.onRoadComplete = () => this.finishRoad();
  }

  showStart() {
    this.clearStageTimers();
    this.stopDockControls(true);
    this.hideRoadUI();
    this.closeModal();
    this.hideAction();
    this.photoUI.classList.add('hidden');
    this.hud.classList.add('hidden');
    this.phoneFab.classList.add('hidden');
    this.roadHud.classList.add('hidden');
    this.roadControls.classList.add('hidden');
    this.world.clearHotspots();
    this.world.setMode('menu');
    this.practice = null;
    const savedStage = this.save.stage || 0;
    const canContinue = savedStage > 0 && savedStage < STAGES.length - 1;
    const continueName = canContinue ? STAGES[savedStage].name : '';
    const bestLine = this.best > 0 ? `BEST SHIFT · ${Math.round(this.best)} / 100` : 'FIRST SHIFT · DALLAS, TX';
    this.screenRoot.innerHTML = `
      <div class="screen start-screen">
        <div class="start-top">
          <div class="start-mark">D<i></i>D</div>
          <span class="version">v1.0 · FREIGHT DAY SIM</span>
        </div>
        <div class="start-main">
          <div class="start-kicker">${bestLine}</div>
          <h1 class="start-title">DOCK<span>TO DROP</span></h1>
          <p class="start-sub">One call. One load. One clean delivery.</p>
          <p class="start-tagline">A mobile procedure simulator for freight drivers. Take the dispatch call, inspect the tractor, check in at the gate, hook the container, prove the seal, and deliver on time.</p>
          <div class="start-actions">
            ${canContinue ? `<button class="btn" data-action="continue">Continue shift <span class="arrow">→</span></button>` : ''}
            <button class="btn ${canContinue ? 'ghost' : ''}" data-action="new">${canContinue ? 'Start a new shift' : 'Start shift'} <span class="arrow">→</span></button>
            <div class="row">
              <button class="btn ghost" data-action="drills">Training drills</button>
              <button class="btn ghost" data-action="how">How to play</button>
            </div>
            <button class="btn ghost hidden" data-action="install">Install app</button>
          </div>
          ${canContinue ? `<p class="start-tagline" style="margin-top:12px">Next up: <b style="color:#c9d5e1">${continueName}</b></p>` : ''}
        </div>
        <div class="start-footer">
          <span class="legal">Independent training simulation. Not affiliated with Amazon, Relay, or any truck manufacturer. For familiarization only — not a substitute for hands-on instruction or regulatory certification.</span>
          <button data-action="settings">Settings</button>
        </div>
      </div>
    `;
  }

  startNewShift() {
    this.audio.init();
    this.audio.click();
    this.practice = null;
    this.completed = false;
    this.scores = { safety: 100, compliance: 100, communication: 100, time: 100 };
    this.messages = [];
    this.photos = [];
    this.unread = 0;
    this.roadFinished = false;
    this.callOpen = false;
    this.screenRoot.innerHTML = '';
    this.hud.classList.remove('hidden');
    this.phoneFab.classList.remove('hidden');
    this.updatePhoneBadge();
    this.enterStage(0);
  }

  continueShift() {
    this.audio.init();
    this.audio.click();
    this.practice = null;
    const st = clamp(this.save.stage || 0, 0, STAGES.length - 1);
    this.scores = Object.assign({ safety: 100, compliance: 100, communication: 100, time: 100 }, this.save.scores || {});
    this.screenRoot.innerHTML = '';
    this.hud.classList.remove('hidden');
    this.phoneFab.classList.remove('hidden');
    this.enterStage(st);
  }

  showDrills() {
    this.audio.click();
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">FREE PRACTICE</span><h2>Training drills</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        <p>Warm up one procedure at a time. Drills are not scored into your shift record.</p>
        <div class="divider"></div>
        <button class="dialogue-choice" data-drill="couple"><b>Coupling drill</b><br><span class="muted">Back under a 53' container and complete the hook-up sequence.</span></button>
        <button class="dialogue-choice" data-drill="seal"><b>Seal photo drill</b><br><span class="muted">Frame the seal, capture a legible number, and send proof.</span></button>
        <button class="dialogue-choice" data-drill="dock"><b>Dock backing drill</b><br><span class="muted">Reverse a loaded trailer into Door 14 without hitting the dock.</span></button>
      </div>
      <div class="modal-foot"><button class="btn ghost" data-close>Back</button></div>
    `);
    $$('[data-drill]', this.modal).forEach((b) => b.addEventListener('click', () => this.startDrill(b.dataset.drill)));
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', () => this.closeModal()));
  }

  startDrill(kind) {
    this.audio.init();
    this.audio.click();
    this.closeModal();
    this.practice = kind;
    this.scores = { safety: 100, compliance: 100, communication: 100, time: 100 };
    this.screenRoot.innerHTML = '';
    this.hud.classList.remove('hidden');
    this.phoneFab.classList.add('hidden');
    const index = kind === 'couple' ? 4 : kind === 'seal' ? 5 : 7;
    this.enterStage(index, { drill: true });
  }

  finishDrill() {
    const kind = this.practice;
    this.practice = null;
    this.stopDockControls(true);
    this.hideRoadUI();
    this.closeModal();
    this.hideAction();
    this.hud.classList.add('hidden');
    this.phoneFab.classList.add('hidden');
    this.world.setMode('menu');
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">DRILL COMPLETE</span><h2>Nice work</h2></div></div>
      <div class="modal-body">
        <p>You completed the ${kind === 'couple' ? 'coupling' : kind === 'seal' ? 'seal photo' : 'dock backing'} drill. Run it again or go back to the shift.</p>
        <div class="stat-grid">
          <div class="stat-card good"><span>Safety</span><strong>${this.scores.safety}</strong></div>
          <div class="stat-card"><span>Repetitions</span><strong>1</strong></div>
        </div>
      </div>
      <div class="modal-foot"><button class="btn ghost" data-close>Main menu</button><button class="btn" id="drill-again">Run again</button></div>
    `);
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', () => { this.closeModal(); this.showStart(); }));
    $('#drill-again', this.modal).addEventListener('click', () => this.startDrill(kind));
  }

  updateHUD() {
    this.stage = STAGES[this.stageIndex];
    $('#stage-kicker').textContent = this.stage.kicker;
    $('#stage-name').textContent = this.stage.name;
    $('#clock').textContent = this.stage.clock;
    $('#route-origin').textContent = 'DALLAS, TX';
    $('#route-dest').textContent = 'HOUSTON, TX';
    $('#route-miles').textContent = `${LOAD.miles} MI`;
    const progress = $('#progress');
    progress.innerHTML = STAGES.slice(0, -1).map((s, i) => {
      const cls = i < this.stageIndex ? 'done' : i === this.stageIndex ? 'now' : '';
      return `<i class="${cls}"></i>`;
    }).join('');
  }

  setAction({ kicker = '', title = '', text = '', buttons = [] }) {
    this.actionDock.classList.remove('hidden');
    this.actionDock.innerHTML = `
      <div class="action-inner">
        <div class="action-copy">
          ${kicker ? `<span class="kicker">${kicker}</span>` : ''}
          <h3>${title}</h3>
          <p>${text}</p>
        </div>
        <div class="action-buttons">
          ${buttons.map((b, i) => `<button class="btn ${b.variant || ''}" data-btn="${i}" ${b.disabled ? 'disabled' : ''}>${b.label}</button>`).join('')}
        </div>
      </div>
    `;
    $$('[data-btn]', this.actionDock).forEach((el) => {
      el.addEventListener('click', () => {
        const b = buttons[Number(el.dataset.btn)];
        if (!b || b.disabled) return;
        this.audio.click();
        b.onClick?.();
      });
    });
  }

  hideAction() {
    this.actionDock.classList.add('hidden');
    this.actionDock.innerHTML = '';
  }

  toast(message, type = '', duration = 2800) {
    const stack = $('#toast-stack');
    const el = document.createElement('div');
    el.className = `toast ${type}`;
    el.textContent = message;
    stack.appendChild(el);
    setTimeout(() => {
      el.classList.add('out');
      setTimeout(() => el.remove(), 260);
    }, duration);
  }

  showModal(html, opts = {}) {
    this.closeModal();
    const backdrop = document.createElement('div');
    backdrop.className = 'modal-backdrop';
    backdrop.innerHTML = `<div class="modal" role="dialog" aria-modal="true">${html}</div>`;
    this.modalRoot.appendChild(backdrop);
    this.modal = backdrop;
    if (opts.dismissible !== false) {
      backdrop.addEventListener('pointerdown', (e) => {
        if (e.target === backdrop) this.closeModal();
      });
    }
    return backdrop;
  }

  showOverlay(html) {
    this.closeModal();
    this.modalRoot.innerHTML = html;
    this.modal = this.modalRoot.firstElementChild;
    return this.modal;
  }

  closeModal() {
    this.modalRoot.innerHTML = '';
    this.modal = null;
  }

  fade(on, ms = 380) {
    return new Promise((resolve) => {
      this.fadeEl.classList.toggle('on', on);
      setTimeout(resolve, ms);
    });
  }

  setTimeout(fn, ms) {
    const id = setTimeout(fn, ms);
    this.stageTimers.push(id);
    return id;
  }

  clearStageTimers() {
    this.stageTimers.forEach((id) => clearTimeout(id));
    this.stageTimers = [];
    if (this.callTimer) clearInterval(this.callTimer);
    this.callTimer = null;
    if (this.roadInterval) clearInterval(this.roadInterval);
    this.roadInterval = null;
    this.audio.stopRing();
    this.audio.stopSpeak();
  }

  toggleSound() {
    this.settings.sound = !this.settings.sound;
    this.audio.setMuted(!this.settings.sound);
    this.audio.click();
    $('#hud-sound').style.opacity = this.settings.sound ? '1' : '.38';
    this.persist();
    this.toast(this.settings.sound ? 'Sound on' : 'Sound off');
  }

  addMessage(from, text, opts = {}) {
    this.messages.push({ from, text, ...opts, time: opts.time || this.stage.clock });
    if (this.phoneOpen) this.renderPhone();
    else {
      this.unread++;
      this.updatePhoneBadge();
    }
  }

  updatePhoneBadge() {
    const badge = $('#phone-badge');
    if (!badge) return;
    badge.textContent = String(this.unread);
    badge.classList.toggle('hidden', this.unread === 0);
  }

  async goStage(index) {
    if (this.transitioning) return;
    this.transitioning = true;
    await this.fade(true, 360);
    this.enterStage(index);
    await wait(80);
    await this.fade(false, 420);
    this.transitioning = false;
  }

  enterStage(index, opts = {}) {
    this.clearStageTimers();
    this.closeModal();
    this.closePhoto(true);
    this.stopDockControls(true);
    this.hideRoadUI();
    this.world.clearHotspots();
    this.world.setBackingInput({});
    this.stageIndex = clamp(index, 0, STAGES.length - 1);
    this.stage = STAGES[this.stageIndex];
    this.updateHUD();
    this.hud.classList.remove('hidden');
    this.phoneFab.classList.remove('hidden');
    this.phoneFab.classList.toggle('hidden', this.stage.key === 'road' || this.stage.key === 'debrief');
    this.hideAction();
    this.unread = 0;
    this.updatePhoneBadge();
    this.ensureMessages();
    this.persist();

    switch (this.stage.key) {
      case 'call': this.setupCall(); break;
      case 'load': this.setupLoad(); break;
      case 'pretrip': this.setupPretrip(); break;
      case 'gate': this.setupGate(); break;
      case 'couple': this.setupCouple(); break;
      case 'seal': this.setupSeal(); break;
      case 'road': this.setupRoad(); break;
      case 'delivery': this.setupDelivery(); break;
      case 'pod': this.setupPod(); break;
      case 'debrief': this.showDebrief(); break;
      default: this.setupCall();
    }
  }

  ensureMessages() {
    if (this.messages.length) return;
    this.messages = [
      { from: 'them', text: "Morning, driver. Load's booked. Details below.", time: '06:14' },
      { from: 'them', text: 'Screenshot attached. Verify the appointment, seal, and rate.', shot: true, time: '06:15' },
      { from: 'me', text: 'Copy. Reviewing now.', time: '06:17' }
    ];
    this.unread = 1;
    this.updatePhoneBadge();
  }

  /* ---------------- CALL ---------------- */

  setupCall() {
    this.world.setMode('call');
    this.phoneFab.classList.add('hidden');
    this.setAction({
      kicker: '06:12 · YARD',
      title: 'Dispatch is calling',
      text: 'Mike has a load. Answer the call and write down the pickup, delivery, appointment, seal, and rate.',
      buttons: [{ label: 'Answer dispatch', onClick: () => this.openIncomingCall() }]
    });
    this.setTimeout(() => this.openIncomingCall(), 1100);
  }

  openIncomingCall() {
    if (this.callOpen || !this.stage || this.stage.key !== 'call') return;
    this.callOpen = true;
    this.audio.ring();
    this.showOverlay(`
      <div class="call-screen">
        <div class="call-avatar">M</div>
        <div class="call-name">
          <span class="kicker">INCOMING CALL · TELELINK</span>
          <h2>Mike · Dispatch</h2>
          <p>Freight desk · Dallas</p>
          <div class="call-timer" id="call-timer">00:00</div>
        </div>
        <div class="call-actions">
          <button class="call-decline" id="call-decline" aria-label="Decline call">${ICONS.phone.replace('<svg', '<svg style="transform:rotate(135deg)"')}</button>
          <button class="call-accept" id="call-accept" aria-label="Accept call">${ICONS.phone}</button>
        </div>
      </div>
    `);
    let sec = 0;
    this.callTimer = setInterval(() => {
      sec++;
      const t = $('#call-timer');
      if (t) t.textContent = `${String(Math.floor(sec / 60)).padStart(2, '0')}:${String(sec % 60).padStart(2, '0')}`;
    }, 1000);
    $('#call-accept').addEventListener('click', () => {
      this.audio.pickup();
      this.callIndex = 0;
      this.callSeconds = 0;
      this.renderCallInterface();
    });
    $('#call-decline').addEventListener('click', () => {
      this.audio.hangup();
      this.callOpen = false;
      if (this.callTimer) clearInterval(this.callTimer);
      this.callTimer = null;
      this.closeModal();
      this.toast('Dispatch will call back. He is not going away.', 'bad');
      this.setTimeout(() => this.openIncomingCall(), 2300);
    });
  }

  renderCallInterface() {
    const line = CALL_LINES[this.callIndex];
    const total = CALL_LINES.length;
    this.showOverlay(`
      <div class="call-screen">
        <div class="call-avatar" style="width:74px;height:74px;font-size:28px">M</div>
        <div class="call-name" style="margin-top:16px">
          <span class="kicker">CONNECTED · TELELINK VOICE</span>
          <h2>Mike · Dispatch</h2>
          <div class="call-timer" id="call-timer">00:00</div>
        </div>
        <div class="call-script">
          <div class="speaker">MIKE · DISPATCH</div>
          <p id="call-text"></p>
          <div class="call-progress">${CALL_LINES.map((_, i) => `<i class="${i <= this.callIndex ? 'on' : ''}"></i>`).join('')}</div>
          <div class="call-next"><button id="call-next">${this.callIndex === total - 1 ? 'Open load details' : 'Next'} →</button></div>
        </div>
        <div class="call-wave">${Array.from({ length: 18 }).map(() => '<i></i>').join('')}</div>
      </div>
    `);
    const p = $('#call-text');
    this.typeText(p, line, 14);
    this.audio.speak(line);
    if (this.callTimer) clearInterval(this.callTimer);
    this.callTimer = setInterval(() => {
      this.callSeconds++;
      const t = $('#call-timer');
      if (t) t.textContent = `${String(Math.floor(this.callSeconds / 60)).padStart(2, '0')}:${String(this.callSeconds % 60).padStart(2, '0')}`;
    }, 1000);
    $('#call-next').addEventListener('click', () => {
      if (this.typed && !this.typed.done) {
        this.typed.finish();
        return;
      }
      if (this.callIndex < CALL_LINES.length - 1) {
        this.callIndex++;
        this.audio.tap();
        this.renderCallInterface();
      } else {
        this.finishCall();
      }
    });
  }

  typeText(el, text, speed = 18) {
    if (!el) return;
    clearInterval(this.typed?.timer);
    el.textContent = '';
    let i = 0;
    const timer = setInterval(() => {
      i++;
      el.textContent = text.slice(0, i);
      if (i >= text.length) {
        clearInterval(timer);
        if (this.typed) this.typed.done = true;
      }
    }, speed);
    this.typed = {
      done: false,
      timer,
      finish: () => {
        clearInterval(timer);
        el.textContent = text;
        this.typed.done = true;
      }
    };
  }

  finishCall() {
    this.callOpen = false;
    if (this.callTimer) clearInterval(this.callTimer);
    this.callTimer = null;
    this.audio.hangup();
    this.closeModal();
    this.messages = [
      { from: 'them', text: "Morning, driver. Load's booked. Details below.", time: '06:14' },
      { from: 'them', text: 'Screenshot attached. Verify the appointment, seal, and rate.', shot: true, time: '06:15' },
      { from: 'me', text: 'Copy. Reviewing now.', time: '06:17' }
    ];
    this.unread = 1;
    this.updatePhoneBadge();
    this.goStage(1);
  }

  /* ---------------- LOAD / PHONE ---------------- */

  setupLoad() {
    this.world.setMode('call');
    this.phoneFab.classList.remove('hidden');
    this.setAction({
      kicker: '06:18 · PAPERWORK',
      title: 'Verify the load',
      text: 'Open the load app. Check the appointment, seal number, weight, and rate against what Mike told you.',
      buttons: [
        { label: 'Open phone', onClick: () => this.openPhone('load') },
        { label: 'Verify details', onClick: () => this.startQuiz() }
      ]
    });
    this.setTimeout(() => this.openPhone('load'), 800);
  }

  openPhone(tab = 'chat') {
    if (!this.stage || this.stage.key === 'road' || this.stage.key === 'debrief') return;
    this.audio.click();
    this.phoneOpen = true;
    this.phoneTab = tab;
    this.unread = 0;
    this.updatePhoneBadge();
    this.renderPhone();
  }

  renderPhone() {
    const unread = this.unread;
    this.showOverlay(`
      <div class="modal-backdrop" style="padding:12px">
        <div class="phone-shell">
          <div class="phone-notch"></div>
          <div class="phone-status"><span>06:18</span><span class="signal">▮▮▮</span><span>5G · 87%</span></div>
          <div class="phone-header">
            <div class="app-avatar">TL</div>
            <div><h3>TeleLink</h3><p>Dispatch · Mike · online</p></div>
            <button class="icon-btn phone-close" id="phone-close" aria-label="Close phone">${ICONS.close}</button>
          </div>
          <div class="phone-body" id="phone-body"></div>
          <div class="phone-tabs">
            <button class="phone-tab ${this.phoneTab === 'chat' ? 'active' : ''}" data-tab="chat">${ICONS.chat}<span>Chat</span></button>
            <button class="phone-tab ${this.phoneTab === 'load' ? 'active' : ''}" data-tab="load">${ICONS.load}<span>Load</span></button>
            <button class="phone-tab ${this.phoneTab === 'photos' ? 'active' : ''}" data-tab="photos">${ICONS.photos}<span>Photos</span></button>
          </div>
        </div>
      </div>
    `);
    $('#phone-close').addEventListener('click', () => {
      this.phoneOpen = false;
      this.closeModal();
    });
    $$('[data-tab]', this.modal).forEach((b) => b.addEventListener('click', () => {
      this.audio.tap();
      this.phoneTab = b.dataset.tab;
      this.renderPhone();
    }));
    const body = $('#phone-body');
    if (this.phoneTab === 'chat') {
      body.innerHTML = `<div class="chat-day">TODAY · DISPATCH</div>` + this.messages.map((m) => this.renderMessage(m)).join('');
      $$('[data-open-load]', body).forEach((b) => b.addEventListener('click', () => {
        this.phoneTab = 'load';
        this.renderPhone();
      }));
      body.scrollTop = body.scrollHeight;
    } else if (this.phoneTab === 'load') {
      body.innerHTML = this.renderLoadTab();
      const verify = $('#verify-load', body);
      if (verify) verify.addEventListener('click', () => {
        this.phoneOpen = false;
        this.closeModal();
        this.startQuiz();
      });
    } else {
      body.innerHTML = this.photos.length ? `<div class="photo-grid">${this.photos.map((p) => `<div class="photo-tile"><img src="${p.src}" alt="${p.label}"><span>${p.label}</span></div>`).join('')}</div>` : `<div class="phone-empty">${ICONS.photos}<br>No proof photos yet.<br>Seal and POD photos will appear here.</div>`;
    }
  }

  renderMessage(m) {
    if (m.shot) {
      return `
        <div class="chat-row">
          <div class="chat-avatar">M</div>
          <div class="chat-bubble">
            ${m.text}
            <div class="chat-shot">
              <div class="shot-top"><span>LOADLINK · BOOKED</span><span>#D2D-0612</span></div>
              <div class="shot-body">
                <div class="shot-route">DALLAS, TX <i></i> HOUSTON, TX</div>
                <div class="shot-price">$${LOAD.rate.toLocaleString()}</div>
                <div class="shot-meta"><span><b>${LOAD.miles} mi</b></span><span><b>${LOAD.weight}</b></span><span><b>${LOAD.equipment}</b></span></div>
                <button class="chat-link" data-open-load>Open load details →</button>
              </div>
            </div>
            <span class="time">${m.time}</span>
          </div>
        </div>`;
    }
    if (m.image) {
      return `
        <div class="chat-row ${m.from === 'me' ? 'me' : ''}">
          <div class="chat-avatar">${m.from === 'me' ? 'D' : 'M'}</div>
          <div class="chat-bubble">${m.text || ''}<img src="${m.image}" alt="Proof" style="width:100%;border-radius:8px;margin-top:6px"><span class="time">${m.time}</span></div>
        </div>`;
    }
    return `
      <div class="chat-row ${m.from === 'me' ? 'me' : ''}">
        <div class="chat-avatar">${m.from === 'me' ? 'D' : 'M'}</div>
        <div class="chat-bubble">${m.text}<span class="time">${m.time}</span></div>
      </div>`;
  }

  renderLoadTab() {
    return `
      <div class="phone-card">
        <h4>Active load · #D2D-0612</h4>
        <div class="big">$${LOAD.rate.toLocaleString()}</div>
        <div class="load-route">
          <div class="stop"><span>PICKUP · ${LOAD.pickup.appt}</span><b>Dallas</b></div>
          <div class="line"></div>
          <div class="stop"><span>DELIVERY · ${LOAD.delivery.appt}</span><b>Houston</b></div>
        </div>
        <div class="row"><span>Shipper</span><b>${LOAD.pickup.name}</b></div>
        <div class="row"><span>Address</span><b>${LOAD.pickup.address}</b></div>
        <div class="row"><span>Receiver</span><b>${LOAD.delivery.name}</b></div>
        <div class="row"><span>Address</span><b>${LOAD.delivery.address}</b></div>
        <div class="row"><span>Equipment</span><b>${LOAD.equipment}</b></div>
        <div class="row"><span>Weight</span><b>${LOAD.weight}</b></div>
        <div class="row"><span>Seal</span><b style="color:#ffb15f">${LOAD.seal}</b></div>
        <div class="row"><span>Commodity</span><b>${LOAD.commodity}</b></div>
        <div class="row"><span>Miles</span><b>${LOAD.miles}</b></div>
        <button class="btn full" id="verify-load">Verify details <span class="arrow">→</span></button>
      </div>
      <p style="font-size:9.5px;color:#6f7e8d;line-height:1.5;text-align:center">LoadLink demo data. This trainer does not connect to a live load board.</p>
    `;
  }

  startQuiz() {
    if (this.phoneOpen) {
      this.phoneOpen = false;
      this.closeModal();
    }
    this.quizIndex = 0;
    this.quizMistakes = 0;
    this.renderQuiz();
  }

  renderQuiz() {
    const q = QUIZ[this.quizIndex];
    this.quizOrder = [0, 1, 2].sort(() => Math.random() - 0.5);
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">LOAD VERIFICATION · ${this.quizIndex + 1}/${QUIZ.length}</span><h2>Check the details</h2></div></div>
      <div class="modal-body">
        <div class="quiz-q">${q.q}</div>
        <div class="quiz-options">
          ${this.quizOrder.map((original, i) => `<button class="quiz-opt" data-opt="${i}"><span class="letter">${String.fromCharCode(65 + i)}</span>${q.options[original]}</button>`).join('')}
        </div>
        <div class="quiz-feedback" id="quiz-feedback"></div>
      </div>
      <div class="modal-foot"><button class="btn full" id="quiz-next" disabled>${this.quizIndex === QUIZ.length - 1 ? 'Start walk-around' : 'Next question'} <span class="arrow">→</span></button></div>
    `, { dismissible: false });
    this.quizAnswered = false;
    $$('[data-opt]', this.modal).forEach((b) => b.addEventListener('click', () => this.answerQuiz(Number(b.dataset.opt))));
    $('#quiz-next').addEventListener('click', () => {
      if (!this.quizAnswered) return;
      this.audio.click();
      if (this.quizIndex < QUIZ.length - 1) {
        this.quizIndex++;
        this.renderQuiz();
      } else {
        this.closeModal();
        this.toast('Details verified. Start the walk-around.', 'good');
        this.goStage(2);
      }
    });
  }

  answerQuiz(i) {
    if (this.quizAnswered) return;
    const q = QUIZ[this.quizIndex];
    this.quizAnswered = true;
    const opts = $$('[data-opt]', this.modal);
    const chosen = this.quizOrder[i];
    opts.forEach((b, idx) => {
      const original = this.quizOrder[idx];
      b.disabled = true;
      if (original === q.answer) b.classList.add('correct');
      if (idx === i && chosen !== q.answer) b.classList.add('wrong');
    });
    const fb = $('#quiz-feedback');
    if (chosen === q.answer) {
      fb.textContent = q.explain;
      fb.className = 'quiz-feedback good';
      this.audio.success();
    } else {
      this.quizMistakes++;
      fb.textContent = `Not quite. ${q.explain}`;
      fb.className = 'quiz-feedback bad';
      this.penalty('communication', 6, 'Communication −6: details must match the dispatch call.');
      this.audio.error();
    }
    $('#quiz-next').disabled = false;
  }

  penalty(category, amount, message = '') {
    this.scores[category] = clamp(this.scores[category] - amount, 0, 100);
    if (message) this.toast(message, 'bad');
    this.persist();
  }

  /* ---------------- PRETRIP ---------------- */

  setupPretrip() {
    this.world.setMode('pretrip');
    this.pretripDone = new Set();
    this.world.setHotspots(INSPECTIONS.map((p) => ({
      id: p.id,
      label: p.label,
      position: p.pos,
      onClick: () => this.showInspection(p)
    })));
    this.updatePretripAction();
  }

  updatePretripAction() {
    const done = this.pretripDone.size;
    if (done >= INSPECTIONS.length) {
      this.world.clearHotspots();
      this.setAction({
        kicker: 'PRE-TRIP · COMPLETE',
        title: 'All five checks complete',
        text: 'Mirrors, seat, brake test, and seatbelt. Start the engine and pull up to Gate 1.',
        buttons: [{ label: 'Enter cab', onClick: () => {
          this.audio.startEngine();
          this.setTimeout(() => this.audio.stopEngine(), 1300);
          this.goStage(3);
        } }]
      });
      return;
    }
    this.setAction({
      kicker: `PRE-TRIP · ${done}/${INSPECTIONS.length}`,
      title: 'Walk-around inspection',
      text: 'Tap each orange marker. Look closely, then mark it checked. Drag the screen to look around the tractor.',
      buttons: [{ label: 'Checklist', variant: 'ghost', onClick: () => this.showPretripChecklist() }]
    });
  }

  showPretripChecklist() {
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">DOT-STYLE WALK-AROUND</span><h2>Tractor checklist</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        ${INSPECTIONS.map((p) => `<div class="check-row ${this.pretripDone.has(p.id) ? 'done' : ''}"><span class="box">${this.pretripDone.has(p.id) ? '✓' : ''}</span><span>${p.title}</span></div>`).join('')}
        <p style="margin-top:12px">Complete every item before you move. A missed defect is cheaper to fix in the yard than on the shoulder.</p>
      </div>
      <div class="modal-foot"><button class="btn full" data-close>Back to tractor</button></div>
    `);
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', () => this.closeModal()));
  }

  showInspection(part) {
    if (this.pretripDone.has(part.id)) {
      this.toast(`${part.title} already checked.`, 'good');
      return;
    }
    this.world.setCamera(part.cam[0], part.cam[1], 0.55, { orbit: true, minRadius: 3.2, maxRadius: 9, minPhi: 0.55 });
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">PRE-TRIP · ${part.label.toUpperCase()}</span><h2>${part.title}</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        <p>${part.body}</p>
        <div class="tip-box"><b>Why it matters:</b> ${part.tip}</div>
      </div>
      <div class="modal-foot"><button class="btn ghost" data-close>Not yet</button><button class="btn" id="inspect-ok">Mark checked</button></div>
    `);
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', () => {
      this.closeModal();
      this.restorePretripCamera();
    }));
    $('#inspect-ok', this.modal).addEventListener('click', () => {
      this.closeModal();
      this.pretripDone.add(part.id);
      this.world.markHotspot(part.id, true);
      this.audio.success();
      this.toast(`${part.title} — checked.`, 'good', 1800);
      this.restorePretripCamera();
      this.updatePretripAction();
    });
  }

  restorePretripCamera() {
    this.world.setCamera([20, 10, 30], [0, 2, 8], 0.7, { orbit: true, minRadius: 12, maxRadius: 38, minPhi: 0.35 });
  }

  /* ---------------- GATE ---------------- */

  setupGate() {
    this.world.setMode('gate');
    this.setAction({
      kicker: '07:22 · NORTHGATE GATE 1',
      title: 'Check in with the guard',
      text: 'Have your appointment, seal number, and driver ID ready. The guard controls access to the yard.',
      buttons: [{ label: 'Approach guard', onClick: () => this.openGateDialogue() }]
    });
  }

  openGateDialogue(message = '') {
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">GATE 1 · SECURITY</span><h2>Check-in</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        <div class="dialogue-top">
          <div class="dialogue-portrait">G</div>
          <div><h3>Gate guard</h3><p>Northgate Logistics</p></div>
        </div>
        <div class="dialogue-text">${message || "Morning. I need your appointment time, the shipper name, and the seal number before I raise this arm."}</div>
        <div class="dialogue-choices">
          <button class="dialogue-choice" data-gate="0">07:30 · Northgate Logistics · seal 482913</button>
          <button class="dialogue-choice" data-gate="1">08:00 · Northgate Logistics · seal 482913</button>
          <button class="dialogue-choice" data-gate="2">07:30 · Gulf Coast Distribution · seal 482913</button>
        </div>
      </div>
    `, { dismissible: false });
    $$('[data-gate]', this.modal).forEach((b) => b.addEventListener('click', () => {
      const choice = Number(b.dataset.gate);
      if (choice === 0) {
        this.closeModal();
        this.audio.success();
        this.world.setBarrier(true);
        this.audio.gate();
        this.toast('Gate open. Door 14 — pull to the yellow line.', 'good');
        this.setAction({
          kicker: 'GATE 1 · CLEARED',
          title: 'Roll through the gate',
          text: 'Proceed slowly. Watch the barrier, the guard, and pedestrians. Follow the yellow line to the trailer spot.',
          buttons: [{ label: 'Roll through', onClick: () => this.goStage(4) }]
        });
      } else {
        this.penalty('compliance', 6, 'Compliance −6: wrong appointment or shipper.');
        this.audio.error();
        this.openGateDialogue("That's not what's on my sheet. Check the load app and give me the correct appointment and shipper.");
      }
    }));
  }

  /* ---------------- COUPLING ---------------- */

  setupCouple() {
    this.world.startCoupling();
    this.hideAction();
    this.showDockControls();
    this.startDockLoop();
    this.toast('Hold REVERSE. Tap ◀ ▶ to steer. Center the kingpin.', '', 4200);
  }

  showDockControls() {
    this.dockControls.classList.remove('hidden');
    this.dockInput = { left: false, right: false, reverse: false, forward: false };
    this.world.setBackingInput(this.dockInput);
  }

  startDockLoop() {
    if (this.dockInterval) clearInterval(this.dockInterval);
    this.dockInterval = setInterval(() => {
      const s = this.world.getBackingState();
      if (!s) return;
      const needle = $('#align-needle');
      if (needle) {
        const pct = 50 + clamp(s.x / 3.6, -1, 1) * 50;
        needle.style.left = `${pct}%`;
        needle.style.background = s.aligned ? '#45d18b' : '#fff';
      }
      this.world.setBackingInput(this.dockInput);
    }, 60);
  }

  stopDockControls(hide = false) {
    if (this.dockInterval) clearInterval(this.dockInterval);
    this.dockInterval = null;
    this.dockInput = { left: false, right: false, reverse: false, forward: false };
    this.world.setBackingInput(this.dockInput);
    if (hide) this.dockControls.classList.add('hidden');
  }

  handleBackingWarning(msg) {
    if (!this.stage) return;
    this.penalty('safety', 4, 'Safety −4: trailer off line.');
    this.audio.error();
    this.toast(msg, 'bad', 3200);
    this.world.shakeCamera(0.12, 0.3);
  }

  handleCoupled() {
    this.stopDockControls(true);
    this.audio.clunk();
    this.world.shakeCamera(0.25, 0.4);
    this.toast('Fifth wheel locked. Finish the hook-up sequence.', 'good');
    this.coupleStep = 0;
    this.showCoupleStep();
  }

  showCoupleStep() {
    if (this.coupleStep >= COUPLE_STEPS.length) {
      this.setAction({
        kicker: 'HOOK-UP · COMPLETE',
        title: 'Ready to roll',
        text: 'Fifth wheel locked, landing gear up, air lines connected, trailer brakes tested. Take the seal photo before you leave.',
        buttons: [{ label: this.practice ? 'Finish drill' : 'Continue to seal', onClick: () => this.practice ? this.finishDrill() : this.goStage(5) }]
      });
      return;
    }
    const step = COUPLE_STEPS[this.coupleStep];
    this.setAction({
      kicker: `HOOK-UP · ${this.coupleStep + 1}/${COUPLE_STEPS.length}`,
      title: step.title,
      text: step.text,
      buttons: [{
        label: step.button,
        onClick: () => {
          if (this.coupleStep === 0) {
            this.audio.clunk();
            this.world.shakeCamera(0.2, 0.35);
            this.toast('Tug test passed. Trailer is locked.', 'good');
          } else if (this.coupleStep === 1) {
            this.world.setLandingGear(true);
            this.audio.air();
            this.toast('Landing gear raised and handle secured.', 'good');
          } else if (this.coupleStep === 2) {
            this.world.airLines.visible = true;
            this.audio.air();
            this.toast('Red and blue gladhands connected.', 'good');
          } else if (this.coupleStep === 3) {
            this.audio.air();
            this.toast('Trailer brake test passed. Brakes release cleanly.', 'good');
          }
          this.coupleStep++;
          this.setTimeout(() => this.showCoupleStep(), 350);
        }
      }]
    });
  }

  /* ---------------- SEAL PHOTO ---------------- */

  setupSeal() {
    this.world.setMode('seal');
    this.setAction({
      kicker: '08:06 · NORTHGATE YARD',
      title: 'Seal verification',
      text: 'Before you leave the shipper, photograph the seal number and send it to dispatch. The number must match the paperwork.',
      buttons: [
        { label: 'Open proof camera', onClick: () => this.openPhoto('seal') },
        { label: 'Load app', variant: 'ghost', onClick: () => this.openPhone('load') }
      ]
    });
  }

  openPhoto(mode) {
    this.closeModal();
    this.photoMode = mode;
    this.photoData = null;
    this.photoUI.classList.remove('hidden');
    this.hideAction();
    this.phoneFab.classList.add('hidden');
    this.hud.classList.add('hidden');
    this.phoneOpen = false;
    this.world.clearHotspots();
    if (mode === 'seal') {
      this.world.setCamera([3.6, 3.2, -23.8], [0.58, 2.36, -16.8], 0.6, { orbit: true, minRadius: 3.4, maxRadius: 10, minPhi: 0.62, maxPhi: 1.32 });
      this.world.photoZoomRange = [10, 4.2];
      $('#photo-label').textContent = 'SEAL · 482913';
      $('#photo-hint').textContent = 'Center the seal number';
      $('#photo-zoom').value = '35';
      this.world.setPhotoZoom(0.35);
      this.toast('Drag to aim. Use the zoom slider. Keep the number sharp.', '', 3000);
    } else {
      this.world.setCamera([-2.0, 2.3, -31.0], [-4.9, 1.2, -33.4], 0.7, { orbit: true, minRadius: 2.8, maxRadius: 6.2, minPhi: 0.62, maxPhi: 1.34 });
      this.world.photoZoomRange = [6.2, 2.8];
      $('#photo-label').textContent = 'POD · SIGNED BOL';
      $('#photo-hint').textContent = 'Frame the signed paperwork';
      $('#photo-zoom').value = '45';
      this.world.setPhotoZoom(0.45);
      this.toast('Frame the signed POD. Make sure the signature is visible.', '', 3000);
    }
  }

  capturePhoto() {
    if (!this.photoMode) return;
    const target = this.photoMode === 'seal' ? this.world.sealTag : this.world.podPaper;
    const screen = this.world.getScreenPosition(target);
    const radius = this.world.orbit.radius;
    const visible = target.visible && screen.visible;
    const centered = screen.x > 0.24 && screen.x < 0.76 && screen.y > 0.14 && screen.y < 0.86;
    const closeEnough = this.photoMode === 'seal' ? radius < 8.2 : radius < 7.8;
    const valid = visible && centered && closeEnough;
    this.photoData = this.world.capturePhoto();
    this.audio.shutter();
    this.world.shakeCamera(0.05, 0.15);
    this.showPhotoPreview(valid, this.photoMode === 'seal' ? 'seal' : 'pod');
  }

  showPhotoPreview(valid, kind) {
    const title = kind === 'seal' ? 'Seal photo' : 'Proof of delivery';
    const status = valid
      ? (kind === 'seal' ? 'Seal 482913 is legible and in frame.' : 'The signed POD is legible and in frame.')
      : (kind === 'seal' ? 'The seal number is not clear enough. Center it and zoom in.' : 'The paperwork is not clear enough. Center it and zoom in.');
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">CAMERA · REVIEW</span><h2>${title}</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        <div class="photo-preview"><img src="${this.photoData}" alt="${title}"><span class="photo-stamp">${kind === 'seal' ? 'SEAL 482913 · 08:06' : 'POD · 13:58'}</span></div>
        <div class="quiz-feedback ${valid ? 'good' : 'bad'}" style="margin-top:12px">${status}</div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" id="photo-retake">Retake</button>
        <button class="btn ${valid ? '' : 'ghost'}" id="photo-send" ${valid ? '' : 'disabled'}>Send to dispatch</button>
      </div>
    `);
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', () => this.closeModal()));
    $('#photo-retake').addEventListener('click', () => {
      this.closeModal();
      this.penalty('time', 2, 'Time −2: retake.');
    });
    $('#photo-send').addEventListener('click', () => {
      if (!valid) return;
      this.closeModal();
      this.saveProof(kind);
    });
  }

  saveProof(kind) {
    const data = this.photoData;
    this.photoUI.classList.add('hidden');
    this.photoMode = null;
    this.hud.classList.remove('hidden');
    this.phoneFab.classList.remove('hidden');
    const label = kind === 'seal' ? 'Seal 482913' : 'Signed POD';
    this.photos.push({ src: data, label });
    if (kind === 'seal') {
      this.addMessage('me', 'Seal photo — 482913. Container is sealed and untouched.', { image: data, time: '08:06' });
      this.addMessage('them', 'Seal verified. You are clear to depart. Drive safe.', { time: '08:07' });
      this.audio.success();
      this.world.setBarrier(true);
      this.toast('Seal proof accepted by dispatch.', 'good');
      if (this.practice === 'seal') {
        this.setTimeout(() => this.finishDrill(), 500);
        return;
      }
      this.setAction({
        kicker: '08:07 · CLEARED',
        title: 'Seal accepted',
        text: 'Dispatch has the proof. Roll out through Gate 1, check traffic, and get onto I-45 South.',
        buttons: [{ label: 'Depart facility', onClick: () => this.goStage(6) }]
      });
    } else {
      this.addMessage('me', 'Proof of delivery — signed BOL, 6 pallets, seal cut.', { image: data, time: '13:58' });
      this.addMessage('them', 'POD received. Clean delivery. I will close the load.', { time: '14:00' });
      this.audio.success();
      this.toast('POD sent. Load is complete.', 'good');
      this.setTimeout(() => this.goStage(9), 550);
    }
  }

  closePhoto(silent = false) {
    this.photoUI.classList.add('hidden');
    this.photoMode = null;
    this.photoData = null;
    if (silent) return;
    this.hud.classList.remove('hidden');
    this.phoneFab.classList.remove('hidden');
    if (this.stage?.key === 'seal') this.setupSeal();
    else if (this.stage?.key === 'pod') this.showPodPhotoAction();
  }

  /* ---------------- ROAD ---------------- */

  setupRoad() {
    this.hud.classList.add('hidden');
    this.phoneFab.classList.add('hidden');
    this.hideAction();
    this.stopDockControls(true);
    this.roadFinished = false;
    this.world.startRoad();
    this.roadHud.classList.remove('hidden');
    this.roadControls.classList.remove('hidden');
    this.audio.startEngine();
    this.updateRoadUI();
    if (this.roadInterval) clearInterval(this.roadInterval);
    this.roadInterval = setInterval(() => this.updateRoadUI(), 120);
    this.toast('Tap ◀ ▶ to change lanes. Watch the traffic.', '', 3500);
  }

  updateRoadUI() {
    if (!this.stage || this.stage.key !== 'road') return;
    const r = this.world.getRoadState();
    if (!r) return;
    const speed = Math.round(61 + Math.sin(r.time * 0.8) * 2);
    $('#road-speed').textContent = String(speed);
    $('#road-distance').textContent = r.distance.toFixed(1);
    const pct = clamp((r.time / 28) * 100, 0, 100);
    $('#road-progress-fill').style.width = `${pct}%`;
    const msg = r.time < 8 ? 'I-45 SOUTH · CLEAR' : r.time < 18 ? 'I-45 SOUTH · TRAFFIC AHEAD' : 'HOUSTON · EXIT 48B · 2 MI';
    $('#road-message').textContent = msg;
  }

  changeLane(dir) {
    if (!this.stage || this.stage.key !== 'road') return;
    this.world.setRoadLane(dir);
    this.audio.tap();
  }

  handleRoadCollision(n) {
    this.penalty('safety', 9, 'Safety −9: following distance / lane contact.');
    this.audio.error();
    const hud = this.roadHud;
    hud.classList.add('road-flash');
    setTimeout(() => hud.classList.remove('road-flash'), 480);
    this.toast('Traffic contact. Increase following distance.', 'bad', 2600);
  }

  finishRoad() {
    if (this.roadFinished) return;
    this.roadFinished = true;
    if (this.roadInterval) clearInterval(this.roadInterval);
    this.roadInterval = null;
    this.audio.stopEngine();
    this.roadHud.classList.add('hidden');
    this.roadControls.classList.add('hidden');
    const r = this.world.getRoadState();
    const collisions = r?.collisions || 0;
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">13:42 · GULF COAST DISTRIBUTION</span><h2>Arrived in Houston</h2></div></div>
      <div class="modal-body">
        <p>You made it to the receiver. Check in at the guardhouse, get your dock assignment, and back in slowly.</p>
        <div class="stat-grid">
          <div class="stat-card ${collisions === 0 ? 'good' : 'warn'}"><span>Traffic contacts</span><strong>${collisions}</strong></div>
          <div class="stat-card"><span>Route</span><strong>240 mi</strong></div>
        </div>
        <div class="tip-box"><b>Dock approach:</b> pull past the dock, square up, then reverse. Use the mirrors. If it feels wrong, pull forward and reset.</div>
      </div>
      <div class="modal-foot"><button class="btn full" id="to-delivery">Check in at dock <span class="arrow">→</span></button></div>
    `);
    $('#to-delivery', this.modal).addEventListener('click', () => {
      this.closeModal();
      this.goStage(7);
    });
  }

  hideRoadUI() {
    this.roadHud.classList.add('hidden');
    this.roadControls.classList.add('hidden');
    if (this.roadInterval) clearInterval(this.roadInterval);
    this.roadInterval = null;
    this.audio.stopEngine();
  }

  /* ---------------- DELIVERY / POD ---------------- */

  setupDelivery() {
    this.world.startDelivery();
    this.hideAction();
    this.showDockControls();
    this.startDockLoop();
    this.toast('Back into Dock 14. Hold REVERSE. Use ◀ ▶ to square up.', '', 4200);
  }

  handleDocked() {
    this.stopDockControls(true);
    this.audio.air();
    this.world.shakeCamera(0.12, 0.3);
    this.toast('Docked at Door 14. Set the parking brake.', 'good');
    if (this.practice === 'dock') {
      this.setTimeout(() => this.finishDrill(), 600);
      return;
    }
    this.setTimeout(() => this.enterStage(8), 850);
  }

  setupPod() {
    this.world.setMode('pod');
    this.world.resetPodState();
    this.podTask = 0;
    this.unloading = false;
    this.showPodCut();
  }

  showPodCut() {
    this.setAction({
      kicker: 'DOCK 14 · SECURE',
      title: 'Cut the seal',
      text: 'Confirm the trailer is parked and the wheels are chocked. Cut the seal and keep it — the receiver may ask for it.',
      buttons: [{ label: 'Cut seal', onClick: () => {
        this.world.cutSeal(true);
        this.audio.thunk();
        this.toast('Seal cut. Keep the tag with the paperwork.', 'good');
        this.podTask = 1;
        this.setTimeout(() => this.showPodOpen(), 450);
      } }]
    });
  }

  showPodOpen() {
    this.setAction({
      kicker: 'DOCK 14 · TRAILER OPEN',
      title: 'Open the doors',
      text: 'Release both door lock rods and swing the doors open. Stay clear of the door path and watch for freight that has shifted.',
      buttons: [{ label: 'Open trailer doors', onClick: () => {
        this.world.setDoors(true);
        this.audio.air();
        this.toast('Doors open. Inspect the load before unloading.', 'good');
        this.podTask = 2;
        this.setTimeout(() => this.showPodUnload(), 750);
      } }]
    });
  }

  showPodUnload() {
    this.setAction({
      kicker: 'DOCK 14 · UNLOAD',
      title: 'Unload six pallets',
      text: 'The receiver’s team takes the freight. Confirm the count, check for damage, and keep the signed paperwork.',
      buttons: [{ label: 'Unload 6 pallets', onClick: () => this.unloadAll() }]
    });
  }

  unloadAll() {
    if (this.unloading) return;
    this.unloading = true;
    this.hideAction();
    this.toast('Unloading freight…', '', 1600);
    for (let i = 0; i < 6; i++) {
      this.setTimeout(() => {
        this.world.unloadBox(i);
        this.audio.thunk();
      }, i * 230);
    }
    this.setTimeout(() => {
      this.unloading = false;
      this.audio.success();
      this.toast('Six pallets staged. Count is clean.', 'good');
      this.showPodPhotoAction();
    }, 1750);
  }

  showPodPhotoAction() {
    this.setAction({
      kicker: 'DOCK 14 · PAPERWORK',
      title: 'Capture the signed POD',
      text: 'Get the receiver’s signature on the BOL, then photograph the signed page. Dispatch needs a clear image to close the load.',
      buttons: [{ label: 'Photograph POD', onClick: () => this.openPhoto('pod') }]
    });
  }

  /* ---------------- DEBRIEF ---------------- */

  showDebrief() {
    this.hud.classList.add('hidden');
    this.phoneFab.classList.add('hidden');
    this.hideAction();
    this.stopDockControls(true);
    this.hideRoadUI();
    this.world.setMode('debrief');
    const total = Math.round(avg(Object.values(this.scores)));
    const stars = total >= 92 ? 3 : total >= 78 ? 2 : 1;
    const safetyBonus = this.scores.safety >= 95 ? 250 : this.scores.safety >= 85 ? 120 : 0;
    const timeBonus = this.scores.time >= 90 ? 100 : 0;
    const deductions = Math.round((100 - total) * 5);
    const net = Math.max(1200, LOAD.rate + safetyBonus + timeBonus - deductions);
    this.best = Math.max(this.best, total);
    this.completed = true;
    this.stageIndex = 9;
    this.stage = STAGES[9];
    this.persist();
    const lowest = Object.entries(this.scores).sort((a, b) => a[1] - b[1])[0][0];
    const starSvg = '<svg viewBox="0 0 24 24"><path d="m12 2.6 2.9 6 6.6.9-4.8 4.6 1.2 6.5-5.9-3.2-5.9 3.2 1.2-6.5L2.5 9.5l6.6-.9 2.9-6Z"/></svg>';
    this.audio.success();
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">14:10 · SHIFT COMPLETE</span><h2>Clean delivery</h2></div></div>
      <div class="modal-body">
        <div class="debrief-hero">
          <div class="stars">${[0, 1, 2].map((i) => starSvg.replace('<svg', `<svg class="${i < stars ? 'on' : ''}"`)).join('')}</div>
          <div class="debrief-score">${total}<span>/100</span></div>
          <p style="margin:4px 0 0">${total >= 92 ? 'Professional run. This is the standard.' : total >= 78 ? 'Solid shift. A little cleanup will make it a three-star run.' : 'You delivered, but the details cost you. Run it back and tighten up.'}</p>
        </div>
        <div class="stat-grid">
          <div class="stat-card ${this.scores.safety >= 90 ? 'good' : 'warn'}"><span>Safety</span><strong>${this.scores.safety}</strong></div>
          <div class="stat-card ${this.scores.compliance >= 90 ? 'good' : 'warn'}"><span>Compliance</span><strong>${this.scores.compliance}</strong></div>
          <div class="stat-card ${this.scores.communication >= 90 ? 'good' : 'warn'}"><span>Communication</span><strong>${this.scores.communication}</strong></div>
          <div class="stat-card ${this.scores.time >= 90 ? 'good' : 'warn'}"><span>Time</span><strong>${this.scores.time}</strong></div>
        </div>
        <div class="pay-row"><span>Load pay</span><b>$${LOAD.rate.toLocaleString()}</b></div>
        <div class="pay-row"><span>Safety bonus</span><b style="color:var(--green)">+$${safetyBonus.toLocaleString()}</b></div>
        <div class="pay-row"><span>On-time bonus</span><b style="color:var(--green)">+$${timeBonus.toLocaleString()}</b></div>
        <div class="pay-row"><span>Errors / retakes</span><b style="color:#ff9b9b">−$${deductions.toLocaleString()}</b></div>
        <div class="pay-row total"><span>Net settlement</span><b>$${net.toLocaleString()}</b></div>
        <div class="tip-box"><b>Coach’s note:</b> ${TIPS[lowest]}</div>
      </div>
      <div class="modal-foot"><button class="btn ghost" id="debrief-menu">Main menu</button><button class="btn" id="debrief-replay">Run it back</button></div>
    `, { dismissible: false });
    $('#debrief-menu', this.modal).addEventListener('click', () => { this.closeModal(); this.showStart(); });
    $('#debrief-replay', this.modal).addEventListener('click', () => { this.closeModal(); this.startNewShift(); });
  }

  /* ---------------- PAUSE / HELP / SETTINGS ---------------- */

  showPause() {
    if (!this.stage || this.stage.key === 'road') return;
    this.audio.click();
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">SHIFT PAUSED</span><h2>${this.stage.name}</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        <p>Take a breath. The load is not going anywhere for a minute.</p>
        <div class="divider"></div>
        <button class="dialogue-choice" id="pause-resume"><b>Resume shift</b><br><span class="muted">Back to the current task.</span></button>
        <button class="dialogue-choice" id="pause-restart"><b>Restart this stage</b><br><span class="muted">Reset the current procedure. Scores stay as they are.</span></button>
        <button class="dialogue-choice" id="pause-help"><b>How to play</b><br><span class="muted">Controls and objectives.</span></button>
        <button class="dialogue-choice" id="pause-settings"><b>Settings</b><br><span class="muted">Sound, voice, and install.</span></button>
      </div>
      <div class="modal-foot"><button class="btn ghost" id="pause-quit">Quit to menu</button><button class="btn" id="pause-resume-2">Resume</button></div>
    `);
    const close = () => this.closeModal();
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', close));
    $('#pause-resume', this.modal).addEventListener('click', close);
    $('#pause-resume-2', this.modal).addEventListener('click', close);
    $('#pause-restart', this.modal).addEventListener('click', () => {
      close();
      this.enterStage(this.stageIndex, { restart: true });
    });
    $('#pause-help', this.modal).addEventListener('click', () => this.showHowTo());
    $('#pause-settings', this.modal).addEventListener('click', () => this.showSettings());
    $('#pause-quit', this.modal).addEventListener('click', () => { close(); this.showStart(); });
  }

  showHowTo() {
    this.audio.click();
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">FIELD GUIDE</span><h2>How to play</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        <div class="check-row"><span class="box">1</span><span><b>Tap the orange markers.</b> They are things you need to inspect, use, or photograph.</span></div>
        <div class="check-row"><span class="box">2</span><span><b>Drag to look around.</b> Pinch or use the zoom slider in camera mode.</span></div>
        <div class="check-row"><span class="box">3</span><span><b>Use the phone.</b> The TeleLink app holds the load details, dispatch chat, and proof photos.</span></div>
        <div class="check-row"><span class="box">4</span><span><b>Backing:</b> hold REVERSE, tap ◀ ▶ to steer, and keep the white alignment needle in the green center.</span></div>
        <div class="check-row"><span class="box">5</span><span><b>No rush.</b> A clean, compliant delivery scores better than a fast, sloppy one.</span></div>
        <div class="tip-box" style="margin-top:14px"><b>Safety:</b> This is a training game. Do not play while driving. Practice only while parked.</div>
      </div>
      <div class="modal-foot"><button class="btn full" data-close>Got it</button></div>
    `);
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', () => this.closeModal()));
  }

  showSettings() {
    this.audio.click();
    this.showModal(`
      <div class="modal-head"><div><span class="kicker">PREFERENCES</span><h2>Settings</h2></div><button class="icon-btn" data-close>${ICONS.close}</button></div>
      <div class="modal-body">
        <div class="setting-row"><div>Sound<small class="sub">Engine, alerts, and interface audio</small></div><button class="switch ${this.settings.sound ? 'on' : ''}" data-toggle="sound" aria-label="Toggle sound"></button></div>
        <div class="setting-row"><div>Dispatcher voice<small class="sub">Reads the call aloud when available</small></div><button class="switch ${this.settings.voice ? 'on' : ''}" data-toggle="voice" aria-label="Toggle voice"></button></div>
        ${this.installPrompt ? '<div class="setting-row"><div>Install<small class="sub">Add Dock to Drop to your home screen</small></div><button class="btn small" data-install>Install</button></div>' : ''}
        <div class="setting-row"><div>Best shift<small class="sub">Saved on this device</small></div><b style="font-family:var(--display);font-size:22px">${this.best || '—'}</b></div>
        <div class="divider"></div>
        <button class="btn ghost full" id="reset-save" style="min-height:42px;font-size:13px">Erase saved progress</button>
        <p style="font-size:9px;line-height:1.5;text-align:center;margin:14px 0 0;color:#6f7e8d">Dock to Drop v1.0 · Independent training sim. Not affiliated with Amazon, Relay, or any truck manufacturer.</p>
      </div>
      <div class="modal-foot"><button class="btn full" data-close>Done</button></div>
    `);
    $$('[data-close]', this.modal).forEach((b) => b.addEventListener('click', () => this.closeModal()));
    $$('[data-toggle]', this.modal).forEach((b) => b.addEventListener('click', () => {
      const key = b.dataset.toggle;
      this.settings[key] = !this.settings[key];
      if (key === 'sound') this.audio.setMuted(!this.settings.sound);
      if (key === 'voice') this.audio.setVoice(this.settings.voice);
      this.persist();
      this.showSettings();
    }));
    const install = $('[data-install]', this.modal);
    if (install) install.addEventListener('click', () => this.promptInstall());
    $('#reset-save', this.modal).addEventListener('click', () => {
      try { localStorage.removeItem(SAVE_KEY); } catch (e) {}
      this.save = {};
      this.best = 0;
      this.completed = false;
      this.closeModal();
      this.toast('Saved progress erased.');
      this.showStart();
    });
  }

  async promptInstall() {
    if (!this.installPrompt) return;
    this.installPrompt.prompt();
    try { await this.installPrompt.userChoice; } catch (e) {}
    this.installPrompt = null;
    this.closeModal();
    this.showStart();
  }
}
