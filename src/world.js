import * as THREE from 'three';

const TAU = Math.PI * 2;
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerp = (a, b, t) => a + (b - a) * t;

function easeInOut(t) {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

function std(color, opts = {}) {
  return new THREE.MeshStandardMaterial({
    color,
    roughness: 0.72,
    metalness: 0.05,
    ...opts
  });
}

function box(parent, w, h, d, material, x = 0, y = 0, z = 0, opts = {}) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  mesh.position.set(x, y, z);
  mesh.castShadow = opts.cast !== false;
  mesh.receiveShadow = opts.receive !== false;
  parent.add(mesh);
  return mesh;
}

function cyl(parent, rt, rb, h, material, x = 0, y = 0, z = 0, rot = null, seg = 16) {
  const mesh = new THREE.Mesh(new THREE.CylinderGeometry(rt, rb, h, seg), material);
  mesh.position.set(x, y, z);
  if (rot) mesh.rotation.set(rot.x || 0, rot.y || 0, rot.z || 0);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  parent.add(mesh);
  return mesh;
}

function canvasTexture(w, h, draw) {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  const ctx = c.getContext('2d');
  draw(ctx, w, h);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.anisotropy = 4;
  return tex;
}

function textPlane(text, opts = {}) {
  const w = opts.width || 1024;
  const h = opts.height || 256;
  const tex = canvasTexture(w, h, (ctx) => {
    ctx.clearRect(0, 0, w, h);
    if (opts.bg) {
      ctx.fillStyle = opts.bg;
      ctx.fillRect(0, 0, w, h);
    }
    ctx.fillStyle = opts.color || '#ffffff';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    const size = opts.fontSize || Math.round(h * 0.44);
    ctx.font = `${opts.weight || 800} ${size}px ${opts.font || 'Arial Narrow, Arial, sans-serif'}`;
    ctx.fillText(text, w / 2, h / 2 + (opts.offsetY || 0));
    if (opts.sub) {
      ctx.font = `600 ${Math.round(size * 0.34)}px Arial, sans-serif`;
      ctx.fillStyle = opts.subColor || 'rgba(255,255,255,.7)';
      ctx.fillText(opts.sub, w / 2, h / 2 + size * 0.72);
    }
  });
  const mat = new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false, side: THREE.DoubleSide });
  const geo = new THREE.PlaneGeometry((opts.physW || 6), (opts.physH || 1.5));
  return new THREE.Mesh(geo, mat);
}

function asphaltTexture() {
  return canvasTexture(512, 512, (ctx, w, h) => {
    ctx.fillStyle = '#555c64';
    ctx.fillRect(0, 0, w, h);
    for (let i = 0; i < 24000; i++) {
      const v = 58 + Math.random() * 44;
      ctx.fillStyle = `rgba(${v},${v + 3},${v + 6},${Math.random() * 0.22})`;
      ctx.fillRect(Math.random() * w, Math.random() * h, 1 + Math.random() * 2, 1 + Math.random() * 2);
    }
    for (let i = 0; i < 30; i++) {
      ctx.strokeStyle = `rgba(20,24,28,${0.05 + Math.random() * 0.1})`;
      ctx.lineWidth = 1 + Math.random() * 3;
      ctx.beginPath();
      const y = Math.random() * h;
      ctx.moveTo(0, y);
      ctx.bezierCurveTo(w * 0.3, y + 20, w * 0.6, y - 20, w, y + 8);
      ctx.stroke();
    }
  });
}

function concreteTexture() {
  return canvasTexture(512, 512, (ctx, w, h) => {
    ctx.fillStyle = '#a9a69e';
    ctx.fillRect(0, 0, w, h);
    for (let i = 0; i < 9000; i++) {
      const v = 150 + Math.random() * 52;
      ctx.fillStyle = `rgba(${v},${v - 1},${v - 5},${Math.random() * 0.16})`;
      ctx.fillRect(Math.random() * w, Math.random() * h, 1.5, 1.5);
    }
    ctx.strokeStyle = 'rgba(65,65,62,.18)';
    ctx.lineWidth = 2;
    for (let x = 0; x <= w; x += 128) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, h);
      ctx.stroke();
    }
    for (let y = 0; y <= h; y += 128) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(w, y);
      ctx.stroke();
    }
  });
}

