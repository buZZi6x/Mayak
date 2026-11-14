(() => {
  'use strict';

  const $ = (id) => document.getElementById(id);
  const icon = (name, className = 'icon') => `<svg class="${className}" aria-hidden="true"><use href="#i-${name}"/></svg>`;
  const escape = (value) => String(value).replace(/[&<>"']/g, char => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[char]));
  const number = (value) => String(value).padStart(2, '0');
  const time = (value) => `${number(Math.floor(value / 60))}:${number(value % 60)}`;
  const photoWord = (value) => value === 1 ? 'кадр' : value >= 2 && value <= 4 ? 'кадра' : 'кадров';

  // A local presentation of existing game rules. The scene and initial values are fixtures,
  // not a connection to Unity. All screenshots are authored assets from this project.
  const scenes = {
    sea: { label: 'Галерея · Море', image: 'assets/sea.png', alt: 'Корабли в ночном океане, освещённые лучом маяка', category: 'ship', subject: 'Корабль', price: 60,
      hotspots: [{id:'ship', x:52, y:49}, {id:'frigate', x:25, y:49}, {id:'steamer', x:70, y:50}, {id:'coast', x:84, y:41}] },
    door: { label: 'Вход в маяк', image: 'assets/door.png', alt: 'Красная дверь у основания маяка', primary: 'door',
      hotspots: [{id:'door', x:44, y:50}, {id:'entrance-light', x:38, y:57}] },
    lamp: { label: 'Лампа маяка', image: 'assets/lamp.png', alt: 'Золотая лампа и линза маяка', primary: 'lamp',
      hotspots: [{id:'lamp', x:50, y:53}, {id:'bed', x:10, y:58}] },
    radio: { label: 'Радио и журнал', image: 'assets/radio.png', alt: 'Бирюзовое радио, журнал вахты и настольная лампа', primary: 'radio',
      hotspots: [{id:'radio', x:50, y:36}, {id:'journal', x:66, y:49}, {id:'desk-light', x:29, y:12}, {id:'telescope', x:75, y:39}, {id:'cup', x:33, y:44}] },
    bed: { label: 'Место для отдыха', image: 'assets/bed.png', alt: 'Кровать смотрителя с синим покрывалом', primary: 'bed',
      hotspots: [{id:'bed', x:50, y:47}, {id:'chart', x:71, y:19}] },
    telescope: { label: 'Телескоп', image: 'assets/telescope.png', alt: 'Латунный телескоп на галерее маяка', primary: 'telescope',
      hotspots: [{id:'telescope', x:50, y:50}] },
  };

  const objects = {
    door: {title:'Дверь маяка', category:'Вход', icon:'door', description:'Проход к винтовой лестнице и рабочим местам смотрителя.', action:true},
    lamp: {title:'Лампа маяка', category:'Обслуживание', icon:'lamp', description:'Частое мигание предупреждает о поломке через 20 секунд. Во время ремонта отсчёт приостановлен.', action:true},
    radio: {title:'Радио', category:'Эфир', icon:'radio', description:'Голоса берега, прогноз погоды и далёкие помехи. Выберите волну или выключите приёмник.', action:true},
    bed: {title:'Кровать', category:'Следующий день', icon:'bed', description:'Лечь и начать следующий день. Остаток топлива переносится, бонус за снимки начисляется после сна.', action:true},
    telescope: {title:'Телескоп', category:'Наблюдение', icon:'telescope', description:'Рассмотрите море вблизи. Для фотографии достаньте камеру клавишей E.', action:true},
    ship: {title:'Барк «Пилигрим»', category:'Морское наблюдение', icon:'ship', description:'Корабль должен быть виден в кадре. Первый снимок корабля — 60 очков, повтор той же категории — половина.', meta:'E — достать камеру'},
    frigate: {title:'Фрегат «Меридиан»', category:'Морское наблюдение', icon:'ship', description:'Парусное судно на отдельном маршруте. За корабли действует общая категория повторов.', meta:'E — достать камеру'},
    steamer: {title:'Пароход «Северный»', category:'Морское наблюдение', icon:'ship', description:'Следите за движением судна. На фотографии учитывается только видимая часть события.', meta:'E — достать камеру'},
    coast: {title:'Дальний берег', category:'Окружение', icon:'eye', description:'Скалистые острова на горизонте. Можно просто остановиться и посмотреть на море.', meta:'Tab — чистый вид'},
    journal: {title:'Журнал вахты', category:'Рабочий стол', icon:'photo', description:'Рабочее место смотрителя рядом с радио. Снимки текущего дня доступны в слотах 1–5.', meta:'1–5 — выбрать фотографию'},
    'desk-light': {title:'Настольная лампа', category:'Освещение', icon:'lamp', description:'Тёплый свет над радио и журналом. Помогает различать рабочее место ночью.', meta:'Q — личный фонарь'},
    'entrance-light': {title:'Входной фонарь', category:'Освещение', icon:'lamp', description:'Освещает дверной проём у подножия маяка.', meta:'Q — личный фонарь'},
    cup: {title:'Кружка смотрителя', category:'Рабочий стол', icon:'moon', description:'Личная вещь на столе у радио. Не требует взаимодействия.', meta:'Предмет окружения'},
    chart: {title:'Карта у кровати', category:'Окружение', icon:'grid', description:'Карта на галерее маяка. Не требует взаимодействия.', meta:'Предмет окружения'},
  };

  const state = {
    slots: [
      {id:1, title:'Корабль в луче маяка', short:'Корабль', image:'assets/sea.png', price:60, sent:true, category:'ship'},
      {id:2, title:'Кракен атакует корабль', short:'Атака кракена', image:'assets/kraken.png', price:300, sent:true, category:'kraken'},
      {id:3, title:'Полярная завеса', short:'Полярная завеса', image:'assets/aurora.png', price:120, sent:true, category:'aurora'},
      null, null,
    ],
    selected:0, equipment:'photo', flashlight:false, clean:false, aiming:false,
    scene:'sea', focus:null, doorOpen:true, day:1, fuel:88, lamp:0,
    score:480, confirmed:480, paidHundreds:0, repeats:{ship:1, kraken:1, aurora:1},
    nextPhotoId:4, busy:null, dragIndex:null, sceneOpen:false, paused:false,
  };
  const tipData = {
    fuel:{title:'Запас топлива', category:'Состояние маяка', icon:'flame', description:'В игре топливо расходуется непрерывно во время вахты. Остаток переносится на следующий день.', footer:'Каждые новые 100 очков: +10 с после сна'},
    score:{title:'Счёт вахты', category:'Фотографии', icon:'spark', description:'Сумма стоимости всех сделанных снимков. Перемещение фотографии между слотами не меняет её цену.', footer:'Бонус топлива начисляется после сна'},
    locations:{title:'Точки осмотра', category:'Веб-макет', icon:'grid', description:'Переключите кадр, чтобы проверить тултипы предметов и действия смотрителя.', footer:'Кадры из текущей игры'},
    torch:{title:'Фонарь', category:'Снаряжение', icon:'flashlight', description:'Личный свет для осмотра тёмных мест. Включается независимо от камеры или фотографии в руке.', key:'Q', footer:'Включить / выключить'},
    camera:{title:'Камера', category:'Снаряжение', icon:'camera', description:'Достаньте камеру, наведите её на море и сделайте снимок. Пять кадров на день, включая пустые.', key:'E', footer:'Достать / убрать · ЛКМ — кадр · ПКМ — зум'},
    clean:{title:'Чистый вид', icon:'eye', description:'Скрывает панели, прицел, предмет в руке и все подсказки. Повторное нажатие возвращает интерфейс.', key:'Tab', footer:'Вернуть UI: Tab или двойной щелчок / тап'},
    held:{title:'Фотография в руке', category:'Просмотр', icon:'photo', description:'Откройте снимок крупно. Переключайте фотографии цифрами 1–5, стрелками или колесом мыши.', key:'ЛКМ', footer:'Рассмотреть · Esc — убрать из руки'},
    close:{title:'Закрыть фотографию', icon:'photo', description:'Вернуться к морю. Снимок останется в руке.', key:'Esc', footer:'Закрыть просмотр'},
    previous:{title:'Предыдущий снимок', icon:'photo', description:'Пустые слоты пропускаются.', key:'←', footer:'Предыдущая фотография'},
    next:{title:'Следующий снимок', icon:'photo', description:'Пустые слоты пропускаются.', key:'→', footer:'Следующая фотография'},
  };

  const occupied = () => state.slots.filter(Boolean).length;
  const unsent = () => state.slots.filter(photo => photo && !photo.sent).length;
  let tooltipTarget = null, tooltipTimer = null, toastTimer = null, wheelUntil = 0, busyTimer = null;
  let returnFocus = null;
  let pausedAt = 0, pausedDuration = 0;
  const previewNow = () => (state.paused ? pausedAt : performance.now()) - pausedDuration;

  function setPaused(value) {
    if (state.paused === value) return;
    if (value) {
      closeInspection(); setLocationMenu(false); finishDrag(); hideTooltip();
      window.keeperRadio?.close();
      pausedAt = performance.now();
    } else pausedDuration += performance.now() - pausedAt;
    state.paused = value;
    $('game').classList.toggle('game-paused', value);
    window.keeperRadio?.setPaused(value);
  }
  window.keeperPreview = Object.freeze({setPaused});

  function notify(message) {
    $('announcer').textContent = message;
    if (state.clean) return;
    clearTimeout(toastTimer);
    $('toast').textContent = message;
    $('toast').hidden = false;
    toastTimer = setTimeout(() => { $('toast').hidden = true; }, 3200);
  }

  function actionInfo(id) {
    if (state.busy) return {label:state.busy.label, duration:'', disabled:true};
    switch (id) {
      case 'door': return {label:state.doorOpen ? 'Закрыть дверь' : 'Открыть дверь', duration:''};
      case 'lamp': return {label:state.lamp <= 0 ? 'Починить маяк' : 'Ремонт не требуется', duration:state.lamp <= 0 ? '6 с' : '', disabled:state.lamp > 0};
      case 'radio': return {label:'Настроить радио', duration:''};
      case 'bed': return {label:'Лечь спать', duration:''};
      case 'telescope': return {label:state.aiming ? 'Отойти от телескопа' : 'Посмотреть в телескоп', duration:''};
      default: return null;
    }
  }

  function readTip(id) {
    if (!id || id === 'object:door') return null;
    if (id.startsWith('object:')) {
      const object = objects[id.slice(7)];
      const action = object.action ? actionInfo(id.slice(7)) : null;
      return {...object, key:action ? 'F' : null, footer:action ? `${action.label}${action.duration ? ' · '+action.duration : ''}` : object.meta};
    }
    return tipData[id];
  }

  function hideTooltip() {
    clearTimeout(tooltipTimer);
    tooltipTimer = null;
    if (tooltipTarget) tooltipTarget.removeAttribute('aria-describedby');
    tooltipTarget = null;
    $('object-card').classList.remove('tooltip-focus');
    if ($('tooltip').matches(':popover-open')) $('tooltip').hidePopover();
    $('tooltip').hidden = true;
  }

  function showTooltip(target) {
    if (state.clean || state.dragIndex !== null || !target?.isConnected) return;
    if ($('photo-dialog').open && !target.closest('#photo-dialog')) return;
    const tip = readTip(target.dataset.tip);
    if (!tip) return;
    hideTooltip();
    tooltipTarget = target;
    // The focused station already has a complete contextual tooltip. Reuse it
    // instead of displaying the same description twice over the scene.
    if (target.dataset.tip === `object:${state.focus}` && !$('object-card').hidden && $('object-card').getClientRects().length) {
      target.setAttribute('aria-describedby','object-card');
      $('object-card').classList.add('tooltip-focus');
      return;
    }
    target.setAttribute('aria-describedby', 'tooltip');
    const tipContainer = $('photo-dialog').open ? $('photo-dialog') : $('game');
    if ($('tooltip').parentElement !== tipContainer) tipContainer.append($('tooltip'));
    $('tooltip').innerHTML = `<div class="tip-heading">${icon(tip.icon || 'eye','tip-icon')}<h3>${escape(tip.title)}</h3></div>${tip.image ? `<img class="tip-image" src="${tip.image}" alt="">` : ''}${tip.description ? `<p>${escape(tip.description)}</p>` : ''}${tip.footer ? `<div class="tip-footer">${tip.key ? `<kbd>${escape(tip.key)}</kbd>` : ''}<span>${escape(tip.footer)}</span></div>` : ''}`;
    $('tooltip').hidden = false;
    $('tooltip').showPopover();
    const anchor = target.getBoundingClientRect();
    const bounds = $('tooltip').getBoundingClientRect();
    const left = Math.max(12, Math.min(innerWidth - bounds.width - 12, anchor.left + anchor.width / 2 - bounds.width / 2));
    const above = anchor.top - bounds.height - 15;
    const top = above > 12 ? above : Math.min(innerHeight - bounds.height - 12, anchor.bottom + 15);
    $('tooltip').style.left = `${left}px`;
    $('tooltip').style.top = `${Math.max(12,top)}px`;
  }

  function renderSlots() {
    hideTooltip();
    const previousFocus = document.activeElement?.closest('[data-slot]')?.dataset.slot;
    $('photo-slots').innerHTML = state.slots.map((photo, index) => {
      tipData[`slot-${index}`] = photo ? {title:photo.title, category:`Фотография ${number(photo.id)} · Слот ${index+1}`, icon:'photo', image:photo.image, description:`${photo.price} очков. ${photo.sent ? 'Штаб принял снимок.' : 'Снимок ещё не отправлен в штаб.'}`, key:String(index+1), footer:'Взять в руку · Двойной щелчок — рассмотреть'} : {title:'Свободный слот', category:`Фотография · Слот ${index+1}`, icon:'camera', description:'Следующий снимок займёт первый свободный слот. Всего пять кадров на день.', key:'E', footer:'Достать камеру → ЛКМ — снять'};
      return `<button class="photo-slot ${photo ? '' : 'empty'} ${state.equipment === 'photo' && state.selected === index ? 'selected' : ''}" data-slot="${index}" data-tip="slot-${index}" draggable="${Boolean(photo)}" aria-label="Слот ${index+1}: ${photo ? escape(photo.title) + ', ' + photo.price + ' очков' : 'свободно'}" aria-pressed="${state.equipment === 'photo' && state.selected === index}" aria-keyshortcuts="${index+1}"><kbd class="slot-number">${index+1}</kbd>${photo ? `<img src="${photo.image}" alt="" draggable="false"><span class="slot-caption"><span class="slot-price">+${photo.price}</span>${photo.sent ? icon('check') : '<span class="pending-dot" aria-label="Не отправлено"></span>'}</span>` : icon('photo','empty-icon')}</button>`;
    }).join('');
    if (previousFocus !== undefined) $('photo-slots').querySelector(`[data-slot="${previousFocus}"]`)?.focus({preventScroll:true});
    $('score-label').textContent = state.score;
    $('exposures-label').textContent = occupied() === 5 ? 'Все кадры использованы' : `Осталось ${5-occupied()} ${photoWord(5-occupied())}`;
  }

  function renderEquipment() {
    const photo = state.slots[state.selected];
    $('held-photo').hidden = state.equipment !== 'photo' || !photo;
    if (photo) {
      $('held-image').src = photo.image;
      $('held-image').alt = photo.title;
      $('held-title').textContent = photo.short;
      $('held-price').textContent = `+${photo.price}`;
      $('held-photo').setAttribute('aria-label',`Рассмотреть: ${photo.title}`);
    }
    $('viewfinder').hidden = state.equipment !== 'camera';
    $('camera-toggle').setAttribute('aria-pressed', String(state.equipment === 'camera'));
    $('camera-label').textContent = state.equipment === 'camera' ? 'Убрать' : 'Достать';
    $('torch-toggle').setAttribute('aria-pressed', String(state.flashlight));
    $('torch-label').textContent = state.flashlight ? 'Выключить' : 'Включить';
    $('game').classList.toggle('torch-on', state.flashlight);
    $('game').classList.toggle('camera-equipped', state.equipment === 'camera');
    $('game').classList.toggle('aiming', state.aiming);
    document.querySelectorAll('[data-slot]').forEach(button => {
      const selected = Number(button.dataset.slot) === state.selected && state.equipment === 'photo';
      button.classList.toggle('selected',selected);
      button.setAttribute('aria-pressed',String(selected));
    });
  }

  function renderStats() {
    $('fuel-label').textContent = time(state.fuel);
    $('score-label').textContent = state.score;
  }

  function renderObject() {
    const object = state.focus && objects[state.focus];
    const show = object?.action && !state.sceneOpen;
    $('game').classList.toggle('has-object',Boolean(show));
    $('object-card').hidden = !show;
    $('object-card').classList.toggle('door-prompt',state.focus === 'door');
    if (!show) return;
    const action = actionInfo(state.focus);
    if (state.focus === 'door') {
      $('object-card').innerHTML = `<button class="object-action" data-action="interact" aria-keyshortcuts="F" aria-disabled="${Boolean(action.disabled)}"><kbd>F</kbd><span>— ${escape(action.label)}</span></button>`;
      return;
    }
    $('object-card').innerHTML = `<div class="object-heading">${icon(object.icon)}<h2>${escape(object.title)}</h2></div><p>${escape(object.description)}</p><button class="object-action" data-action="interact" data-tip="object:${state.focus}" aria-keyshortcuts="F" aria-disabled="${Boolean(action.disabled)}"><kbd>F</kbd><span>${escape(action.label)}</span><small>${escape(action.duration)}</small></button>${state.busy ? '<div class="action-progress" role="progressbar" aria-label="Выполнение действия" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0"><i></i></div>' : ''}`;
  }

  function fitScene() {
    const ratio = 16 / 9;
    const width = Math.max(innerWidth, innerHeight * ratio);
    $('scene-plane').style.width = `${width}px`;
    $('scene-plane').style.height = `${width / ratio}px`;
  }

  function setScene(id) {
    if (!scenes[id] || state.clean) return;
    if (state.busy) { notify('Дождитесь окончания действия.'); return; }
    state.scene = id;
    state.focus = scenes[id].primary || null;
    state.aiming = false;
    setLocationMenu(false);
    const scene = scenes[id];
    $('scene-image').src = scene.image;
    $('scene-image').alt = scene.alt;
    $('location-label').textContent = scene.label;
    $('hotspots').innerHTML = scene.hotspots.map(target => `<button class="hotspot ${objects[target.id].action ? '' : 'noninteractive'}" style="left:${target.x}%;top:${target.y}%" data-object="${target.id}" ${target.id === 'door' ? '' : `data-tip="object:${target.id}"`} aria-label="${escape(objects[target.id].title)}"><span class="hotspot-marker"></span></button>`).join('');
    document.querySelectorAll('[data-scene]').forEach(button => button.setAttribute('aria-current', String(button.dataset.scene === id)));
    hideTooltip();
    renderObject();
    renderEquipment();
  }

  function setLocationMenu(open) {
    state.sceneOpen = open;
    $('location-menu').hidden = !open;
    $('location-toggle').setAttribute('aria-expanded',String(open));
    hideTooltip();
    renderObject();
  }

  function selectSlot(index, {silent = false} = {}) {
    if (state.clean || state.busy) return;
    const photo = state.slots[index];
    if (!photo) {
      if (!silent) notify(`Слот ${index+1} свободен. E — достать камеру, ЛКМ — снять.`);
      return;
    }
    state.selected = index;
    state.equipment = 'photo';
    state.aiming = false;
    hideTooltip();
    renderEquipment();
    if ($('photo-dialog').open) renderInspection();
    $('announcer').textContent = `Слот ${index+1}. ${photo.title}, ${photo.price} очков.`;
  }

  function cyclePhoto(direction) {
    if (state.clean || state.busy) return;
    if (!occupied()) { notify('Пока нет фотографий. E — достать камеру.'); return; }
    for (let step=1; step<=5; step++) {
      const index = (state.selected + direction * step + 5) % 5;
      if (state.slots[index]) { selectSlot(index); return; }
    }
  }

  function swapSlots(from, to) {
    if (state.clean || state.busy || from === to || !state.slots[from]) return;
    const heldId = state.slots[state.selected]?.id;
    [state.slots[from],state.slots[to]] = [state.slots[to],state.slots[from]];
    if (heldId) state.selected = state.slots.findIndex(photo => photo?.id === heldId);
    renderSlots();
    renderEquipment();
    notify(`Фотографии в слотах ${from+1} и ${to+1} поменялись местами.`);
  }

  function toggleCamera() {
    if (state.clean || state.busy) return;
    closeInspection();
    state.equipment = state.equipment === 'camera' ? (state.slots[state.selected] ? 'photo' : 'none') : 'camera';
    state.aiming = false;
    hideTooltip();
    renderEquipment();
  }

  function capture() {
    if (state.clean || state.busy || state.equipment !== 'camera' || $('photo-dialog').open || state.sceneOpen) return;
    const slot = state.slots.findIndex(photo => !photo);
    if (slot < 0) { notify('Все пять кадров использованы. Можно идти спать или продолжить наблюдение.'); return; }
    const scene = scenes[state.scene];
    const repeats = state.repeats[scene.category] || 0;
    const price = scene.price ? Math.floor(scene.price / 2 ** repeats) : 0;
    if (scene.category) state.repeats[scene.category] = repeats + 1;
    state.slots[slot] = {id:state.nextPhotoId++, title:scene.subject || 'Пустой кадр', short:scene.subject || 'Пустой кадр', image:scene.image, price, sent:true, category:scene.category || 'empty'};
    state.selected = slot;
    state.score += price; state.confirmed += price;
    renderSlots();
    renderEquipment();
    renderObject();
    $('flash').classList.remove('expose');
    void $('flash').offsetWidth;
    $('flash').classList.add('expose');
    notify(`Снимок в слоте ${slot+1} · +${price} очков${price === 0 ? ' · Пустой кадр тоже расходует попытку' : ''}`);
  }

  function renderInspection() {
    const photo = state.slots[state.selected];
    if (!photo) return;
    $('inspect-index').textContent = number(photo.id);
    $('inspect-title').textContent = photo.title;
    $('inspect-image').src = photo.image;
    $('inspect-image').alt = photo.title;
    $('inspect-price').textContent = `+${photo.price} очк.`;
    $('inspect-status').textContent = photo.sent ? '✓ Фотография принята штабом' : '○ Ожидает подтверждения';
  }

  function openInspection() {
    if (state.clean || state.busy || state.equipment !== 'photo' || !state.slots[state.selected]) return;
    hideTooltip();
    setLocationMenu(false);
    returnFocus = document.activeElement;
    renderInspection();
    if (!$('photo-dialog').open) $('photo-dialog').showModal();
    $('inspect-close').focus({preventScroll:true});
  }

  function closeInspection() {
    if (!$('photo-dialog').open) return;
    $('photo-dialog').close();
    if (returnFocus?.isConnected) returnFocus.focus({preventScroll:true});
    hideTooltip();
  }

  function toggleClean() {
    closeInspection();
    setLocationMenu(false);
    hideTooltip();
    finishDrag();
    clearTimeout(toastTimer);
    $('toast').hidden = true;
    state.clean = !state.clean;
    $('game').classList.toggle('clean-view',state.clean);
    $('hud').inert = state.clean;
    $('hotspots').inert = state.clean;
    if (state.clean) document.activeElement?.blur();
    $('announcer').textContent = state.clean ? 'Чистый вид. Tab или двойной щелчок, чтобы вернуть интерфейс.' : 'Интерфейс возвращён.';
  }

  function reportPhotos() {
    for (const photo of state.slots) if (photo && !photo.sent) { photo.sent = true; state.confirmed += photo.price; }
  }

  function timedAction(label, seconds, finish, midpoint = null) {
    state.busy = {label, start:previewNow(), duration:seconds*1000, finish, midpoint};
    hideTooltip();
    renderObject();
    busyTimer = setInterval(() => {
      if (state.paused) return;
      const elapsed = previewNow()-state.busy.start;
      if (state.busy.midpoint && elapsed >= 800) {
        const midpoint = state.busy.midpoint;
        state.busy.midpoint = null;
        midpoint();
      }
      const progress = Math.min(1,elapsed/state.busy.duration);
      const bar = $('object-card').querySelector('.action-progress');
      if (bar) { bar.setAttribute('aria-valuenow',String(Math.round(progress*100))); bar.firstElementChild.style.width = `${progress*100}%`; }
      if (progress < 1) return;
      clearInterval(busyTimer);
      busyTimer = null;
      const done = state.busy.finish;
      state.busy = null;
      done();
      renderSlots(); renderEquipment(); renderStats(); renderObject();
    },80);
  }

  function interact(id = state.focus) {
    if (state.clean || state.busy || $('photo-dialog').open || state.sceneOpen || !objects[id]?.action) return;
    state.focus = id;
    const action = actionInfo(id);
    if (action.disabled) { notify(action.label); return; }
    if (id === 'door') {
      state.doorOpen = !state.doorOpen;
      // The review uses a still screenshot; only interaction/UI state changes here.
    } else if (id === 'lamp') {
      timedAction('Ремонт маяка…',6,() => {state.lamp=60; notify('Маяк починен. Топливо осталось прежним.');});
    } else if (id === 'radio') {
      window.keeperRadio.open();
    } else if (id === 'bed') {
      state.equipment = 'none';
      window.keeperRadio.stop();
      $('game').classList.add('sleeping');
      timedAction('Сон…',1.45,() => $('game').classList.remove('sleeping'),() => {
        reportPhotos();
        const hundreds = Math.floor(state.confirmed/100);
        const bonus = Math.max(0,hundreds-state.paidHundreds)*10;
        state.paidHundreds = hundreds;
        state.fuel += bonus;
        state.slots = Array(5).fill(null);
        state.repeats = {};
        state.selected = 0;
        state.day++;
        state.nextPhotoId = 1;
        notify(`День ${state.day}. Остаток топлива сохранён. Бонус: +${bonus} с.`);
        renderSlots(); renderEquipment(); renderStats();
      });
    } else if (id === 'telescope') {
      state.aiming = !state.aiming;
      state.equipment = 'none';
    }
    hideTooltip();
    renderObject();
    renderEquipment();
  }

  function finishDrag() {
    state.dragIndex = null;
    document.querySelectorAll('.dragging,.drop-target').forEach(element => element.classList.remove('dragging','drop-target'));
  }

  $('location-menu').innerHTML = '<div class="menu-heading">Точки осмотра</div>' + Object.entries(scenes).map(([id,scene]) => `<button class="location-option" data-scene="${id}" aria-current="${id === state.scene}" data-tip="${scene.primary ? 'object:'+scene.primary : 'locations'}"><img src="${scene.image}" alt=""><span>${escape(scene.label)}</span>${icon('chevron')}</button>`).join('');
  $('location-toggle').addEventListener('click', () => setLocationMenu(!state.sceneOpen));
  $('location-menu').addEventListener('click', event => { const target = event.target.closest('[data-scene]'); if(target) setScene(target.dataset.scene); });
  $('torch-toggle').addEventListener('click',() => {if(!state.clean) {state.flashlight=!state.flashlight; hideTooltip(); renderEquipment();}});
  $('camera-toggle').addEventListener('click',toggleCamera);
  $('clean-toggle').addEventListener('click',toggleClean);
  $('held-photo').addEventListener('click',openInspection);
  $('inspect-close').addEventListener('click',closeInspection);
  $('inspect-prev').addEventListener('click',() => cyclePhoto(-1));
  $('inspect-next').addEventListener('click',() => cyclePhoto(1));
  $('photo-dialog').addEventListener('cancel',event => {event.preventDefault(); closeInspection();});
  $('photo-dialog').addEventListener('click',event => {if(event.target === $('photo-dialog')) closeInspection();});
  $('photo-slots').addEventListener('click',event => {const slot=event.target.closest('[data-slot]'); if(slot) selectSlot(Number(slot.dataset.slot));});
  $('photo-slots').addEventListener('dblclick',event => {const slot=event.target.closest('[data-slot]'); if(slot && state.slots[Number(slot.dataset.slot)]) openInspection();});
  $('object-card').addEventListener('click',event => {if(event.target.closest('[data-action]')) interact();});
  $('hotspots').addEventListener('click',event => {
    const target=event.target.closest('[data-object]');
    if(!target || state.clean || state.busy) return;
    state.focus=objects[target.dataset.object].action ? target.dataset.object : null;
    renderObject();
    showTooltip(target);
  });
  $('hotspots').addEventListener('focusin',event => {
    const target=event.target.closest('[data-object]');
    if(target && !state.busy) {state.focus=objects[target.dataset.object].action ? target.dataset.object : null; renderObject();}
  });
  $('world').addEventListener('click',event => {
    if(event.target.closest('button') || state.clean) return;
    if(state.sceneOpen) {setLocationMenu(false); return;}
    if(state.equipment === 'camera') capture();
    else if(state.equipment === 'photo') openInspection();
  });
  $('world').addEventListener('dblclick',() => {if(state.clean) toggleClean();});
  $('world').addEventListener('contextmenu',event => event.preventDefault());
  $('world').addEventListener('pointerdown',event => {
    if(event.button === 2 && state.equipment === 'camera' && !state.clean && !state.busy) {state.aiming=true; renderEquipment();}
  });
  window.addEventListener('pointerup',event => {if(event.button === 2 && state.equipment === 'camera') {state.aiming=false; renderEquipment();}});
  window.addEventListener('blur',() => {if(state.equipment === 'camera') {state.aiming=false; renderEquipment();} hideTooltip(); finishDrag();});
  window.addEventListener('pointermove',event => {
    if(state.paused) return;
    if(state.flashlight && !state.clean) {
      $('torch-light').style.setProperty('--torch-x',`${event.clientX}px`);
      $('torch-light').style.setProperty('--torch-y',`${event.clientY}px`);
    }
  });
  document.addEventListener('click',event => {if(state.sceneOpen && !event.target.closest('.location-picker')) setLocationMenu(false);});

  document.addEventListener('pointerover',event => {
    if(event.pointerType === 'touch') return;
    const target=event.target.closest('[data-tip]');
    if(!target || target.contains(event.relatedTarget)) return;
    hideTooltip();
    tooltipTimer=setTimeout(() => showTooltip(target),180);
  });
  document.addEventListener('pointerout',event => {
    const target=event.target.closest('[data-tip]');
    if(target && !target.contains(event.relatedTarget)) hideTooltip();
  });
  document.addEventListener('focusin',event => {const target=event.target.closest('[data-tip]'); if(target) showTooltip(target);});
  document.addEventListener('focusout',hideTooltip);

  $('photo-slots').addEventListener('dragstart',event => {
    const target=event.target.closest('[data-slot]');
    if(!target || state.clean || state.busy) {event.preventDefault(); return;}
    state.dragIndex=Number(target.dataset.slot);
    if(!state.slots[state.dragIndex]) {event.preventDefault(); finishDrag(); return;}
    event.dataTransfer.setData('text/plain',String(state.dragIndex));
    event.dataTransfer.effectAllowed='move';
    target.classList.add('dragging');
    hideTooltip();
  });
  $('photo-slots').addEventListener('dragover',event => {
    if(state.dragIndex === null) return;
    const target=event.target.closest('[data-slot]');
    if(!target) return;
    event.preventDefault();
    event.dataTransfer.dropEffect='move';
    document.querySelectorAll('.drop-target').forEach(element => element.classList.remove('drop-target'));
    target.classList.add('drop-target');
  });
  $('photo-slots').addEventListener('dragleave',event => {
    const target=event.target.closest('[data-slot]');
    if(target && !target.contains(event.relatedTarget)) target.classList.remove('drop-target');
  });
  $('photo-slots').addEventListener('drop',event => {
    const target=event.target.closest('[data-slot]');
    if(state.dragIndex === null || !target) return;
    event.preventDefault();
    swapSlots(state.dragIndex,Number(target.dataset.slot));
    finishDrag();
  });
  document.addEventListener('dragend',finishDrag);

  window.addEventListener('wheel',event => {
    if(state.paused || state.clean || state.sceneOpen || state.busy) return;
    const onPhotos=event.target.closest('.photo-inventory,.held-photo,.photo-dialog');
    if(!onPhotos && state.equipment !== 'photo') return;
    if(Math.abs(event.deltaY) < 4) return;
    event.preventDefault();
    if(performance.now() < wheelUntil) return;
    wheelUntil=performance.now()+220;
    cyclePhoto(event.deltaY>0 ? 1 : -1);
  },{passive:false});

  document.addEventListener('keydown',event => {
    if(state.paused) return;
    if(event.ctrlKey || event.altKey || event.metaKey || event.isComposing || event.target.matches('input,textarea,select,[contenteditable="true"]')) return;
    if(event.code === 'Tab' && !event.shiftKey) {
      event.preventDefault();
      if(!event.repeat) toggleClean();
      return;
    }
    if(state.clean) return;
    if(event.code === 'Escape') {
      event.preventDefault();
      if(event.repeat) return;
      if(state.dragIndex !== null) {finishDrag(); return;}
      if($('photo-dialog').open) {closeInspection(); return;}
      if(state.sceneOpen) {setLocationMenu(false); $('location-toggle').focus(); return;}
      hideTooltip();
      state.equipment='none'; state.aiming=false; renderEquipment();
      return;
    }
    if(event.code === 'ArrowLeft' || event.code === 'ArrowRight') {
      const direction=event.code === 'ArrowRight' ? 1 : -1;
      const slot=event.target.closest('[data-slot]');
      if(slot && event.shiftKey) {
        event.preventDefault();
        const index=Number(slot.dataset.slot);
        swapSlots(index,(index+direction+5)%5);
        $('photo-slots').querySelector(`[data-slot="${(index+direction+5)%5}"]`)?.focus();
      } else if($('photo-dialog').open || slot || state.equipment === 'photo') {event.preventDefault(); cyclePhoto(direction);}
      return;
    }
    if(event.repeat || event.shiftKey) return;
    const digit=/^(Digit|Numpad)([1-5])$/.exec(event.code);
    if(digit) {event.preventDefault(); selectSlot(Number(digit[2])-1); return;}
    if($('photo-dialog').open) return;
    if(event.code === 'KeyQ') {event.preventDefault(); state.flashlight=!state.flashlight; hideTooltip(); renderEquipment();}
    if(event.code === 'KeyE') {event.preventDefault(); toggleCamera();}
    if(event.code === 'KeyF') {event.preventDefault(); interact();}
  });

  window.addEventListener('resize',() => {fitScene(); hideTooltip();});
  // Authoring navigation stays outside the normal HUD. Direct scene links let
  // the owner review an object without restoring decorative HUD labels.
  const preview = new URLSearchParams(location.search);
  $('review-locations').hidden = preview.get('review') !== '1';
  fitScene(); renderSlots(); renderEquipment(); renderStats();
  setScene(scenes[preview.get('scene')] ? preview.get('scene') : 'sea');
})();
