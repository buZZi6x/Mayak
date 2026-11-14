(() => {
  'use strict';

  // Review-only entry points; the actual game continues to own EndRun and Best.
  // F1 returns to the existing HUD demo. No run/archive state is persisted here.
  const query = new URLSearchParams(location.search);
  const reasons = {
    fuel: 'Топливо кончилось. Маяк погас.',
    lamp: 'Не успели обслужить лампу.',
    gaze: 'Она заметила твой взгляд.',
  };
  const reason = query.get('gameover');
  if (!Object.hasOwn(reasons, reason)) return;

  const dialog = document.getElementById('end-dialog');
  const game = document.getElementById('game');
  const restart = document.getElementById('end-restart');
  const menu = document.getElementById('end-menu');
  const scoreValue = (name, fallback) => {
    const raw = query.get(name);
    return raw !== null && /^\d{1,10}$/.test(raw) ? Math.min(2147483647, Number(raw)) : fallback;
  };
  const score = scoreValue('score', window.keeperPreview.snapshot().score);
  const best = Math.max(score, scoreValue('best', 1260));
  const format = new Intl.NumberFormat('ru-RU');
  let leaving = false;

  document.getElementById('end-reason').textContent = reasons[reason];
  document.getElementById('end-score').textContent = format.format(score);
  document.getElementById('end-record').textContent = format.format(best);
  dialog.querySelector('.end-results').dataset.digits = best >= 100000000 ? 'maximum' : best >= 100000 ? 'long' : 'normal';

  function leave(toMenu) {
    if (leaving) return;
    leaving = true;
    window.keeperRadio?.stop();
    if (toMenu && parent !== window) parent.postMessage({type: 'keeper-return-menu'}, location.origin);
    else location.assign(toMenu ? 'menu.html' : 'index.html');
  }

  restart.addEventListener('click', () => leave(false));
  menu.addEventListener('click', () => leave(true));
  dialog.addEventListener('cancel', event => { event.preventDefault(); leave(true); });
  // Registered before pause.js: Escape goes to the menu and cannot open pause.
  // Tab stays between the two actions; Enter/Space retain native activation.
  window.addEventListener('keydown', event => {
    if (!dialog.open || event.ctrlKey || event.altKey || event.metaKey) return;
    event.stopImmediatePropagation();
    if (event.code === 'Tab') {
      event.preventDefault();
      const target = document.activeElement === restart ? menu : document.activeElement === menu ? restart : event.shiftKey ? menu : restart;
      target.focus({preventScroll: true});
    } else if (event.code === 'F1' || event.code === 'Escape') {
      event.preventDefault();
      if (!event.repeat) leave(event.code === 'Escape');
    } else if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.code)) {
      event.preventDefault();
      const target = event.code === 'Home' ? restart : event.code === 'End' ? menu : document.activeElement === restart ? menu : restart;
      target.focus({preventScroll: true});
    }
  }, true);

  window.keeperPreview.setPaused(true);
  game.classList.add('run-ended');
  game.inert = true;
  dialog.showModal();
  // Read the outcome before moving to the actions with Tab or arrow keys.
  document.getElementById('end-title').focus({preventScroll: true});
})();
