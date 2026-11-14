/* HTML approval state only. These keys never touch Unity's player saves. */
(function (root) {
  'use strict';
  const SETTINGS_KEY = 'lighthouse.menu.prototype.settings.v1';
  const defaults = Object.freeze({master: 80, ambience: 75, effects: 80, display: 'borderless', resolution: '1920x1080', vsync: true, fps: 60, brightness: 100, glitches: true, sensitivity: 1, invertY: false});
  const numeric = {master: [0, 100, 1], ambience: [0, 100, 1], effects: [0, 100, 1], brightness: [70, 130, 1], sensitivity: [.2, 3, .1]};
  const choices = {display: ['borderless', 'fullscreen', 'windowed'], resolution: ['1280x720', '1920x1080', '2560x1440', '3840x2160'], fps: [0, 30, 60, 120, 144]};
  const clone = value => JSON.parse(JSON.stringify(value));
  function normalizeSettings(input, base = defaults) {
    const result = {...defaults, ...base};
    if (!input || typeof input !== 'object' || Array.isArray(input)) return result;
    for (const [key, [min, max, step]] of Object.entries(numeric)) {
      const value = input[key];
      if (typeof value === 'number' && Number.isFinite(value)) result[key] = Number((Math.round(Math.max(min, Math.min(max, value)) / step) * step).toFixed(2));
    }
    for (const [key, options] of Object.entries(choices)) if (options.includes(input[key])) result[key] = input[key];
    for (const key of ['vsync', 'glitches', 'invertY']) if (typeof input[key] === 'boolean') result[key] = input[key];
    return result;
  }
  function loadSettings(storage, reducedMotion = false) {
    const base = {...defaults, glitches: !reducedMotion};
    try {
      const raw = storage.getItem(SETTINGS_KEY);
      if (!raw) return {settings: base, warning: ''};
      const saved = JSON.parse(raw);
      if (saved.version !== 1 || !saved.values || typeof saved.values !== 'object') throw new Error('schema');
      return {settings: normalizeSettings(saved.values, base), warning: ''};
    } catch { return {settings: base, warning: 'Не удалось прочитать настройки. Использованы значения по умолчанию.'}; }
  }
  function saveSettings(storage, values) {
    const settings = normalizeSettings(values);
    try {
      storage.setItem(SETTINGS_KEY, JSON.stringify({version: 1, values: settings}));
      return {ok: true, settings};
    } catch { return {ok: false, settings, warning: 'Не удалось сохранить настройки. Изменения действуют до закрытия макета.'}; }
  }
  function equalSettings(a, b) { return Object.keys(defaults).every(key => a[key] === b[key]); }
  function prepareArchive(value) {
    const seen = new Set();
    const photos = [];
    for (const item of Array.isArray(value?.photos) ? value.photos : []) {
      if (!item || typeof item.id !== 'string' || !item.id || seen.has(item.id) || !Number.isSafeInteger(item.price) || item.price <= 0 || typeof item.label !== 'string') continue;
      seen.add(item.id);
      const image = typeof item.image === 'string' && /^assets\/[a-z0-9_-]+\.png$/i.test(item.image) ? item.image : '';
      photos.push({id: item.id, label: item.label.slice(0, 120), price: item.price, image, subjects: typeof item.subjects === 'string' ? item.subjects.slice(0, 300) : ''});
    }
    // Stable sort retains the original capture order when prices tie.
    photos.sort((a, b) => b.price - a.price);
    return {best: Number.isSafeInteger(value?.best) && value.best >= 0 ? value.best : 0, photos: photos.slice(0, 20)};
  }
  const demoArchive = Object.freeze({best: 480, photos: [
    {id: 'demo-ship', label: 'Корабль в луче маяка', price: 60, image: 'assets/sea.png', subjects: 'Корабль · 60 очков'},
    {id: 'demo-kraken', label: 'Кракен атакует корабль', price: 300, image: 'assets/kraken.png', subjects: 'Нападение кракена · 300 очков'},
    {id: 'demo-aurora', label: 'Полярная завеса', price: 120, image: 'assets/aurora.png', subjects: 'Полярная завеса · 120 очков'}
  ]});
  const api = {SETTINGS_KEY, defaults, clone, normalizeSettings, loadSettings, saveSettings, equalSettings, prepareArchive, demoArchive};
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.MenuState = Object.freeze(api);
})(typeof window !== 'undefined' ? window : globalThis);
