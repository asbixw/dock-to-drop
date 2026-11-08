export class AudioEngine {
  constructor() {
    this.ctx = null;
    this.master = null;
    this.muted = false;
    this.voiceOn = true;
    this.ringTimer = null;
    this.engine = null;
    this.ready = false;
  }

  init() {
    if (this.ready) {
      if (this.ctx?.state === 'suspended') this.ctx.resume();
      return;
    }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    this.ctx = new AC();
    this.master = this.ctx.createGain();
    this.master.gain.value = this.muted ? 0 : 0.55;
    this.master.connect(this.ctx.destination);
    this.ready = true;
  }

  setMuted(muted) {
    this.muted = muted;
    if (this.master) this.master.gain.setTargetAtTime(muted ? 0 : 0.55, this.ctx.currentTime, 0.03);
    if (muted) this.stopRing();
  }

  setVoice(on) {
    this.voiceOn = on;
    if (!on) this.stopSpeak();
  }

  tone(freq, dur = 0.12, type = 'sine', vol = 0.12, slide = 0) {
    if (!this.ctx || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t);
    if (slide) osc.frequency.exponentialRampToValueAtTime(Math.max(40, freq + slide), t + dur);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + 0.012);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    osc.connect(g).connect(this.master);
    osc.start(t);
    osc.stop(t + dur + 0.03);
  }

  noise(dur = 0.2, filterFreq = 1200, vol = 0.12, q = 0.6) {
    if (!this.ctx || this.muted) return;
    const t = this.ctx.currentTime;
    const len = Math.max(1, Math.floor(this.ctx.sampleRate * dur));
    const buf = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
    const data = buf.getChannelData(0);
    for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    const filter = this.ctx.createBiquadFilter();
    filter.type = 'bandpass';
    filter.frequency.value = filterFreq;
    filter.Q.value = q;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(filter).connect(g).connect(this.master);
    src.start(t);
    src.stop(t + dur + 0.02);
  }

  click() {
    this.tone(620, 0.05, 'triangle', 0.06, -80);
  }

  tap() {
    this.tone(380, 0.06, 'sine', 0.05, -40);
  }

  success() {
    [523, 659, 784, 1046].forEach((f, i) => setTimeout(() => this.tone(f, 0.18, 'sine', 0.075), i * 75));
  }

  error() {
    this.tone(180, 0.16, 'sawtooth', 0.07, -60);
    setTimeout(() => this.tone(140, 0.2, 'sawtooth', 0.06, -40), 90);
  }

  shutter() {
    this.noise(0.08, 2600, 0.18, 1.2);
    this.tone(1100, 0.03, 'square', 0.04);
    setTimeout(() => this.noise(0.05, 900, 0.1, 1), 45);
  }

  clunk() {
    this.noise(0.12, 170, 0.22, 1.4);
    this.tone(72, 0.18, 'sine', 0.17, -20);
  }

  air() {
    this.noise(0.65, 2100, 0.12, 0.7);
    setTimeout(() => this.noise(0.4, 1500, 0.07, 0.7), 160);
  }

  gate() {
    this.noise(0.55, 500, 0.08, 0.8);
    this.tone(120, 0.4, 'sawtooth', 0.035, 40);
  }

  thunk() {
    this.noise(0.1, 110, 0.18, 1.2);
  }

  ring() {
    if (this.ringTimer) return;
    const play = () => {
      if (this.muted) return;
      this.tone(880, 0.22, 'sine', 0.09);
      setTimeout(() => this.tone(660, 0.22, 'sine', 0.08), 240);
      setTimeout(() => this.tone(880, 0.22, 'sine', 0.09), 520);
      setTimeout(() => this.tone(660, 0.22, 'sine', 0.08), 760);
    };
    play();
    this.ringTimer = setInterval(play, 2100);
  }

  stopRing() {
    if (this.ringTimer) clearInterval(this.ringTimer);
    this.ringTimer = null;
  }

  pickup() {
    this.stopRing();
    this.tone(440, 0.1, 'sine', 0.08);
    setTimeout(() => this.tone(620, 0.16, 'sine', 0.07), 90);
  }

  hangup() {
    this.stopRing();
    this.tone(420, 0.12, 'sine', 0.06, -100);
    setTimeout(() => this.tone(280, 0.2, 'sine', 0.05, -80), 100);
  }

  startEngine() {
    if (!this.ctx || this.muted) return;
    this.stopEngine();
    const t = this.ctx.currentTime;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.09, t + 0.4);
    const osc = this.ctx.createOscillator();
    osc.type = 'sawtooth';
    osc.frequency.value = 44;
    const osc2 = this.ctx.createOscillator();
    osc2.type = 'triangle';
    osc2.frequency.value = 66;
    const filter = this.ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = 220;
    const lfo = this.ctx.createOscillator();
    lfo.frequency.value = 8;
    const lfoGain = this.ctx.createGain();
    lfoGain.gain.value = 3;
    lfo.connect(lfoGain).connect(osc.frequency);
    osc.connect(filter);
    osc2.connect(filter);
    filter.connect(g).connect(this.master);
    osc.start(t); osc2.start(t); lfo.start(t);
    this.engine = { osc, osc2, lfo, g };
  }

  stopEngine() {
    if (!this.engine) return;
    const { osc, osc2, lfo, g } = this.engine;
    const t = this.ctx.currentTime;
    g.gain.setTargetAtTime(0.0001, t, 0.08);
    setTimeout(() => {
      try { osc.stop(); osc2.stop(); lfo.stop(); } catch (e) {}
    }, 400);
    this.engine = null;
  }

  speak(text, opts = {}) {
    if (!this.voiceOn || this.muted || !('speechSynthesis' in window)) return;
    try {
      this.stopSpeak();
      const u = new SpeechSynthesisUtterance(text);
      u.rate = opts.rate || 0.98;
      u.pitch = opts.pitch ?? 0.88;
      u.volume = opts.volume ?? 0.9;
      const voices = window.speechSynthesis.getVoices();
      const preferred = voices.find((v) => /en[-_](US|GB)/i.test(v.lang) && /male|david|daniel|alex/i.test(v.name)) ||
        voices.find((v) => /en[-_](US|GB)/i.test(v.lang));
      if (preferred) u.voice = preferred;
      window.speechSynthesis.speak(u);
    } catch (e) {}
  }

  stopSpeak() {
    try { window.speechSynthesis?.cancel(); } catch (e) {}
  }
}