export class World {
  constructor(canvas) {
    this.canvas = canvas;
    this.renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: true,
      alpha: false,
      powerPreference: 'high-performance',
      preserveDrawingBuffer: true
    });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.12;

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(42, window.innerWidth / window.innerHeight, 0.1, 600);
    this.elapsed = 0;
    this.lastTime = performance.now();
    this.mode = 'menu';
    this.camTarget = new THREE.Vector3(0, 2, 0);
    this.camTween = null;
    this.orbit = { theta: 0.8, phi: 1.1, radius: 24, target: new THREE.Vector3(0, 2, 0), enabled: true, autoSpin: 0, minPhi: 0.25, maxPhi: 1.42, minRadius: 7, maxRadius: 34 };
    this.drag = null;
    this.pointers = new Map();
    this.shake = null;
    this.hotspots = [];
    this.road = null;
    this.onRoadCollision = null;
    this.onRoadComplete = null;

    this.mats = this.createMaterials();
    this.buildSky();
    this.buildLights();
    this.buildYard();
    this.truck = this.buildTruck();
    this.trailer = this.buildTrailer();
    this.scene.add(this.truck, this.trailer);
    this.buildHighway();
    this.bindInput();
    this.resize();
    this.setMode('menu');
    this.renderer.setAnimationLoop(() => this.animate());
  }

  createMaterials() {
    const asphalt = asphaltTexture();
    asphalt.wrapS = asphalt.wrapT = THREE.RepeatWrapping;
    asphalt.repeat.set(20, 20);
    const concrete = concreteTexture();
    concrete.wrapS = concrete.wrapT = THREE.RepeatWrapping;
    concrete.repeat.set(8, 8);
    return {
      asphalt: new THREE.MeshStandardMaterial({ map: asphalt, color: 0xc2c8cf, roughness: 0.95, metalness: 0.02 }),
      road: new THREE.MeshStandardMaterial({ map: asphalt, color: 0x8f969d, roughness: 0.94 }),
      concrete: new THREE.MeshStandardMaterial({ map: concrete, color: 0xd0ccc3, roughness: 0.92 }),
      lineWhite: new THREE.MeshStandardMaterial({ color: 0xe8e3d4, roughness: 0.8 }),
      lineYellow: new THREE.MeshStandardMaterial({ color: 0xe4b23d, roughness: 0.8 }),
      dark: std(0x1a2027, { roughness: 0.8 }),
      metal: std(0x737d88, { roughness: 0.4, metalness: 0.65 }),
      chrome: std(0xc7d0d8, { roughness: 0.22, metalness: 0.9 }),
      tire: std(0x17191c, { roughness: 0.92 }),
      glass: new THREE.MeshStandardMaterial({ color: 0x162733, roughness: 0.08, metalness: 0.55, transparent: true, opacity: 0.9 }),
      cab: std(0x1f4f71, { roughness: 0.33, metalness: 0.22 }),
      cabDark: std(0x173b55, { roughness: 0.36, metalness: 0.2 }),
      trailer: std(0xa84931, { roughness: 0.68, metalness: 0.08 }),
      trailerInterior: std(0x4b463d, { roughness: 1, side: THREE.DoubleSide }),
      trailerRib: std(0x8d3a27, { roughness: 0.72 }),
      chassis: std(0x252a2f, { roughness: 0.8 }),
      warehouse: std(0x6d7378, { roughness: 0.86 }),
      warehouseDark: std(0x434a50, { roughness: 0.9 }),
      roof: std(0x343b42, { roughness: 0.92 }),
      orange: std(0xf36b1c, { roughness: 0.55 }),
      yellow: std(0xf1b83b, { roughness: 0.55 }),
      green: std(0x3f7c5c, { roughness: 0.8 }),
      red: std(0x9c3b33, { roughness: 0.75 }),
      blue: std(0x2f5f7f, { roughness: 0.75 }),
      white: std(0xd8d6cc, { roughness: 0.7 }),
      black: std(0x101316, { roughness: 0.85 }),
      light: new THREE.MeshStandardMaterial({ color: 0xfff2c4, emissive: 0xffc45a, emissiveIntensity: 1.4, roughness: 0.4 }),
      redLight: new THREE.MeshStandardMaterial({ color: 0xff3b2a, emissive: 0xff1a0a, emissiveIntensity: 1.6, roughness: 0.4 }),
      gate: std(0xd7dbe0, { roughness: 0.4, metalness: 0.5 })
    };
  }

  buildSky() {
    this.skyUniforms = {
      top: { value: new THREE.Color(0x4f7fa8) },
      bottom: { value: new THREE.Color(0xffc08a) },
      offset: { value: 30 },
      exponent: { value: 0.82 }
    };
    const mat = new THREE.ShaderMaterial({
      uniforms: this.skyUniforms,
      side: THREE.BackSide,
      depthWrite: false,
      vertexShader: `
        varying vec3 vWorldPosition;
        void main(){
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorldPosition = wp.xyz;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform vec3 top;
        uniform vec3 bottom;
        uniform float offset;
        uniform float exponent;
        varying vec3 vWorldPosition;
        void main(){
          float h = normalize(vWorldPosition + vec3(0.0, offset, 0.0)).y;
          float f = pow(max(h, 0.0), exponent);
          gl_FragColor = vec4(mix(bottom, top, f), 1.0);
        }
      `
    });
    this.scene.add(new THREE.Mesh(new THREE.SphereGeometry(340, 32, 18), mat));

    const glow = canvasTexture(128, 128, (ctx, w, h) => {
      const g = ctx.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, w / 2);
      g.addColorStop(0, 'rgba(255,245,210,1)');
      g.addColorStop(0.16, 'rgba(255,210,130,.95)');
      g.addColorStop(0.45, 'rgba(255,160,80,.28)');
      g.addColorStop(1, 'rgba(255,120,40,0)');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
    });
    this.sun = new THREE.Sprite(new THREE.SpriteMaterial({ map: glow, depthWrite: false, transparent: true, blending: THREE.AdditiveBlending }));
    this.sun.scale.set(42, 42, 1);
    this.sun.position.set(95, 48, -170);
    this.scene.add(this.sun);
    this.scene.fog = new THREE.Fog(0xd9b28d, 100, 270);
  }

  buildLights() {
    this.hemi = new THREE.HemisphereLight(0xa7bfd8, 0x4a3b2c, 1.15);
    this.scene.add(this.hemi);
    this.dir = new THREE.DirectionalLight(0xffc38c, 2.4);
    this.dir.position.set(36, 55, 34);
    this.dir.castShadow = true;
    this.dir.shadow.mapSize.set(1024, 1024);
    this.dir.shadow.camera.left = -52;
    this.dir.shadow.camera.right = 52;
    this.dir.shadow.camera.top = 52;
    this.dir.shadow.camera.bottom = -52;
    this.dir.shadow.camera.near = 1;
    this.dir.shadow.camera.far = 130;
    this.dir.shadow.bias = -0.0004;
    this.scene.add(this.dir, this.dir.target);
    this.ambient = new THREE.AmbientLight(0xa9bdd0, 0.48);
    this.scene.add(this.ambient);
    this.sunFill = new THREE.DirectionalLight(0x7fb4dd, 0.35);
    this.sunFill.position.set(-34, 22, -42);
    this.scene.add(this.sunFill);
  }

  buildYard() {
    const y = new THREE.Group();
    this.yardGroup = y;
    this.scene.add(y);

    // Ground and aprons
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(180, 180), this.mats.asphalt);
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    y.add(ground);

    const apron = new THREE.Mesh(new THREE.PlaneGeometry(62, 30), this.mats.concrete);
    apron.rotation.x = -Math.PI / 2;
    apron.position.set(0, 0.012, -20);
    apron.receiveShadow = true;
    y.add(apron);

    box(y, 10, 0.02, 48, this.mats.road, 0, 0.02, 30, { cast: false });
    box(y, 16, 0.025, 10, this.mats.road, 0, 0.024, 51, { cast: false });

    // Warehouse shell with real dock openings
    box(y, 50, 10, 0.5, this.mats.warehouse, 0, 5, -46.75);
    box(y, 0.5, 10, 16, this.mats.warehouse, -24.75, 5, -39);
    box(y, 0.5, 10, 16, this.mats.warehouse, 24.75, 5, -39);
    box(y, 51, 0.6, 17, this.mats.roof, 0, 10.2, -39);
    box(y, 50, 5.4, 0.5, this.mats.warehouse, 0, 7.3, -31);
    for (const [a, b] of [[-25, -19.95], [-16.05, -10.95], [-7.05, -1.95], [1.95, 7.05], [10.95, 25]]) {
      box(y, b - a, 10, 0.5, this.mats.warehouse, (a + b) / 2, 5, -31);
    }

    // Interior loading floor, walls, ceiling, and lights
    box(y, 49, 0.12, 15.2, this.mats.concrete, 0, 0.06, -38.5, { cast: false });
    box(y, 49, 0.15, 15.2, this.mats.warehouseDark, 0, 9.92, -38.5, { cast: false });
    box(y, 49, 9.8, 0.2, this.mats.warehouseDark, 0, 5, -46.55, { cast: false });
    for (const sx of [-1, 1]) box(y, 0.2, 9.8, 15.2, this.mats.warehouseDark, sx * 24.55, 5, -38.5, { cast: false });
    for (const x of [-15, 0, 15]) {
      box(y, 5.5, 0.08, 0.28, this.mats.light, x, 8.95, -35.5, { cast: false });
    }
    // A little warehouse dressing
    const forklift = new THREE.Group();
    forklift.position.set(-10, 0, -36.5);
    box(forklift, 1.25, 0.9, 2.1, std(0xd9a326, { roughness: 0.6 }), 0, 0.65, 0);
    box(forklift, 0.95, 0.85, 0.9, this.mats.dark, 0, 1.45, -0.35);
    box(forklift, 0.12, 3.1, 0.12, this.mats.metal, -0.55, 1.7, 1.02);
    box(forklift, 0.12, 3.1, 0.12, this.mats.metal, 0.55, 1.7, 1.02);
    box(forklift, 0.9, 0.1, 1.6, this.mats.metal, 0, 0.22, 1.6);
    for (const sx of [-0.48, 0.48]) box(forklift, 0.12, 0.08, 1.3, this.mats.metal, sx, 0.18, 2.0);
    for (const sx of [-0.55, 0.55]) for (const z of [-0.6, 0.6]) this.addWheel(forklift, sx, 0.3, z, 0.3, 0.2);
    y.add(forklift);
    for (let i = 0; i < 4; i++) {
      box(y, 1.2, 0.14, 1.0, std(0x8a6a45), 13 + i * 1.4, 0.1, -34.5);
      box(y, 1.05, 0.8, 0.9, std(0xa38d6d), 13 + i * 1.4, 0.55, -34.5);
    }

    // Facade sign
    this.shipperSignTexture = canvasTexture(1024, 180, (ctx, w, h) => {
      ctx.fillStyle = '#10161b';
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = '#f3eee3';
      ctx.textAlign = 'center';
      ctx.font = '800 86px Arial Narrow, Arial, sans-serif';
      ctx.fillText('NORTHGATE LOGISTICS', w / 2, 92);
      ctx.fillStyle = '#ff8b34';
      ctx.font = '700 25px Arial, sans-serif';
      ctx.fillText('SHIPPER  ·  DALLAS, TX  ·  APPOINTMENT ONLY', w / 2, 138);
    });
    this.receiverSignTexture = canvasTexture(1024, 180, (ctx, w, h) => {
      ctx.fillStyle = '#10161b';
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = '#f3eee3';
      ctx.textAlign = 'center';
      ctx.font = '800 76px Arial Narrow, Arial, sans-serif';
      ctx.fillText('GULF COAST DISTRIBUTION', w / 2, 90);
      ctx.fillStyle = '#65d39a';
      ctx.font = '700 25px Arial, sans-serif';
      ctx.fillText('RECEIVER  ·  HOUSTON, TX  ·  DOCK 14', w / 2, 138);
    });
    this.signMesh = new THREE.Mesh(new THREE.PlaneGeometry(22, 3.9), new THREE.MeshBasicMaterial({ map: this.shipperSignTexture, transparent: true }));
    this.signMesh.position.set(0, 7.9, -30.88);
    y.add(this.signMesh);

    // Dock doors and hardware
    this.dockDoors = [];
    const doorXs = [-18, -9, 0, 9];
    const doorNums = [11, 12, 14, 15];
    doorXs.forEach((x, i) => {
      box(y, 0.24, 5.2, 0.32, this.mats.warehouseDark, x - 2.0, 2.6, -30.82, { cast: false });
      box(y, 0.24, 5.2, 0.32, this.mats.warehouseDark, x + 2.0, 2.6, -30.82, { cast: false });
      box(y, 4.24, 0.32, 0.32, this.mats.warehouseDark, x, 4.92, -30.82, { cast: false });
      const door = box(y, 3.7, 4.6, 0.14, this.mats.dark, x, 2.4, -30.94);
      door.castShadow = false;
      door.userData.closedY = 2.4;
      this.dockDoors.push(door);
      box(y, 3.4, 0.18, 0.4, this.mats.yellow, x, 0.2, -30.4);
      box(y, 0.28, 0.55, 0.24, this.mats.yellow, x - 1.9, 0.5, -30.5);
      box(y, 0.28, 0.55, 0.24, this.mats.yellow, x + 1.9, 0.5, -30.5);
      const lamp = box(y, 0.5, 0.18, 0.1, this.mats.light, x, 5.35, -30.55);
      lamp.castShadow = false;
      const num = textPlane(String(doorNums[i]), { width: 256, height: 128, physW: 1.2, physH: 0.6, color: '#f0e7d5', fontSize: 94 });
      num.position.set(x, 5.62, -30.76);
      y.add(num);
    });
    box(y, 6.5, 0.08, 0.26, this.mats.light, 0, 8.85, -32.4, { cast: false });
    this.dockDoor = this.dockDoors[2];
    this.dockDoorClosedY = 2.4;

    // Dock plate / yellow safety markings
    box(y, 46, 0.025, 0.18, this.mats.lineYellow, 0, 0.03, -29.2, { cast: false });
    for (let x = -20; x <= 20; x += 4) {
      box(y, 0.16, 0.025, 2.6, this.mats.lineYellow, x, 0.03, -28.1, { cast: false });
    }

    // Freight staged on the dock and the POD clipboard
    this.dockCargo = new THREE.Group();
    this.dockCargo.visible = false;
    for (let i = 0; i < 6; i++) {
      box(this.dockCargo, 0.85, 1.05, 1.15, std(i % 3 === 0 ? 0xb1966d : 0xc1a77e, { roughness: 0.85 }), 6.4 + (i % 2) * 0.95, 0.55, -33.0 + Math.floor(i / 2) * 1.25);
    }
    this.dockCargo.children.forEach((c) => (c.visible = false));
    y.add(this.dockCargo);

    const podTex = canvasTexture(512, 680, (ctx, w, h) => {
      ctx.fillStyle = '#f0eadb'; ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = '#22282c'; ctx.textAlign = 'center';
      ctx.font = '800 44px Arial Narrow'; ctx.fillText('PROOF OF DELIVERY', w / 2, 62);
      ctx.font = '700 22px Arial'; ctx.fillText('GULF COAST DISTRIBUTION', w / 2, 98);
      ctx.strokeStyle = '#9b9484'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(40, 120); ctx.lineTo(w - 40, 120); ctx.stroke();
      ctx.textAlign = 'left'; ctx.fillStyle = '#4d5356'; ctx.font = '600 18px Arial';
      const rows = [['BOL', 'D2D-482913'], ['LOAD', 'NORTHGATE → GULF COAST'], ['PIECES', '6 PALLETS'], ['SEAL', '482913 / INTACT'], ['ARRIVAL', '13:58']];
      rows.forEach((r, i) => { ctx.fillText(r[0], 44, 168 + i * 42); ctx.fillStyle = '#22282c'; ctx.font = '700 18px Arial'; ctx.fillText(r[1], 170, 168 + i * 42); ctx.fillStyle = '#4d5356'; ctx.font = '600 18px Arial'; });
      ctx.fillStyle = '#22282c'; ctx.font = '700 22px Arial'; ctx.fillText('RECEIVED BY', 44, 410);
      ctx.strokeStyle = '#3b4144'; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(44, 505); ctx.bezierCurveTo(110, 455, 150, 535, 210, 482); ctx.bezierCurveTo(260, 445, 320, 520, 410, 470); ctx.stroke();
      ctx.font = '600 16px Arial'; ctx.fillStyle = '#6d716c'; ctx.fillText('Dock 14 · 13:58 · signed', 44, 540);
      ctx.strokeStyle = '#c9c1ae'; ctx.strokeRect(18, 18, w - 36, h - 36);
    });
    this.podPaper = new THREE.Mesh(new THREE.PlaneGeometry(0.95, 1.26), new THREE.MeshBasicMaterial({ map: podTex, side: THREE.DoubleSide }));
    this.podPaper.position.set(-4.9, 1.18, -33.4);
    this.podPaper.rotation.set(-0.32, 0.15, 0.03);
    y.add(this.podPaper);
    box(y, 0.08, 1.0, 0.08, this.mats.metal, -5.3, 0.5, -33.4);
    box(y, 0.08, 1.0, 0.08, this.mats.metal, -4.5, 0.5, -33.4);
    box(y, 1.1, 0.06, 0.55, this.mats.dark, -4.9, 0.95, -33.4);

    // Trailer spot markings
    for (const x of [-9, 8]) {
      box(y, 0.14, 0.025, 16, this.mats.lineWhite, x - 2.4, 0.028, -13, { cast: false });
      box(y, 0.14, 0.025, 16, this.mats.lineWhite, x + 2.4, 0.028, -13, { cast: false });
      box(y, 5, 0.025, 0.14, this.mats.lineWhite, x, 0.028, -5, { cast: false });
    }
    box(y, 0.16, 0.025, 34, this.mats.lineYellow, 0, 0.032, 31, { cast: false });
    box(y, 0.16, 0.025, 22, this.mats.lineYellow, 0, 0.032, 9, { cast: false });

    // Gate booth + barrier
    box(y, 4.2, 3.5, 3.4, this.mats.warehouse, 7.7, 1.75, 25.5);
    box(y, 4.7, 0.35, 3.9, this.mats.roof, 7.7, 3.6, 25.5);
    box(y, 3.4, 1.15, 0.08, this.mats.glass, 8.15, 2.35, 23.78, { cast: false });
    box(y, 0.08, 1.4, 2.5, this.mats.glass, 5.58, 2.1, 25.5, { cast: false });
    const gateSign = textPlane('GATE 1', { width: 512, height: 160, physW: 3.2, physH: 1, bg: '#11181f', color: '#ff8b34', fontSize: 100 });
    gateSign.position.set(7.7, 3.9, 23.72);
    y.add(gateSign);
    // Guard silhouette inside the booth
    const guard = new THREE.Group();
    guard.position.set(8.15, 1.25, 24.55);
    box(guard, 0.56, 0.9, 0.38, std(0x24313c, { roughness: 0.9 }), 0, 0.45, 0);
    const head = new THREE.Mesh(new THREE.SphereGeometry(0.19, 12, 8), std(0x8a6a50, { roughness: 0.9 }));
    head.position.set(0, 1.12, 0);
    guard.add(head);
    box(guard, 0.34, 0.1, 0.28, std(0x1a2832, { roughness: 0.9 }), 0, 1.31, 0);
    const badge = box(guard, 0.14, 0.1, 0.02, this.mats.orange, 0.15, 0.6, 0.2, { cast: false });
    y.add(guard);
    const boothLight = box(y, 0.35, 0.06, 0.12, this.mats.light, 8.15, 3.35, 24.5, { cast: false });

    this.barrierPivot = new THREE.Group();
    this.barrierPivot.position.set(3.5, 1.45, 25.2);
    y.add(this.barrierPivot);
    box(this.barrierPivot, 8.2, 0.18, 0.18, this.mats.gate, -4.1, 0, 0);
    for (let x = -0.8; x > -8; x -= 1.1) {
      box(this.barrierPivot, 0.75, 0.22, 0.2, this.mats.orange, x, 0, 0, { cast: false });
    }
    box(y, 0.7, 0.4, 0.7, this.mats.dark, 3.5, 0.2, 25.2);
    this.barrierOpen = false;
    this.barrierAngle = 0;
    this.barrierTarget = 0;

    // Stop line and gate signs
    box(y, 9, 0.03, 0.7, this.mats.lineWhite, 0, 0.04, 20.5, { cast: false });
    const stop = textPlane('STOP', { width: 512, height: 256, physW: 5, physH: 2.5, color: 'rgba(240,235,220,.85)', fontSize: 170 });
    stop.rotation.x = -Math.PI / 2;
    stop.position.set(-5.5, 0.045, 18.6);
    y.add(stop);

    // Fence line
    this.buildFence(y);
    this.buildYardProps(y);
    this.buildDistant(y);

    // Yard lights
    for (const [x, z] of [[-24, 12], [24, 12], [-24, -18], [24, -18]]) {
      const pole = cyl(y, 0.13, 0.18, 8.5, this.mats.dark, x, 4.25, z, null, 8);
      box(y, 1.3, 0.18, 0.45, this.mats.dark, x, 8.5, z);
      box(y, 1.0, 0.12, 0.35, this.mats.light, x, 8.38, z, { cast: false });
      pole.castShadow = true;
    }

    // Shed / office
    box(y, 8, 4, 6, this.mats.warehouseDark, -30, 2, 4);
    box(y, 8.7, 0.4, 6.7, this.mats.roof, -30, 4.2, 4);
    box(y, 2.2, 1.1, 0.08, this.mats.glass, -30, 2.4, 1.02, { cast: false });
    const officeSign = textPlane('DISPATCH', { width: 512, height: 128, physW: 4, physH: 1, bg: '#1a232c', color: '#d9e2ea', fontSize: 70 });
    officeSign.position.set(-30, 5.2, 1.4);
    y.add(officeSign);
  }

  buildFence(y) {
    const fenceTex = canvasTexture(256, 256, (ctx, w, h) => {
      ctx.clearRect(0, 0, w, h);
      ctx.strokeStyle = 'rgba(160,172,180,.62)';
      ctx.lineWidth = 1.5;
      for (let i = -h; i < w + h; i += 18) {
        ctx.beginPath();
        ctx.moveTo(i, 0);
        ctx.lineTo(i + h, h);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(i, h);
        ctx.lineTo(i + h, 0);
        ctx.stroke();
      }
    });
    fenceTex.wrapS = fenceTex.wrapT = THREE.RepeatWrapping;
    fenceTex.repeat.set(3, 1);
    const fenceMat = new THREE.MeshBasicMaterial({ map: fenceTex, transparent: true, opacity: 0.45, side: THREE.DoubleSide, depthWrite: false });

    const panels = [
      [-42, 2.2, -4, 0],
      [42, 2.2, -4, 0],
      [-42, 2.2, 28, 0],
      [42, 2.2, 28, 0]
    ];
    for (const [x, yy, z, rot] of panels) {
      const plane = new THREE.Mesh(new THREE.PlaneGeometry(42, 3.8), fenceMat);
      plane.position.set(x, yy, z);
      plane.rotation.y = rot || Math.PI / 2;
      y.add(plane);
    }
    for (let z = -30; z <= 38; z += 8) {
      for (const x of [-42, 42]) {
        cyl(y, 0.08, 0.1, 4.4, this.mats.metal, x, 2.2, z, null, 6);
      }
    }
    for (let x = -42; x <= 42; x += 8) {
      cyl(y, 0.08, 0.1, 4.4, this.mats.metal, x, 2.2, 38, null, 6);
    }
  }

  buildYardProps(y) {
    // Container stacks in the background
    const colors = [this.mats.blue, this.mats.trailer, this.mats.green, this.mats.white, this.mats.red];
    let ci = 0;
    for (let row = 0; row < 2; row++) {
      for (let i = 0; i < 3; i++) {
        const c = box(y, 2.6, 2.9, 12.2, colors[ci++ % colors.length], -36 + i * 3.1, 1.5 + row * 3.0, 12 + (i % 2) * 3);
        c.rotation.y = Math.PI / 2;
      }
    }

    // Cones along the exit lane
    for (let i = 0; i < 6; i++) {
      this.addCone(y, i < 3 ? -6.8 : 6.8, 14 + i * 4.4);
    }
    this.addCone(y, -10.5, -2);
    this.addCone(y, 10.5, -2);
    this.addCone(y, -12, 26);
    this.addCone(y, 12, 26);

    // Barriers / bollards near dock
    for (let x = -22; x <= 22; x += 8) {
      cyl(y, 0.18, 0.22, 1.0, this.mats.yellow, x, 0.5, -27.8, null, 10);
      cyl(y, 0.05, 0.05, 0.1, this.mats.black, x, 1.02, -27.8, null, 8);
    }

    // Air/electrical hookups at dock
    for (let x = -16; x <= 14; x += 10) {
      box(y, 0.7, 1, 0.45, this.mats.metal, x + 2.5, 1.5, -30.4);
      cyl(y, 0.1, 0.1, 0.6, this.mats.black, x + 2.5, 1.2, -30.15, { x: Math.PI / 2 }, 8);
    }

    // Pallets and freight by the shed
    for (let i = 0; i < 4; i++) {
      box(y, 1.2, 0.14, 1.0, std(0x8a6a45), -26 + i * 1.4, 0.08, 8);
      box(y, 1.05, 0.9, 0.9, std(0xb19b79), -26 + i * 1.4, 0.6, 8);
    }

    // Fuel island in the distance
    box(y, 7, 0.3, 5, this.mats.concrete, -32, 0.15, 26);
    box(y, 4, 3.4, 0.5, this.mats.warehouseDark, -34, 1.7, 26);
    box(y, 0.4, 2.8, 0.4, this.mats.metal, -32, 1.4, 26);
  }

  addCone(parent, x, z) {
    const g = new THREE.Group();
    g.position.set(x, 0, z);
    box(g, 0.55, 0.06, 0.55, this.mats.orange, 0, 0.03, 0);
    const cone = new THREE.Mesh(new THREE.ConeGeometry(0.22, 0.62, 12), this.mats.orange);
    cone.position.y = 0.34;
    cone.castShadow = true;
    g.add(cone);
    const band = new THREE.Mesh(new THREE.CylinderGeometry(0.155, 0.19, 0.1, 12), this.mats.white);
    band.position.y = 0.37;
    g.add(band);
    parent.add(g);
    return g;
  }

  buildDistant(y) {
    // Low hills and tree line
    const hillMat = std(0x63715d, { roughness: 1, flatShading: true });
    for (let i = 0; i < 7; i++) {
      const h = new THREE.Mesh(new THREE.ConeGeometry(22 + Math.random() * 28, 10 + Math.random() * 16, 7), hillMat);
      h.position.set(-130 + i * 44, 0, -105 - Math.random() * 25);
      h.rotation.y = Math.random();
      h.receiveShadow = true;
      y.add(h);
    }
    const trunkMat = std(0x4a3a29, { roughness: 1 });
    const leafMat = std(0x3f5c3f, { roughness: 1, flatShading: true });
    for (let i = 0; i < 26; i++) {
      const x = i % 2 === 0 ? -50 - Math.random() * 12 : 50 + Math.random() * 12;
      const z = -40 + Math.random() * 95;
      const t = new THREE.Group();
      t.position.set(x, 0, z);
      cyl(t, 0.16, 0.24, 3.2, trunkMat, 0, 1.6, 0, null, 5);
      const crown = new THREE.Mesh(new THREE.ConeGeometry(1.5 + Math.random(), 4.2, 6), leafMat);
      crown.position.y = 4.8;
      crown.castShadow = true;
      t.add(crown);
      y.add(t);
    }
  }

  buildTruck() {
    const t = new THREE.Group();
    t.name = 'tractor';

    // Frame and rear deck
    box(t, 2.05, 0.28, 7.4, this.mats.chassis, 0, 0.95, 0);
    box(t, 1.7, 0.16, 2.5, this.mats.dark, 0, 1.12, -2.4);
    box(t, 2.38, 0.34, 0.4, this.mats.chrome, 0, 0.72, 4.2);

    // Cab, sleeper, hood
    box(t, 2.46, 2.25, 2.15, this.mats.cab, 0, 2.05, 0.85);
    box(t, 2.34, 1.95, 1.55, this.mats.cabDark, 0, 1.95, -0.78);
    box(t, 2.34, 0.36, 1.75, this.mats.cab, 0, 3.28, 0.95);
    box(t, 2.05, 1.18, 2.15, this.mats.cab, 0, 1.55, 3.05);
    box(t, 1.98, 0.62, 1.95, this.mats.cabDark, 0, 1.15, 3.12);

    // Hood grille + chrome trim
    box(t, 1.78, 0.82, 0.1, this.mats.chrome, 0, 1.68, 4.14);
    for (let i = -3; i <= 3; i++) {
      box(t, 0.09, 0.64, 0.06, this.mats.dark, i * 0.22, 1.68, 4.2, { cast: false });
    }
    box(t, 1.9, 0.12, 0.12, this.mats.chrome, 0, 2.12, 4.16);
    box(t, 2.15, 0.25, 0.5, this.mats.chrome, 0, 0.72, 4.42);

    // Windshield and side glass
    box(t, 2.12, 0.86, 0.055, this.mats.glass, 0, 2.55, 1.945, { cast: false });
    box(t, 2.12, 0.14, 0.08, this.mats.chrome, 0, 3.05, 1.95, { cast: false });
    for (const s of [-1, 1]) {
      box(t, 0.045, 0.82, 0.95, this.mats.glass, s * 1.235, 2.55, 1.0, { cast: false });
      box(t, 0.045, 0.74, 0.55, this.mats.glass, s * 1.185, 2.48, -0.12, { cast: false });
      // mirror arm + glass
      box(t, 0.06, 0.06, 0.7, this.mats.chrome, s * 1.44, 2.72, 1.92);
      box(t, 0.16, 0.48, 0.32, this.mats.dark, s * 1.48, 2.62, 2.28);
      box(t, 0.12, 0.4, 0.26, this.mats.glass, s * 1.49, 2.62, 2.3, { cast: false });
      // steps
      box(t, 0.55, 0.08, 0.6, this.mats.metal, s * 1.15, 0.75, 0.25);
      box(t, 0.55, 0.08, 0.6, this.mats.metal, s * 1.15, 0.42, 0.25);
      // fuel tank
      const tank = cyl(t, 0.4, 0.4, 1.75, this.mats.chrome, s * 1.19, 0.93, 0.42, { x: Math.PI / 2 }, 18);
      tank.castShadow = true;
      box(t, 0.06, 0.7, 0.12, this.mats.dark, s * 1.28, 0.93, -0.1);
      box(t, 0.06, 0.7, 0.12, this.mats.dark, s * 1.28, 0.93, 0.92);
      // exhaust stack
      cyl(t, 0.085, 0.095, 2.35, this.mats.chrome, s * 1.16, 2.42, -1.46, null, 12);
      box(t, 0.28, 0.1, 0.22, this.mats.dark, s * 1.14, 3.56, -1.5, { cast: false });
    }

    // Headlights, markers, bumper lights
    for (const s of [-1, 1]) {
      box(t, 0.42, 0.22, 0.08, this.mats.light, s * 0.78, 1.12, 4.42, { cast: false });
      box(t, 0.18, 0.11, 0.06, this.mats.orange, s * 0.55, 0.62, 4.44, { cast: false });
    }
    for (let i = -2; i <= 2; i++) {
      box(t, 0.14, 0.08, 0.06, this.mats.orange, i * 0.38, 3.46, 1.75, { cast: false });
    }

    // Wheels
    const wheelZs = [2.95, -1.35, -2.45];
    for (const z of wheelZs) {
      for (const s of [-1, 1]) {
        this.addWheel(t, s * 1.12, 0.56, z, 0.56, 0.4);
      }
    }
    // Fenders and mud flaps
    for (const s of [-1, 1]) {
      box(t, 0.5, 0.35, 1.35, this.mats.cabDark, s * 1.08, 1.12, 2.95);
      box(t, 0.62, 0.65, 0.06, this.mats.black, s * 1.08, 0.48, -3.18, { cast: false });
    }

    // Fifth wheel
    const plate = box(t, 1.35, 0.14, 1.25, this.mats.metal, 0, 1.14, -1.9);
    plate.rotation.x = 0.035;
    box(t, 0.18, 0.16, 0.55, this.mats.dark, 0, 1.26, -1.58);
    this.fifthWheel = plate;

    // Rear lights and plate
    for (const s of [-1, 1]) {
      box(t, 0.2, 0.16, 0.06, this.mats.redLight, s * 0.95, 0.95, -3.72, { cast: false });
    }
    const plateTex = canvasTexture(256, 128, (ctx, w, h) => {
      ctx.fillStyle = '#e8e2d2'; ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = '#20262b'; ctx.textAlign = 'center'; ctx.font = '800 48px Arial Narrow'; ctx.fillText('D2D · 53', w / 2, 62);
      ctx.font = '600 20px Arial'; ctx.fillText('TEXAS  ·  APP TRUCK', w / 2, 94);
    });
    const plateMesh = new THREE.Mesh(new THREE.PlaneGeometry(0.7, 0.35), new THREE.MeshBasicMaterial({ map: plateTex }));
    plateMesh.position.set(0, 0.55, -3.74);
    plateMesh.rotation.y = Math.PI;
    t.add(plateMesh);

    // Cab door decals
    const decal = textPlane('D2D', { width: 512, height: 256, physW: 1.15, physH: 0.58, color: '#f0b15c', fontSize: 150 });
    decal.position.set(1.238, 2.06, 0.45);
    decal.rotation.y = Math.PI / 2;
    t.add(decal);
    const decal2 = decal.clone();
    decal2.position.x = -1.238;
    decal2.rotation.y = -Math.PI / 2;
    t.add(decal2);

    // Air lines (hidden until connection)
    this.airLines = new THREE.Group();
    const lineColors = [0x2d6fb5, 0xb53535, 0x222222];
    lineColors.forEach((c, i) => {
      const curve = new THREE.CatmullRomCurve3([
        new THREE.Vector3(0.55 + i * 0.1, 1.16, -2.35),
        new THREE.Vector3(0.48 + i * 0.1, 0.86, -2.85),
        new THREE.Vector3(0.62 + i * 0.1, 1.05, -3.32),
        new THREE.Vector3(0.58 + i * 0.1, 1.28, -3.58)
      ]);
      const tube = new THREE.Mesh(new THREE.TubeGeometry(curve, 12, 0.035, 6, false), std(c, { roughness: 0.8 }));
      tube.castShadow = true;
      this.airLines.add(tube);
    });
    this.airLines.visible = false;
    t.add(this.airLines);

    return t;
  }

  addWheel(parent, x, y, z, r, w) {
    const tire = new THREE.Mesh(new THREE.CylinderGeometry(r, r, w, 18), this.mats.tire);
    tire.rotation.z = Math.PI / 2;
    tire.position.set(x, y, z);
    tire.castShadow = true;
    tire.receiveShadow = true;
    parent.add(tire);
    const hub = new THREE.Mesh(new THREE.CylinderGeometry(r * 0.43, r * 0.43, w + 0.02, 14), this.mats.chrome);
    hub.rotation.z = Math.PI / 2;
    hub.position.set(x, y, z);
    hub.castShadow = true;
    parent.add(hub);
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * TAU;
      const lug = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, w + 0.035, 6), this.mats.dark);
      lug.rotation.z = Math.PI / 2;
      lug.position.set(x, y + Math.sin(a) * r * 0.27, z + Math.cos(a) * r * 0.27);
      parent.add(lug);
    }
    return tire;
  }

  buildTrailer() {
    const tr = new THREE.Group();
    tr.name = 'trailer';

    // Chassis rails and cross members
    for (const s of [-1, 1]) box(tr, 0.18, 0.22, 13.5, this.mats.chassis, s * 0.9, 1.04, 0);
    for (let z = -6.3; z <= 6.3; z += 1.25) box(tr, 1.95, 0.16, 0.16, this.mats.chassis, 0, 1.04, z);
    box(tr, 2.15, 0.22, 0.35, this.mats.chassis, 0, 0.82, 6.7);

    // Container body
    // Hollow container shell so the rear doors reveal a real cargo bay
    for (const s of [-1, 1]) {
      box(tr, 0.12, 2.9, 13.42, this.mats.trailer, s * 1.22, 2.72, 0);
      box(tr, 0.07, 2.7, 13.2, this.mats.trailerInterior, s * 1.145, 2.72, 0, { cast: false });
    }
    box(tr, 2.56, 0.12, 13.42, this.mats.trailer, 0, 4.16, 0);
    box(tr, 2.4, 0.07, 13.2, this.mats.trailerInterior, 0, 4.08, 0, { cast: false });
    box(tr, 2.56, 0.12, 13.42, this.mats.trailerInterior, 0, 1.29, 0, { cast: false });
    box(tr, 2.56, 2.9, 0.12, this.mats.trailer, 0, 2.72, 6.71);
    box(tr, 2.4, 2.7, 0.07, this.mats.trailerInterior, 0, 2.72, 6.63, { cast: false });
    // Rear opening frame
    box(tr, 2.56, 0.14, 0.18, this.mats.chassis, 0, 4.15, -6.73);
    box(tr, 2.56, 0.14, 0.18, this.mats.chassis, 0, 1.3, -6.73);
    for (const s of [-1, 1]) box(tr, 0.14, 2.9, 0.18, this.mats.chassis, s * 1.21, 2.72, -6.73);
    // Interior light strip
    box(tr, 0.1, 0.04, 11.6, new THREE.MeshStandardMaterial({ color: 0xffe6b0, emissive: 0xffc36b, emissiveIntensity: 0.8 }), 0, 4.02, 0, { cast: false });

    // Cargo inside the trailer
    this.cargoGroup = new THREE.Group();
    this.cargoBoxes = [];
    for (let i = 0; i < 6; i++) {
      const bx = box(this.cargoGroup, 0.85, 1.05, 1.15, std(i % 3 === 0 ? 0xb1966d : 0xc1a77e, { roughness: 0.85 }), (i % 2 === 0 ? -0.58 : 0.58), 1.88, -5.7 + Math.floor(i / 2) * 1.25);
      bx.castShadow = true;
      this.cargoBoxes.push(bx);
    }
    tr.add(this.cargoGroup);
    box(tr, 2.62, 0.16, 13.48, this.mats.trailerRib, 0, 4.18, 0);
    box(tr, 2.62, 0.2, 13.48, this.mats.chassis, 0, 1.3, 0);
    box(tr, 2.5, 0.14, 2.1, this.mats.trailerRib, 0, 4.22, -6.0);

    // Corrugated sides
    for (let z = -6.25; z <= 6.25; z += 0.52) {
      for (const s of [-1, 1]) {
        box(tr, 0.055, 2.72, 0.11, this.mats.trailerRib, s * 1.286, 2.72, z, { cast: false });
      }
    }
    // Side branding
    const sideTex = canvasTexture(1024, 320, (ctx, w, h) => {
      ctx.clearRect(0, 0, w, h);
      ctx.fillStyle = 'rgba(245,238,223,.92)';
      ctx.textAlign = 'center';
      ctx.font = '800 120px Arial Narrow, Arial, sans-serif';
      ctx.fillText('LOADLINK', w / 2, 154);
      ctx.fillStyle = 'rgba(245,238,223,.62)';
      ctx.font = '700 32px Arial, sans-serif';
      ctx.fillText('53 FT  ·  38,500 LB  ·  SEALED FREIGHT', w / 2, 214);
      ctx.fillStyle = 'rgba(245,238,223,.35)';
      ctx.font = '600 22px Arial, sans-serif';
      ctx.fillText('NORTHGATE LOGISTICS  →  GULF COAST DISTRIBUTION', w / 2, 260);
    });
    const sideMat = new THREE.MeshBasicMaterial({ map: sideTex, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    for (const s of [-1, 1]) {
      const m = new THREE.Mesh(new THREE.PlaneGeometry(8.5, 2.65), sideMat);
      m.position.set(s * 1.315, 2.72, 0.2);
      m.rotation.y = s * Math.PI / 2;
      tr.add(m);
    }

    // Rear doors (pivots)
    this.doorPivots = [];
    for (const s of [-1, 1]) {
      const pivot = new THREE.Group();
      pivot.position.set(s * 1.26, 2.7, -6.72);
      tr.add(pivot);
      const door = box(pivot, 1.24, 2.74, 0.09, this.mats.trailerRib, -s * 0.62, 0, 0);
      door.castShadow = true;
      // lock rods
      for (let i = 0; i < 2; i++) {
        const lx = -s * (0.22 + i * 0.6);
        cyl(pivot, 0.028, 0.028, 2.5, this.mats.metal, lx, 0, 0.09, null, 6);
        box(pivot, 0.28, 0.055, 0.06, this.mats.metal, lx, -0.35, 0.12, { cast: false });
      }
      pivot.userData.closed = 0;
      pivot.userData.open = s * 1.62;
      this.doorPivots.push(pivot);
    }
    this.doorOpen = false;
    this.doorAmount = 0;

    // Seal + cable
    const sealTex = canvasTexture(220, 320, (ctx, w, h) => {
      ctx.fillStyle = '#e8b33d'; ctx.fillRect(0, 0, w, h);
      ctx.strokeStyle = '#4b3510'; ctx.lineWidth = 8; ctx.strokeRect(8, 8, w - 16, h - 16);
      ctx.fillStyle = '#2a1d08'; ctx.textAlign = 'center';
      ctx.font = '800 34px Arial'; ctx.fillText('SEAL', w / 2, 62);
      ctx.font = '800 58px Arial Narrow'; ctx.fillText('482913', w / 2, 150);
      ctx.font = '700 18px Arial'; ctx.fillText('DO NOT REMOVE', w / 2, 206);
      ctx.font = '600 15px Arial'; ctx.fillText('NORTHGATE / D2D', w / 2, 252);
      for (let i = 0; i < 4; i++) {
        ctx.fillStyle = 'rgba(90,62,16,.35)';
        ctx.fillRect(30 + i * 45, 280, 20, 20);
      }
    });
    this.sealTag = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.42, 0.04), new THREE.MeshStandardMaterial({ map: sealTex, roughness: 0.45, metalness: 0.25 }));
    this.sealTag.position.set(0.58, 2.36, -6.79);
    this.sealTag.castShadow = true;
    tr.add(this.sealTag);
    const cable = new THREE.Mesh(new THREE.TorusGeometry(0.13, 0.018, 6, 14, Math.PI * 1.7), this.mats.metal);
    cable.position.set(0.58, 2.54, -6.76);
    cable.rotation.x = Math.PI / 2;
    tr.add(cable);
    this.sealCable = cable;
    this.sealVisible = true;

    // Landing gear
    this.landingGear = new THREE.Group();
    this.landingGear.position.set(0, 0, 4.6);
    for (const s of [-1, 1]) {
      box(this.landingGear, 0.15, 2.2, 0.15, this.mats.metal, s * 0.86, 1.2, 0);
      box(this.landingGear, 0.55, 0.12, 0.62, this.mats.chassis, s * 0.86, 0.08, 0.1);
      box(this.landingGear, 0.08, 0.6, 0.08, this.mats.chrome, s * 0.86, 1.8, 0.2);
    }
    box(this.landingGear, 1.75, 0.12, 0.14, this.mats.chassis, 0, 2.25, 0);
    tr.add(this.landingGear);
    this.gearUp = false;
    this.gearAmount = 0;

    // Rear wheels
    for (const z of [-4.7, -5.78]) {
      for (const s of [-1, 1]) this.addWheel(tr, s * 1.12, 0.58, z, 0.58, 0.42);
    }
    // Rear underride guard and lights
    box(tr, 2.1, 0.12, 0.12, this.mats.chassis, 0, 0.55, -6.92);
    for (const s of [-1, 0, 1]) {
      box(tr, 0.2, 0.13, 0.05, this.mats.redLight, s * 0.82, 0.88, -6.95, { cast: false });
    }
    for (const s of [-1, 1]) {
      box(tr, 0.5, 0.5, 0.06, this.mats.black, s * 1.05, 0.38, -6.8, { cast: false });
    }

    // Kingpin
    cyl(tr, 0.13, 0.15, 0.3, this.mats.metal, 0, 1.12, 6.83, null, 12);
    box(tr, 1.15, 0.1, 0.85, this.mats.chassis, 0, 1.25, 6.55);

    return tr;
  }

  buildHighway() {
    const h = new THREE.Group();
    this.highwayGroup = h;
    h.visible = false;
    this.scene.add(h);

    const road = new THREE.Mesh(new THREE.PlaneGeometry(34, 620), this.mats.road);
    road.rotation.x = -Math.PI / 2;
    road.position.set(0, 0, -280);
    road.receiveShadow = true;
    h.add(road);

    for (const s of [-1, 1]) {
      const shoulder = new THREE.Mesh(new THREE.PlaneGeometry(7, 620), this.mats.concrete);
      shoulder.rotation.x = -Math.PI / 2;
      shoulder.position.set(s * 20, 0.012, -280);
      shoulder.receiveShadow = true;
      h.add(shoulder);
      box(h, 0.25, 0.2, 620, this.mats.white, s * 13.6, 0.12, -280, { cast: false });
      // guardrail
      box(h, 0.22, 0.36, 620, this.mats.metal, s * 17.6, 0.8, -280, { cast: false });
      for (let z = 25; z > -580; z -= 9) {
        box(h, 0.2, 1.1, 0.2, this.mats.metal, s * 17.6, 0.55, z, { cast: false });
      }
    }

    // Moving lane dashes
    this.dashGroup = new THREE.Group();
    h.add(this.dashGroup);
    for (let z = 24; z > -420; z -= 10) {
      for (const x of [-3.5, 3.5]) {
        box(this.dashGroup, 0.16, 0.03, 4.2, this.mats.lineWhite, x, 0.03, z, { cast: false });
      }
    }

    // Roadside sign
    const sign = textPlane('HOUSTON  42 MI', { width: 1024, height: 320, physW: 7.5, physH: 2.35, bg: '#1f5b3c', color: '#eef4e8', fontSize: 100, sub: 'KEEP RIGHT  ·  TRUCKS USE RIGHT LANE', subColor: 'rgba(238,244,232,.8)' });
    sign.position.set(12.5, 4.2, -34);
    sign.rotation.y = -0.12;
    h.add(sign);
    box(h, 0.18, 4.2, 0.18, this.mats.metal, 10.8, 2.1, -34);
    box(h, 0.18, 4.2, 0.18, this.mats.metal, 14.2, 2.1, -34);

    // Distant hills and tree line
    const hillMat = std(0x6d7f68, { roughness: 1, flatShading: true });
    for (let i = 0; i < 9; i++) {
      const hill = new THREE.Mesh(new THREE.ConeGeometry(22 + Math.random() * 30, 12 + Math.random() * 18, 7), hillMat);
      hill.position.set(-150 + i * 38, 0, -420 - Math.random() * 40);
      hill.rotation.y = Math.random();
      h.add(hill);
    }

    this.scrollItems = [];
    const trunkMat = std(0x4c3a28, { roughness: 1 });
    const leafMat = std(0x39533a, { roughness: 1, flatShading: true });
    for (let i = 0; i < 34; i++) {
      const side = i % 2 === 0 ? -1 : 1;
      const g = new THREE.Group();
      const z = 18 - Math.random() * 280;
      g.position.set(side * (23 + Math.random() * 26), 0, z);
      cyl(g, 0.18, 0.3, 3.6, trunkMat, 0, 1.8, 0, null, 5);
      const crown = new THREE.Mesh(new THREE.ConeGeometry(1.7 + Math.random() * 1.1, 5.2, 6), leafMat);
      crown.position.y = 5.5;
      crown.castShadow = true;
      g.add(crown);
      h.add(g);
      this.scrollItems.push(g);
    }

    // Light poles
    for (let i = 0; i < 7; i++) {
      const side = i % 2 === 0 ? -1 : 1;
      const g = new THREE.Group();
      g.position.set(side * 20.5, 0, 8 - i * 42);
      cyl(g, 0.13, 0.2, 9, this.mats.metal, 0, 4.5, 0, null, 6);
      box(g, 2.2, 0.12, 0.35, this.mats.metal, -side * 1.1, 9, 0);
      box(g, 0.9, 0.12, 0.3, this.mats.light, -side * 1.5, 8.88, 0, { cast: false });
      h.add(g);
      this.scrollItems.push(g);
    }

    // Traffic
    this.carsGroup = new THREE.Group();
    h.add(this.carsGroup);
    this.carPalette = [0xc9c4b8, 0x2f4d66, 0x8a3b32, 0x2c2e31, 0xd4d7d9, 0x436d58];

    // Player rig clones
    this.roadRig = new THREE.Group();
    this.roadRig.visible = false;
    h.add(this.roadRig);
    this.roadTruck = this.truck.clone(true);
    this.roadTruck.position.set(0, 0, 0);
    this.roadTruck.rotation.y = Math.PI;
    this.roadTrailer = this.trailer.clone(true);
    this.roadTrailer.position.set(0, 0, 8.55);
    this.roadTrailer.rotation.y = Math.PI;
    this.roadRig.add(this.roadTruck, this.roadTrailer);

    // Headlight glow for road
    const glow = new THREE.SpriteMaterial({ map: this.makeGlowTexture('#fff3c4'), transparent: true, depthWrite: false, blending: THREE.AdditiveBlending });
    for (const s of [-1, 1]) {
      const sp = new THREE.Sprite(glow);
      sp.scale.set(2.6, 2.6, 1);
      sp.position.set(s * 0.78, 1.15, -4.55);
      this.roadTruck.add(sp);
    }

    this.road = { active: false, lane: 1, time: 0, distance: 0, collisions: 0, cars: [], spawnTimer: 0, complete: false, lastCar: null };
  }

  makeGlowTexture(color) {
    return canvasTexture(64, 64, (ctx, w, h) => {
      const g = ctx.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, w / 2);
      g.addColorStop(0, color);
      g.addColorStop(0.3, `${color}88`);
      g.addColorStop(1, 'rgba(255,255,255,0)');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
    });
  }

  makeCar(color) {
    const g = new THREE.Group();
    const bodyMat = std(color, { roughness: 0.45, metalness: 0.15 });
    box(g, 1.9, 0.62, 4.2, bodyMat, 0, 0.62, 0);
    box(g, 1.7, 0.55, 2.1, bodyMat, 0, 1.12, -0.18);
    box(g, 1.55, 0.42, 1.15, this.mats.glass, 0, 1.14, 0.28, { cast: false });
    for (const s of [-1, 1]) {
      for (const z of [-1.3, 1.3]) {
        const w = cyl(g, 0.32, 0.32, 0.22, this.mats.tire, s * 0.97, 0.33, z, { z: Math.PI / 2 }, 12);
        w.castShadow = false;
      }
      box(g, 0.4, 0.12, 0.05, this.mats.redLight, s * 0.62, 0.7, 2.11, { cast: false });
    }
    return g;
  }

  setRoadLane(dir) {
    if (!this.road || !this.road.active) return;
    this.road.lane = clamp(this.road.lane + dir, 0, 2);
  }

  startRoad() {
    this.setMode('road');
    this.road = { active: true, lane: 1, time: 0, distance: 0, collisions: 0, cars: [], spawnTimer: 0.5, complete: false, lastCar: null };
    this.roadRig.position.set(0, 0, 0);
    this.roadRig.rotation.set(0, 0, 0);
    this.carsGroup.clear();
    this.roadTruck.position.set(0, 0, 0);
    this.roadTrailer.position.set(0, 0, 8.55);
    this.camera.position.set(0, 13, 42);
    this.camTarget.set(0, 2, 2);
    this.camera.lookAt(this.camTarget);
    this.orbit.enabled = false;
    this.camTween = null;
    this.setSky('day');
  }

  updateRoad(dt) {
    const r = this.road;
    if (!r || !r.active) return;
    r.time += dt;
    r.distance = Math.min(3, r.time / 28 * 3);
    const speed = 40;
    this.dashGroup.position.z = (this.dashGroup.position.z + speed * dt) % 10;
    for (const item of this.scrollItems) {
      item.position.z += speed * dt;
      if (item.position.z > 32) item.position.z -= 310;
    }

    const targetX = (r.lane - 1) * 4;
    this.roadRig.position.x = lerp(this.roadRig.position.x, targetX, 1 - Math.exp(-dt * 7));
    this.roadRig.rotation.z = lerp(this.roadRig.rotation.z, (targetX - this.roadRig.position.x) * 0.012, 1 - Math.exp(-dt * 6));

    // Traffic spawn and movement
    r.spawnTimer -= dt;
    if (r.spawnTimer <= 0 && r.cars.length < 5) {
      r.spawnTimer = 1.2 + Math.random() * 1.5;
      const lane = Math.floor(Math.random() * 3);
      const car = this.makeCar(this.carPalette[Math.floor(Math.random() * this.carPalette.length)]);
      car.position.set((lane - 1) * 4, 0, -90 - Math.random() * 30);
      car.userData.lane = lane;
      car.userData.hit = false;
      this.carsGroup.add(car);
      r.cars.push(car);
    }
    for (const car of r.cars) {
      car.position.z += 19 * dt;
      if (!car.userData.hit && car.position.z > -2.4 && car.position.z < 3.2 && car.userData.lane === r.lane) {
        car.userData.hit = true;
        r.collisions++;
        r.lastCar = car;
        this.shakeCamera(0.22, 0.45);
        if (this.onRoadCollision) this.onRoadCollision(r.collisions);
        car.position.x += car.position.x >= 0 ? 2.4 : -2.4;
        car.rotation.y += 0.7;
      }
    }
    r.cars = r.cars.filter((car) => {
      if (car.position.z > 30 || car.userData.hit) {
        this.carsGroup.remove(car);
        return false;
      }
      return true;
    });

    // Camera chase
    const camX = this.roadRig.position.x * 0.42;
    this.camera.position.x = lerp(this.camera.position.x, camX, 1 - Math.exp(-dt * 4));
    this.camera.position.y = 13 + Math.sin(r.time * 1.6) * 0.06;
    this.camera.position.z = 42;
    this.camTarget.set(this.roadRig.position.x * 0.3, 2, 2);
    this.camera.lookAt(this.camTarget);
    this.applyShake(dt);

    if (r.time >= 28 && !r.complete) {
      r.complete = true;
      r.active = false;
      if (this.onRoadComplete) this.onRoadComplete(r);
    }
  }

  getRoadState() {
    return this.road;
  }

  bindInput() {
    const c = this.canvas;
    c.addEventListener('contextmenu', (e) => e.preventDefault());
    c.addEventListener('pointerdown', (e) => {
      if (!this.orbit.enabled) return;
      c.setPointerCapture?.(e.pointerId);
      this.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (this.pointers.size === 1) {
        this.drag = { x: e.clientX, y: e.clientY };
      } else if (this.pointers.size === 2) {
        const [a, b] = [...this.pointers.values()];
        this.pinch = { dist: Math.hypot(a.x - b.x, a.y - b.y), radius: this.orbit.radius };
        this.drag = null;
      }
    });
    c.addEventListener('pointermove', (e) => {
      if (!this.pointers.has(e.pointerId)) return;
      this.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (this.pointers.size === 2 && this.pinch) {
        const [a, b] = [...this.pointers.values()];
        const dist = Math.hypot(a.x - b.x, a.y - b.y);
        this.orbit.radius = clamp(this.pinch.radius * (this.pinch.dist / Math.max(dist, 1)), this.orbit.minRadius, this.orbit.maxRadius);
        return;
      }
      if (!this.drag) return;
      const dx = e.clientX - this.drag.x;
      const dy = e.clientY - this.drag.y;
      this.orbit.theta -= dx * 0.006;
      this.orbit.phi = clamp(this.orbit.phi - dy * 0.0045, this.orbit.minPhi, this.orbit.maxPhi);
      this.drag = { x: e.clientX, y: e.clientY };
    });
    const end = (e) => {
      this.pointers.delete(e.pointerId);
      if (this.pointers.size < 2) this.pinch = null;
      if (this.pointers.size === 0) this.drag = null;
    };
    c.addEventListener('pointerup', end);
    c.addEventListener('pointercancel', end);
    c.addEventListener('wheel', (e) => {
      if (!this.orbit.enabled) return;
      e.preventDefault();
      this.orbit.radius = clamp(this.orbit.radius + e.deltaY * 0.02, this.orbit.minRadius, this.orbit.maxRadius);
    }, { passive: false });
  }

  resize() {
    const w = window.innerWidth;
    const h = window.innerHeight;
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setSize(w, h);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  setCamera(pos, target, duration = 1.2, opts = {}) {
    const p = new THREE.Vector3(pos[0], pos[1], pos[2]);
    const t = new THREE.Vector3(target[0], target[1], target[2]);
    this.orbit.enabled = opts.orbit !== false;
    this.orbit.autoSpin = opts.autoSpin || 0;
    this.orbit.minPhi = opts.minPhi ?? 0.25;
    this.orbit.maxPhi = opts.maxPhi ?? 1.42;
    this.orbit.minRadius = opts.minRadius ?? 6;
    this.orbit.maxRadius = opts.maxRadius ?? 38;
    if (duration <= 0.01) {
      this.camera.position.copy(p);
      this.camTarget.copy(t);
      this.camera.lookAt(t);
      this.syncOrbit();
      this.camTween = null;
      return;
    }
    this.camTween = {
      fromPos: this.camera.position.clone(),
      fromTarget: this.camTarget.clone(),
      toPos: p,
      toTarget: t,
      t: 0,
      duration
    };
  }

  syncOrbit() {
    this.orbit.target.copy(this.camTarget);
    const off = this.camera.position.clone().sub(this.camTarget);
    this.orbit.radius = Math.max(off.length(), 0.1);
    this.orbit.theta = Math.atan2(off.x, off.z);
    this.orbit.phi = Math.acos(clamp(off.y / this.orbit.radius, -1, 1));
  }

  setOrbitRadius(r) {
    this.orbit.radius = clamp(r, this.orbit.minRadius, this.orbit.maxRadius);
  }

  updateCamera(dt) {
    if (this.camTween) {
      const tw = this.camTween;
      tw.t += dt / tw.duration;
      const k = easeInOut(clamp(tw.t, 0, 1));
      this.camera.position.lerpVectors(tw.fromPos, tw.toPos, k);
      this.camTarget.lerpVectors(tw.fromTarget, tw.toTarget, k);
      this.camera.lookAt(this.camTarget);
      if (tw.t >= 1) {
        this.camTween = null;
        this.syncOrbit();
      }
      return;
    }
    if (!this.orbit.enabled) return;
    if (this.orbit.autoSpin) this.orbit.theta += this.orbit.autoSpin * dt;
    const target = this.orbit.target;
    const pos = new THREE.Vector3().setFromSphericalCoords(this.orbit.radius, this.orbit.phi, this.orbit.theta).add(target);
    const k = 1 - Math.exp(-dt * 9);
    this.camera.position.lerp(pos, k);
    this.camTarget.lerp(target, k);
    this.camera.lookAt(this.camTarget);
  }

  shakeCamera(amp = 0.12, dur = 0.35) {
    this.shake = { amp, dur, t: 0 };
  }

  applyShake(dt) {
    if (!this.shake) return;
    this.shake.t += dt;
    const k = 1 - this.shake.t / this.shake.dur;
    if (k <= 0) {
      this.shake = null;
      return;
    }
    const a = this.shake.amp * k;
    this.camera.position.x += (Math.random() - 0.5) * a;
    this.camera.position.y += (Math.random() - 0.5) * a * 0.6;
  }

  setSky(preset) {
    const presets = {
      dawn: {
        top: 0x5b8ab8, bottom: 0xffd0a0, fog: 0xd8b79b, sun: 0xffd09a, hemiSky: 0xb4cde3, hemiGround: 0x5a4734,
        hemi: 1.28, dir: 2.65, fill: 0.42, sunPos: [95, 48, -170], dirPos: [36, 55, 34], fogNear: 95, fogFar: 280
      },
      day: {
        top: 0x3f86bd, bottom: 0xdcecf3, fog: 0xbdd4e2, sun: 0xfff2cc, hemiSky: 0xc2def2, hemiGround: 0x63513d,
        hemi: 1.42, dir: 2.75, fill: 0.5, sunPos: [20, 105, -180], dirPos: [30, 65, 30], fogNear: 130, fogFar: 350
      },
      afternoon: {
        top: 0x4c76a0, bottom: 0xffc79a, fog: 0xd6b394, sun: 0xffbe7d, hemiSky: 0xb3c8dc, hemiGround: 0x604832,
        hemi: 1.22, dir: 2.5, fill: 0.4, sunPos: [-80, 38, -170], dirPos: [-30, 48, 30], fogNear: 100, fogFar: 295
      }
    };
    const p = presets[preset] || presets.day;
    this.skyUniforms.top.value.setHex(p.top);
    this.skyUniforms.bottom.value.setHex(p.bottom);
    this.scene.fog.color.setHex(p.fog);
    this.scene.fog.near = p.fogNear;
    this.scene.fog.far = p.fogFar;
    this.hemi.color.setHex(p.hemiSky);
    this.hemi.groundColor.setHex(p.hemiGround);
    this.hemi.intensity = p.hemi;
    this.dir.color.setHex(p.sun);
    this.dir.intensity = p.dir;
    this.sunFill.color.setHex(p.fill ? 0x7fb4dd : 0xffffff);
    this.sunFill.intensity = p.fill;
    this.sun.position.set(...p.sunPos);
    this.dir.position.set(...p.dirPos);
    this.ambient.intensity = preset === 'dawn' ? 0.58 : preset === 'day' ? 0.48 : 0.52;
    this.sunFill.position.set(p.dirPos[0] * -0.9, 22, -42);
    this.renderer.toneMappingExposure = preset === 'dawn' ? 1.02 : 1.08;
  }

  setSign(type) {
    this.signMesh.material.map = type === 'receiver' ? this.receiverSignTexture : this.shipperSignTexture;
    this.signMesh.material.needsUpdate = true;
  }

  prepareRig(truckZ, trailerZ = null, attach = false) {
    if (this.trailer.parent !== this.scene) this.scene.attach(this.trailer);
    this.truck.position.set(0, 0, truckZ);
    this.truck.rotation.set(0, 0, 0);
    this.trailer.position.set(0, 0, trailerZ ?? -10);
    this.trailer.rotation.set(0, 0, 0);
    this.truck.visible = true;
    this.landingGear.position.y = 0;
    this.gearUp = false;
    this.gearAmount = 0;
    this.doorOpen = false;
    this.doorAmount = 0;
    this.doorPivots.forEach((p) => (p.rotation.y = 0));
    this.cutSeal(false);
    this.airLines.visible = attach;
    this.docked = false;
    this.setDockDoor(false, true);
    if (attach) {
      this.truck.attach(this.trailer);
      this.trailer.position.set(0, 0, -8.5);
      this.trailer.rotation.set(0, 0, 0);
    }
  }

  setMode(mode) {
    this.mode = mode;
    this.backing = null;
    if (mode !== 'road' && this.road) this.road.active = false;
    const road = mode === 'road';
    this.yardGroup.visible = !road;
    this.highwayGroup.visible = road;
    this.roadRig.visible = road;
    this.truck.visible = !road;
    this.trailer.visible = !road;
    this.camTween = null;
    this.orbit.autoSpin = 0;
    this.barrierOpen = false;
    this.barrierTarget = 0;
    this.dockOpen = false;
    this.setSign('shipper');

    if (mode === 'menu') {
      this.prepareRig(6, -10, false);
      this.truck.position.set(5, 0, 7);
      this.truck.rotation.y = -0.2;
      this.setSky('dawn');
      this.setCamera([40, 13, 42], [-1, -0.5, -4], 0, { orbit: true, autoSpin: 0.01, minRadius: 26, maxRadius: 64 });
    } else if (mode === 'call') {
      this.prepareRig(8, -10, false);
      this.setSky('dawn');
      this.setCamera([15, 7, 17], [0, 2.2, 2], 1.2, { orbit: true, autoSpin: 0.008, minRadius: 12, maxRadius: 28 });
    } else if (mode === 'pretrip') {
      this.prepareRig(8, -10, false);
      this.trailer.visible = false;
      this.setSky('dawn');
      this.setCamera([20, 10, 30], [0, 2, 8], 1.2, { orbit: true, minRadius: 12, maxRadius: 38, minPhi: 0.35 });
    } else if (mode === 'gate') {
      this.prepareRig(14, -10, false);
      this.trailer.visible = false;
      this.setSky('day');
      this.setCamera([-18, 7.5, 37], [4, 2.5, 24], 1.3, { orbit: true, minRadius: 12, maxRadius: 38 });
    } else if (mode === 'couple') {
      this.prepareRig(10, -10, false);
      this.setSky('day');
      this.setCamera([4, 10, 32], [0, 1, -8], 1.3, { orbit: false });
    } else if (mode === 'seal') {
      this.prepareRig(-1.5, -10, true);
      this.gearUp = true;
      this.airLines.visible = true;
      this.setSky('day');
      this.setCamera([3.6, 3.2, -23.8], [0.58, 2.36, -16.8], 1.3, { orbit: true, minRadius: 3.4, maxRadius: 10, minPhi: 0.62, maxPhi: 1.32 });
    } else if (mode === 'delivery') {
      this.prepareRig(-4, -10, true);
      this.gearUp = true;
      this.setSign('receiver');
      this.setSky('afternoon');
      this.setCamera([23, 11, -2], [0, 1.5, -24], 1.3, { orbit: false });
    } else if (mode === 'pod') {
      if (!this.docked) {
        this.prepareRig(-15.3, null, true);
        this.gearUp = true;
        this.airLines.visible = true;
        this.docked = true;
      }
      this.setSign('receiver');
      this.setSky('afternoon');
      this.dockOpen = true;
      this.setCamera([0, 3.0, -44], [0, 2.2, -30.5], 1.2, { orbit: true, minRadius: 5, maxRadius: 16, minPhi: 0.62, maxPhi: 1.33 });
    } else if (mode === 'debrief') {
      this.setSign('receiver');
      this.setSky('afternoon');
      this.setCamera([21, 11, 6], [0, 2, -14], 1.6, { orbit: true, autoSpin: 0.012, minRadius: 18, maxRadius: 40 });
    }
  }

  setBarrier(open) {
    this.barrierOpen = open;
  }

  setDockDoor(open, instant = false) {
    this.dockOpen = open;
    if (instant && this.dockDoor) this.dockDoor.position.y = open ? 7.4 : this.dockDoorClosedY;
  }

  setDoors(open) {
    this.doorOpen = open;
  }

  setLandingGear(up) {
    this.gearUp = up;
  }

  cutSeal(cut = true) {
    this.sealVisible = !cut;
    if (this.sealTag) this.sealTag.visible = !cut;
    if (this.sealCable) this.sealCable.visible = !cut;
  }

  resetPodState() {
    this.cutSeal(false);
    this.setDoors(false);
    this.resetCargo();
    this.setDockDoor(true);
    this.gearUp = true;
    this.airLines.visible = true;
  }

  resetCargo() {
    this.cargoGroup.visible = true;
    this.cargoBoxes.forEach((b) => (b.visible = true));
    this.dockCargo.visible = false;
    this.dockCargo.children.forEach((c) => (c.visible = false));
  }

  unloadBox(index) {
    const b = this.cargoBoxes[index];
    if (!b || !b.visible) return false;
    b.visible = false;
    this.dockCargo.visible = true;
    const d = this.dockCargo.children[index];
    if (d) {
      d.visible = true;
      d.scale.set(0.01, 0.01, 0.01);
      d.userData.pop = 0;
    }
    return true;
  }

  updateAnimations(dt) {
    const k = 1 - Math.exp(-dt * 7);
    const bTarget = this.barrierOpen ? -1.35 : 0;
    this.barrierAngle = lerp(this.barrierAngle, bTarget, k);
    if (this.barrierPivot) this.barrierPivot.rotation.z = this.barrierAngle;

    if (this.dockDoor) {
      const yTarget = this.dockOpen ? 7.4 : this.dockDoorClosedY;
      this.dockDoor.position.y = lerp(this.dockDoor.position.y, yTarget, 1 - Math.exp(-dt * 3.4));
    }

    const gTarget = this.gearUp ? 1.75 : 0;
    this.gearAmount = lerp(this.gearAmount, gTarget, 1 - Math.exp(-dt * 5));
    if (this.landingGear) this.landingGear.position.y = this.gearAmount;

    const dTarget = this.doorOpen ? 1 : 0;
    this.doorAmount = lerp(this.doorAmount, dTarget, 1 - Math.exp(-dt * 4.5));
    if (this.doorPivots) {
      this.doorPivots.forEach((p) => {
        p.rotation.y = p.userData.open * this.doorAmount;
      });
    }

    for (const c of this.dockCargo ? this.dockCargo.children : []) {
      if (c.userData.pop !== undefined && c.visible && c.userData.pop < 1) {
        c.userData.pop = Math.min(1, c.userData.pop + dt * 3);
        const s = 1 - Math.pow(1 - c.userData.pop, 3);
        c.scale.setScalar(0.2 + s * 0.8);
      }
    }
  }

  startCoupling() {
    this.setMode('couple');
    this.backing = {
      kind: 'couple', x: 0, z: 10, speed: 0, input: 0, locked: false,
      targetZ: -1.5, tol: 0.55, warned: false, progress: 0
    };
    this.truck.position.set(0, 0, 10);
    this.truck.rotation.set(0, 0, 0);
  }

  startDelivery() {
    this.setMode('delivery');
    this.backing = {
      kind: 'delivery', x: 0, z: -4, speed: 0, input: 0, locked: false,
      targetZ: -15.3, tol: 0.62, warned: false, progress: 0
    };
    this.truck.position.set(0, 0, -4);
    this.truck.rotation.set(0, 0, 0);
  }

  setBackingInput(input) {
    if (this.backing) this.backing.input = input;
  }

  updateBacking(dt) {
    const b = this.backing;
    if (!b || b.locked) return;
    const inp = b.input || {};
    let dir = 0;
    if (inp.reverse && !inp.forward) dir = -1;
    if (inp.forward && !inp.reverse) dir = 1;
    const max = b.kind === 'couple' ? 2.9 : 2.4;
    b.speed = lerp(b.speed, dir * max, 1 - Math.exp(-dt * 7));
    if (Math.abs(b.speed) < 0.02) b.speed = 0;
    b.z += b.speed * dt;
    const steer = (inp.right ? 1 : 0) - (inp.left ? 1 : 0);
    if (Math.abs(b.speed) > 0.08) {
      b.x += steer * 1.45 * dt * Math.min(1, Math.abs(b.speed) / max + 0.25);
    }
    b.x = clamp(b.x, -3.6, 3.6);
    if (b.z > 13) b.z = 13;
    this.truck.position.x = b.x;
    this.truck.position.z = b.z;
    this.truck.rotation.y = b.kind === 'couple' ? -steer * 0.025 : 0;
    b.progress = clamp((b.kind === 'couple' ? 10 - b.z : -4 - b.z) / (b.kind === 'couple' ? 11.5 : 11.3), 0, 1);

    if (b.kind === 'couple') {
      if (b.z <= b.targetZ && Math.abs(b.x) <= b.tol) {
        b.locked = true;
        b.speed = 0;
        b.z = b.targetZ;
        this.truck.position.set(0, 0, b.targetZ);
        this.truck.rotation.set(0, 0, 0);
        this.truck.attach(this.trailer);
        this.trailer.position.set(0, 0, -8.5);
        this.trailer.rotation.set(0, 0, 0);
        this.airLines.visible = true;
        this.shakeCamera(0.18, 0.32);
        if (this.onCouplingLocked) this.onCouplingLocked();
      } else if (b.z < b.targetZ - 0.85) {
        b.z = b.targetZ - 0.85;
        b.speed = 0;
        this.truck.position.z = b.z;
        if (!b.warned) {
          b.warned = true;
          if (this.onBackingWarning) this.onBackingWarning('Off line. Pull forward and square up.');
        }
      }
    } else {
      if (b.z <= b.targetZ && Math.abs(b.x) <= b.tol) {
        b.locked = true;
        b.speed = 0;
        b.z = b.targetZ;
        this.truck.position.z = b.targetZ;
        this.docked = true;
        this.dockOpen = true;
        this.shakeCamera(0.16, 0.3);
        if (this.onDocked) this.onDocked();
      } else if (b.z < b.targetZ - 1.0) {
        b.z = b.targetZ - 1.0;
        b.speed = 0;
        this.truck.position.z = b.z;
        if (!b.warned) {
          b.warned = true;
          if (this.onBackingWarning) this.onBackingWarning('Too far off center. Pull forward and try again.');
        }
      }
    }
  }

  getBackingState() {
    const b = this.backing;
    if (!b) return null;
    return { ...b, aligned: Math.abs(b.x) <= b.tol };
  }

  capturePhoto() {
    this.renderer.render(this.scene, this.camera);
    return this.canvas.toDataURL('image/jpeg', 0.84);
  }

  getScreenPosition(object) {
    const p = object.getWorldPosition(new THREE.Vector3()).project(this.camera);
    return {
      x: (p.x * 0.5 + 0.5),
      y: (-p.y * 0.5 + 0.5),
      z: p.z,
      visible: p.z < 1 && p.z > -1
    };
  }

  setPhotoZoom(value) {
    const [maxR, minR] = this.photoZoomRange || [9, 3.4];
    const r = lerp(maxR, minR, value);
    this.setOrbitRadius(r);
  }

  setHotspots(items) {
    this.clearHotspots();
    const root = document.getElementById('hotspots');
    if (!root) return;
    for (const item of items) {
      const el = document.createElement('button');
      el.className = 'hotspot' + (item.small ? ' small' : '');
      el.type = 'button';
      el.setAttribute('aria-label', item.label);
      el.innerHTML = `<span class="pulse">${item.icon || '!'}</span><span class="tag">${item.label}</span>`;
      el.addEventListener('pointerdown', (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (item.onClick) item.onClick();
      });
      root.appendChild(el);
      const pos = item.position instanceof THREE.Vector3 ? item.position.clone() : new THREE.Vector3(...item.position);
      this.hotspots.push({ id: item.id, el, position: pos });
    }
  }

  clearHotspots() {
    const root = document.getElementById('hotspots');
    if (root) root.innerHTML = '';
    this.hotspots = [];
  }

  markHotspot(id, done = true) {
    const h = this.hotspots.find((x) => x.id === id);
    if (h) h.el.classList.toggle('done', done);
  }

  updateHotspots() {
    if (!this.hotspots.length) return;
    const w = window.innerWidth;
    const h = window.innerHeight;
    for (const spot of this.hotspots) {
      const p = spot.position.clone().project(this.camera);
      const visible = p.z < 1 && p.z > -1 && p.x > -1.25 && p.x < 1.25 && p.y > -1.25 && p.y < 1.25;
      if (!visible) {
        spot.el.style.display = 'none';
        continue;
      }
      spot.el.style.display = 'flex';
      const px = (p.x * 0.5 + 0.5) * w;
      const py = (-p.y * 0.5 + 0.5) * h;
      spot.el.style.left = `${px}px`;
      spot.el.style.top = `${py}px`;
      spot.el.classList.toggle('edge-left', px < 78);
      spot.el.classList.toggle('edge-right', px > w - 78);
      spot.el.classList.toggle('edge-top', py < 120);
    }
  }

  animate() {
    const now = performance.now();
    const dt = Math.min((now - this.lastTime) / 1000, 0.1);
    this.lastTime = now;
    this.elapsed += dt;
    this.updateAnimations(dt);
    this.updateBacking(dt);
    this.updateRoad(dt);
    if (this.mode !== 'road') {
      this.updateCamera(dt);
      this.applyShake(dt);
    }
    this.updateHotspots();
    this.renderer.render(this.scene, this.camera);
  }
}
