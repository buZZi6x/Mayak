(() => {
  'use strict';
  const dialog = document.getElementById('pause-dialog');
  const game = document.getElementById('game');
  const settings = document.getElementById('pause-settings');
  const frame = document.getElementById('pause-settings-frame');
  let returnFocus = null;

  function showSheet() {
    frame.hidden = true;
    frame.removeAttribute('src');
    dialog.classList.remove('is-settings');
    settings.focus({preventScroll: true});
  }

  function pause() {
    if (dialog.open || document.getElementById('end-dialog')?.open) return;
    returnFocus = document.activeElement;
    window.keeperPreview.setPaused(true);
    game.inert = true;
    dialog.showModal();
    showSheet();
  }

  function resume() {
    if (!dialog.open) return;
    frame.removeAttribute('src');
    frame.hidden = true;
    dialog.classList.remove('is-settings');
    dialog.close();
    game.inert = false;
    window.keeperPreview.setPaused(false);
    if (returnFocus?.isConnected && !returnFocus.closest('[inert]')) returnFocus.focus({preventScroll: true});
  }

  // Capture Escape before the HUD/radio so it cannot change equipment or leak
  // into gameplay on the same key press. Key repeat must not toggle the pause.
  window.addEventListener('keydown', event => {
    if (event.code !== 'Escape' || event.ctrlKey || event.altKey || event.metaKey) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    if (!event.repeat) { if (dialog.open) resume(); else pause(); }
  }, true);
  dialog.addEventListener('cancel', event => { event.preventDefault(); resume(); });
  dialog.addEventListener('keydown', event => {
    if (!['ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key) || dialog.classList.contains('is-settings')) return;
    event.preventDefault();
    const buttons = [...dialog.querySelectorAll('.pause-actions button')];
    const index = buttons.indexOf(document.activeElement);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : (index + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
    buttons[next].focus();
  });
  settings.addEventListener('click', () => {
    dialog.classList.add('is-settings');
    frame.hidden = false;
    frame.src = 'menu.html?context=pause#settings';
  });
  document.getElementById('pause-exit').addEventListener('click', () => {
    window.keeperRadio?.stop();
    if (parent !== window) parent.postMessage({type: 'keeper-return-menu'}, location.origin);
    else location.assign('menu.html');
  });
  window.addEventListener('message', event => {
    if (event.origin !== location.origin || event.source !== frame.contentWindow || !dialog.open) return;
    if (event.data?.type === 'keeper-settings-close') { if (event.data.resume) resume(); else showSheet(); }
    if (event.data?.type === 'keeper-settings-preview') game.style.filter = `brightness(${event.data.brightness / 100})`;
  });
  if (new URLSearchParams(location.search).get('pause') === '1') pause();
})();
