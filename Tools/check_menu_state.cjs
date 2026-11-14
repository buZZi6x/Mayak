const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const state = require('../ArtSource/UI/menu-state.js');
const checks = [];
function check(name, run) { run(); checks.push(name); console.log('PASS ' + name); }
const memory = new Map();
const storage = {getItem: key => memory.get(key) || null, setItem: (key, value) => memory.set(key, value)};
check('settings survive save/reload without touching the Unity score key', () => {
  memory.set('Lighthouse.Blockout.Best', '1970');
  assert.equal(state.saveSettings(storage, {...state.defaults, master: 24, invertY: true, glitches: false}).ok, true);
  const restored = state.loadSettings(storage).settings;
  assert.equal(restored.master, 24); assert.equal(restored.invertY, true); assert.equal(restored.glitches, false);
  assert.equal(memory.get('Lighthouse.Blockout.Best'), '1970');
});
check('corrupt and future-version settings fall back without erasing the original', () => {
  for (const raw of ['{broken', 'null', '{"version":7,"values":{}}']) {
    memory.set(state.SETTINGS_KEY, raw);
    const restored = state.loadSettings(storage);
    assert.deepEqual(restored.settings, state.defaults); assert.ok(restored.warning);
    assert.equal(memory.get(state.SETTINGS_KEY), raw);
  }
});
check('invalid values, missing fields and extreme numbers cannot break controls', () => {
  const result = state.normalizeSettings({master: 99999, ambience: -20, sensitivity: Infinity, brightness: 'NaN', resolution: '99999x1', glitches: 'true', fps: 9999});
  assert.equal(result.master, 100); assert.equal(result.ambience, 0);
  assert.equal(result.sensitivity, 1); assert.equal(result.brightness, 100); assert.equal(result.resolution, '1920x1080'); assert.equal(result.fps, 60);
});
check('storage denial is reported and reduced-motion defaults remain usable', () => {
  const denied = {getItem() {throw new Error('denied');}, setItem() {throw new Error('quota');}};
  const restored = state.loadSettings(denied, true);
  assert.equal(restored.settings.glitches, false); assert.ok(restored.warning);
  assert.equal(state.saveSettings(denied, {...state.defaults, master: 0}).ok, false);
});
check('best photos sort by captured price; equal-price order is stable', () => {
  const photos = [{id:'a',price:60,label:'a'},{id:'b',price:300,label:'b'},{id:'c',price:300,label:'c'}];
  const result = state.prepareArchive({best:1000, photos});
  assert.deepEqual(result.photos.map(photo => photo.id), ['b','c','a']);
  assert.equal(result.best, 1000); assert.deepEqual(photos.map(photo => photo.id), ['a','b','c']);
});
check('archive rejects duplicate IDs, zero/invalid prices and unsafe image paths', () => {
  const result = state.prepareArchive({best: -20, photos: [null, {id:'x',label:'x',price:100,image:'https://example.com/tracker.png'}, {id:'x',label:'duplicate',price:200}, {id:'zero',label:'zero',price:0}, {id:'invalid',label:'bad',price:NaN}]});
  assert.equal(result.best, 0); assert.equal(result.photos.length, 1); assert.equal(result.photos[0].image, '');
});
check('top 20 remain bounded and missing images do not discard the best score', () => {
  const photos = Array.from({length:100}, (_,i) => ({id:'p'+i,label:'p'+i,price:i+1}));
  const result = state.prepareArchive({best:9999,photos});
  assert.equal(result.photos.length, 20); assert.equal(result.photos[0].price, 100); assert.equal(result.photos[19].price, 81); assert.equal(result.best, 9999);
  assert.deepEqual(state.prepareArchive({best:50,photos:[]}), {best:50,photos:[]});
});
check('all shipped demo photographs and menu assets exist locally', () => {
  for (const photo of state.demoArchive.photos) assert.ok(fs.existsSync(path.resolve(__dirname, '../ArtSource/UI', photo.image)));
  for (const file of ['menu-simulation.mp4','menu-simulation-poster.jpg','menu-ocean.ogg','menu-shutter.wav','nautical-chart.svg','mayak-wordmark.svg','fonts/CormorantGaramond.ttf','fonts/Commissioner.ttf','fonts/cormorantgaramond-OFL.txt','fonts/commissioner-OFL.txt']) assert.ok(fs.statSync(path.resolve(__dirname,'../ArtSource/UI/assets',file)).size > 0);
  assert.equal(state.prepareArchive(state.demoArchive).photos.reduce((sum,photo) => sum + photo.price,0), state.demoArchive.best);
});
const out = path.resolve(__dirname, '../Artifacts/UI/MainMenu');
fs.mkdirSync(out, {recursive:true});
fs.writeFileSync(path.join(out,'state-checks.json'), JSON.stringify({passed:checks.length,checks},null,2));
console.log(`${checks.length}/${checks.length} menu-state checks passed`);
