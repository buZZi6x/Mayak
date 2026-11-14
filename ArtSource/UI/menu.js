(function () {
  'use strict';
  const state = MenuState;
  const $ = id => document.getElementById(id);
  const format = value => value.toLocaleString('ru-RU');
  const query = new URLSearchParams(location.search);
  const pauseContext = query.get('context') === 'pause' && parent !== window;
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let storage;
  try { storage = localStorage; } catch { storage = {getItem() { throw new Error('unavailable'); }, setItem() { throw new Error('unavailable'); }}; }
  const loaded = state.loadSettings(storage, reducedMotion);
  let saved = loaded.settings, draft = {...saved}, screen = '', currentPhoto = 0, photoOpener = null;
  let emptyArchive = query.get('archive') === 'empty';
  let archive = state.prepareArchive(emptyArchive ? {best: 0, photos: []} : state.demoArchive);
  let toastTimer, tipTimer, tipTarget, audioReady = false;
  const seaAudio = $('menu-waves');
  const musicAudio = $('menu-music');
  const effectAudio = $('menu-effects');
  seaAudio.loop = true; seaAudio.preload = 'none'; effectAudio.preload = 'none';
  const form = $('settings-form');
  const instruments = MenuControls.enhance(form);
  const controls = Array.from(form.querySelectorAll('input[name],select[name]'));
  const screens = ['home','settings','gallery','play','exit'];
  const tabs = Array.from(document.querySelectorAll('[data-tab]'));
  function audioLevels(values) {
    // Menu-only mix: 0.12 / 0.45 = -11.48 dB relative to the previous version.
    seaAudio.volume = values.master / 100 * values.ambience / 100 * .12;
    musicAudio.volume = values.master / 100 * values.ambience / 100 * .35;
    effectAudio.volume = values.master / 100 * values.effects / 100 * .55;
  }
  function syncAudio() {
    if (!audioReady) return;
    for (const audio of [seaAudio, musicAudio]) {
      if (pauseContext || document.hidden || screen === 'play' || screen === 'exit') audio.pause();
      else audio.play().catch(() => {});
    }
  }
  function unlockAudio() { audioReady = true; syncAudio(); }
  document.addEventListener('pointerdown', unlockAudio, {once: true});
  document.addEventListener('keydown', unlockAudio, {once: true});
  document.addEventListener('visibilitychange', syncAudio);
  addEventListener('pagehide', () => {seaAudio.pause(); musicAudio.pause(); effectAudio.pause();});
  function previewSettings(values) { MenuSea.configure(values); audioLevels(values); }
  function notify(text) { clearTimeout(toastTimer); $('toast').textContent = text; $('toast').hidden = false; toastTimer = setTimeout(() => {$('toast').hidden = true;}, 4400); }
  function hideTip() { clearTimeout(tipTimer); $('tooltip').hidden = true; if (tipTarget) tipTarget.removeAttribute('aria-describedby'); tipTarget = null; }
  function showTip(target) {
    hideTip(); tipTarget = target;
    tipTimer = setTimeout(() => {
      if (!target.isConnected || target.closest('[hidden]')) return;
      const tip = $('tooltip'); tip.textContent = target.dataset.tip; tip.hidden = false;
      // A dialog is in the top layer: place its tooltip there as well.
      (target.closest('dialog') || document.body).append(tip);
      const rect = target.getBoundingClientRect(), box = tip.getBoundingClientRect();
      const left = Math.min(innerWidth - box.width - 10, Math.max(10, rect.left + rect.width / 2 - box.width / 2));
      const top = rect.top > box.height + 16 ? rect.top - box.height - 10 : rect.bottom + 10;
      tip.style.left = left + 'px'; tip.style.top = Math.min(innerHeight - box.height - 10, Math.max(10, top)) + 'px';
      target.setAttribute('aria-describedby', 'tooltip');
    }, 350);
  }
  document.addEventListener('pointerover', event => {const target = event.target.closest('[data-tip]'); if (target && target !== tipTarget) showTip(target);});
  document.addEventListener('pointerout', event => {if (event.target.closest('[data-tip]') && !event.target.closest('[data-tip]').contains(event.relatedTarget)) hideTip();});
  document.addEventListener('focusin', event => {if (event.target.matches('[data-tip]')) showTip(event.target);});
  document.addEventListener('focusout', hideTip);
  addEventListener('resize', hideTip);
  function renderSettings() {
    for (const control of controls) {
      const value = draft[control.name];
      if (control.type === 'checkbox') { control.checked = value; document.querySelector(`[data-value="${control.name}"]`).textContent = value ? 'Вкл.' : 'Выкл.'; }
      else control.value = String(value);
      if (control.type === 'range') {
        control.style.setProperty('--range', ((value - Number(control.min)) / (Number(control.max) - Number(control.min)) * 100) + '%');
        const label = control.name === 'sensitivity' ? value.toFixed(1).replace('.', ',') + '×' : value + '%';
        form.querySelector(`output[for="${control.id}"]`).textContent = label;
        control.setAttribute('aria-valuetext', label);
      }
    }
    $('fps').disabled = draft.vsync;
    instruments.sync();
    $('apply-settings').disabled = state.equalSettings(saved, draft);
    $('settings-status').textContent = state.equalSettings(saved, draft) ? '' : 'Есть неприменённые изменения';
  }
  controls.forEach(control => control.addEventListener('input', () => {
    draft[control.name] = control.type === 'checkbox' ? control.checked : control.type === 'range' || control.name === 'fps' ? Number(control.value) : control.value;
    draft = state.normalizeSettings(draft); renderSettings(); previewSettings(draft);
  }));
  form.addEventListener('submit', event => {
    event.preventDefault();
    const result = state.saveSettings(storage, draft); saved = result.settings; draft = {...saved};
    renderSettings(); previewSettings(saved);
    $('settings-status').textContent = result.ok ? 'Настройки сохранены' : 'Сохранение недоступно';
    if (!result.ok) notify(result.warning);
  });
  $('reset-settings').addEventListener('click', () => { draft = {...state.defaults, glitches: !reducedMotion}; renderSettings(); previewSettings(draft); });
  $('test-audio').addEventListener('click', () => { unlockAudio(); effectAudio.currentTime = 0; effectAudio.play().catch(() => notify('Браузер не разрешил воспроизведение звука.')); });
  function selectTab(name, focus = false) {
    form.dataset.section = name;
    instruments.close();
    tabs.forEach(tab => {
      const selected = tab.dataset.tab === name;
      tab.setAttribute('aria-selected', String(selected)); tab.tabIndex = selected ? 0 : -1;
      $('panel-' + tab.dataset.tab).hidden = !selected;
      if (selected && focus) tab.focus();
    });
    hideTip();
  }
  tabs.forEach(tab => {
    tab.addEventListener('click', () => selectTab(tab.dataset.tab));
    tab.addEventListener('keydown', event => {
      if (!['ArrowDown','ArrowUp','ArrowLeft','ArrowRight','Home','End'].includes(event.key)) return;
      event.preventDefault();
      const index = tabs.indexOf(tab), next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (index + (['ArrowDown','ArrowRight'].includes(event.key) ? 1 : -1) + tabs.length) % tabs.length;
      selectTab(tabs[next].dataset.tab, true);
    });
  });
  function renderGallery() {
    const grid = $('photo-grid'); grid.replaceChildren();
    $('best-score').replaceChildren(document.createTextNode(format(archive.best) + ' '));
    const unit = document.createElement('small'); unit.textContent = 'очков'; $('best-score').append(unit);
    $('empty-archive').hidden = archive.photos.length > 0;
    grid.hidden = !archive.photos.length;
    $('archive-note').textContent = emptyArchive ? 'Новая игра · пустой архив' : 'Демонстрационные данные';
    archive.photos.forEach((photo, index) => {
      const card = document.createElement('button'); card.type = 'button'; card.className = 'photo-card'; card.dataset.photo = String(index);
      card.setAttribute('aria-label', `${photo.label}, ${photo.price} очков. Рассмотреть снимок`);
      card.dataset.tip = 'Рассмотреть снимок';
      const imageWrap = document.createElement('div'); imageWrap.className = 'card-image';
      const missing = document.createElement('span'); missing.className = 'card-missing'; missing.textContent = 'Изображение недоступно';
      if (photo.image) {
        const img = document.createElement('img'); img.src = photo.image; img.alt = photo.label; img.draggable = false;
        img.addEventListener('error', () => {img.remove(); imageWrap.prepend(missing);}); imageWrap.append(img);
      } else imageWrap.append(missing);
      const rank = document.createElement('span'); rank.className = 'card-rank'; rank.textContent = String(index + 1).padStart(2, '0'); imageWrap.append(rank);
      const caption = document.createElement('div'); caption.className = 'card-caption';
      const label = document.createElement('h3'); label.textContent = photo.label;
      const price = document.createElement('strong'); price.textContent = format(photo.price) + ' ';
      const suffix = document.createElement('small'); suffix.textContent = 'очков'; price.append(suffix); caption.append(label, price);
      card.append(imageWrap, caption); card.addEventListener('click', () => {photoOpener = card; openPhoto(index);}); grid.append(card);
    });
  }
  function openPhoto(index) {
    if (!archive.photos.length) return;
    currentPhoto = (index + archive.photos.length) % archive.photos.length;
    const photo = archive.photos[currentPhoto];
    $('viewer-title').textContent = photo.label; $('viewer-subjects').textContent = photo.subjects;
    $('viewer-price').replaceChildren(document.createTextNode(format(photo.price) + ' '));
    const unit = document.createElement('small'); unit.textContent = 'очков'; $('viewer-price').append(unit);
    $('viewer-position').textContent = `${currentPhoto + 1} / ${archive.photos.length}`;
    $('viewer-image').alt = photo.label; $('viewer-image').hidden = !photo.image; $('viewer-missing').hidden = !!photo.image;
    if (photo.image) $('viewer-image').src = photo.image; else $('viewer-image').removeAttribute('src');
    $('previous-photo').disabled = $('next-photo').disabled = archive.photos.length < 2;
    hideTip(); if (!$('photo-viewer').open) $('photo-viewer').showModal();
    $('viewer-image').classList.remove('exposing');
    void $('viewer-image').offsetWidth;
    $('viewer-image').classList.add('exposing');
  }
  $('viewer-image').addEventListener('error', () => {$('viewer-image').hidden = true; $('viewer-missing').hidden = false;});
  function closePhoto() { hideTip(); $('photo-viewer').close(); if (photoOpener?.isConnected) photoOpener.focus({preventScroll: true}); }
  $('close-viewer').addEventListener('click', closePhoto);
  $('photo-viewer').addEventListener('cancel', event => {event.preventDefault(); closePhoto();});
  $('previous-photo').addEventListener('click', () => openPhoto(currentPhoto - 1));
  $('next-photo').addEventListener('click', () => openPhoto(currentPhoto + 1));
  function closePauseSettings(resume) {
    instruments.close(); draft = {...saved}; previewSettings(saved); effectAudio.pause();
    parent.postMessage({type:'keeper-settings-close', resume}, location.origin);
  }
  function navigate(target) {
    if (pauseContext) { if (target === 'home') closePauseSettings(false); return; }
    if (target !== screen) location.hash = target === 'home' ? '' : target;
  }
  function route() {
    const requested = location.hash.slice(1), next = pauseContext ? 'settings' : screens.includes(requested) ? requested : 'home';
    if (screen === next) return;
    instruments.close();
    const previous = screen;
    if (screen === 'settings') { draft = {...saved}; previewSettings(saved); }
    if ($('photo-viewer').open) closePhoto();
    if ($('quit-dialog').open) $('quit-dialog').close();
    hideTip();
    screen = next; document.body.dataset.screen = screen;
    screens.forEach(id => {$(id).hidden = id !== screen;});
    if (screen === 'settings') {draft = {...saved}; renderSettings(); selectTab('audio');}
    if (screen === 'play') $('game-preview').src = 'index.html?scene=sea';
    else if (previous === 'play') $('game-preview').removeAttribute('src');
    MenuSea.setActive(screen !== 'play' && screen !== 'exit');
    syncAudio();
    const focus = screen === 'home' ? document.querySelector(previous === 'settings' || previous === 'gallery' ? `[data-route="${previous}"]` : '.play-button') : $(screen).querySelector('h2[tabindex]') || $(screen).querySelector('button');
    if (previous) focus?.focus({preventScroll: true});
    $(screen).scrollTop = 0;
  }
  document.querySelectorAll('[data-route]').forEach(button => button.addEventListener('click', () => navigate(button.dataset.route)));
  addEventListener('hashchange', route);
  addEventListener('message', event => {
    if (event.origin === location.origin && event.source === $('game-preview').contentWindow && event.data?.type === 'keeper-return-menu') navigate('home');
  });
  if (pauseContext) addEventListener('keydown', event => {
    if (event.code !== 'Escape') return;
    event.preventDefault(); event.stopImmediatePropagation();
    if (!event.repeat) closePauseSettings(true);
  }, true);
  $('quit-button').addEventListener('click', () => {$('quit-dialog').showModal(); $('cancel-quit').focus();});
  $('cancel-quit').addEventListener('click', () => $('quit-dialog').close());
  $('confirm-quit').addEventListener('click', () => {$('quit-dialog').close(); navigate('exit');});
  document.addEventListener('keydown', event => {
    if (event.defaultPrevented) return;
    if ($('photo-viewer').open) {
      if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {event.preventDefault(); openPhoto(currentPhoto + (event.key === 'ArrowLeft' ? -1 : 1));}
      return;
    }
    if ($('quit-dialog').open) return;
    if (event.key === 'Escape' && screen !== 'home') {event.preventDefault(); navigate('home'); return;}
    if (screen !== 'home' || !['ArrowUp','ArrowDown','Home','End'].includes(event.key)) return;
    event.preventDefault();
    const buttons = Array.from(document.querySelectorAll('.main-nav button'));
    const selected = buttons.indexOf(document.activeElement);
    let next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : selected < 0 ? 0 : (selected + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
    buttons[next].focus();
  });
  $('review-controls').hidden = query.get('review') !== '1';
  $('review-glitch').addEventListener('click', () => {if (!MenuSea.previewGlitch()) notify('Сбои фона отключены в настройках или системой уменьшения движения.');});
  $('review-empty').addEventListener('click', () => {
    emptyArchive = !emptyArchive; archive = state.prepareArchive(emptyArchive ? {best: 0, photos: []} : state.demoArchive); renderGallery();
    $('review-empty').textContent = emptyArchive ? 'Заполненный архив' : 'Пустой архив'; navigate('gallery');
  });
  previewSettings(saved); renderGallery(); route();
  if (loaded.warning) notify(loaded.warning);
})();
