/* Small, keyboard-operable instruments over the existing settings inputs. */
(function () {
  'use strict';
  window.MenuControls = Object.freeze({
    enhance(form) {
      const selectors = [];
      let open = null;
      function close() {
        if (!open) return;
        open.list.hidden = true;
        open.button.setAttribute('aria-expanded', 'false');
        open.button.removeAttribute('aria-activedescendant');
        open.shell.classList.remove('is-open');
        open = null;
      }
      form.querySelectorAll('input[type="range"]').forEach(input => {
        const fader = document.createElement('span'); fader.className = 'fader';
        input.before(fader); fader.append(input);
        input.addEventListener('pointerdown', () => fader.classList.add('is-dragging'));
        const release = () => fader.classList.remove('is-dragging');
        input.addEventListener('pointerup', release); input.addEventListener('pointercancel', release);
        input.addEventListener('lostpointercapture', release); input.addEventListener('blur', release);
      });
      form.querySelectorAll('select').forEach(select => {
        const shell = document.createElement('div'); shell.className = 'selector';
        const button = document.createElement('button'); button.type = 'button'; button.className = 'selector-button'; button.id = select.id + '-button';
        const label = form.querySelector(`label[for="${select.id}"]`);
        label.id = select.id + '-label'; label.htmlFor = button.id;
        const value = document.createElement('span'); value.id = select.id + '-value';
        const mark = document.createElement('i'); mark.setAttribute('aria-hidden', 'true'); button.append(value, mark);
        const list = document.createElement('div'); list.id = select.id + '-options'; list.className = 'selector-options'; list.hidden = true;
        list.setAttribute('role', 'listbox'); list.setAttribute('aria-labelledby', label.id);
        button.setAttribute('role', 'combobox'); button.setAttribute('aria-haspopup', 'listbox'); button.setAttribute('aria-expanded', 'false');
        button.setAttribute('aria-controls', list.id); button.setAttribute('aria-labelledby', label.id + ' ' + value.id);
        if (select.hasAttribute('aria-describedby')) button.setAttribute('aria-describedby', select.getAttribute('aria-describedby'));
        select.before(shell); shell.append(button, list, select); select.hidden = true;
        const item = {select, shell, button, value, list, options: [], active: 0}; selectors.push(item);
        function highlight(index) {
          item.active = (index + item.options.length) % item.options.length;
          item.options.forEach((option, n) => option.classList.toggle('is-active', n === item.active));
          button.setAttribute('aria-activedescendant', item.options[item.active].id);
        }
        function expand() {
          if (button.disabled) return;
          close(); open = item; list.hidden = false; shell.classList.add('is-open'); button.setAttribute('aria-expanded', 'true');
          const bounds = button.getBoundingClientRect(), height = list.offsetHeight;
          shell.classList.toggle('open-up', bounds.bottom + height + 16 > innerHeight && bounds.top > height + 16);
          highlight(select.selectedIndex);
        }
        function choose(index) {
          select.selectedIndex = index; close();
          select.dispatchEvent(new Event('input', {bubbles: true}));
          button.focus({preventScroll: true});
        }
        [...select.options].forEach((option, index) => {
          const choice = document.createElement('button'); choice.type = 'button'; choice.tabIndex = -1; choice.className = 'selector-option';
          choice.id = select.id + '-option-' + index; choice.textContent = option.text; choice.setAttribute('role', 'option');
          choice.addEventListener('pointerenter', () => highlight(index));
          choice.addEventListener('click', () => choose(index)); list.append(choice); item.options.push(choice);
        });
        button.addEventListener('click', () => open === item ? close() : expand());
        button.addEventListener('keydown', event => {
          if (event.key === 'Escape' && open === item) { event.preventDefault(); event.stopPropagation(); close(); return; }
          if (event.key === 'Tab') { close(); return; }
          if (['Enter',' '].includes(event.key)) {
            event.preventDefault(); event.stopPropagation();
            if (open === item) choose(item.active); else expand(); return;
          }
          if (['ArrowDown','ArrowUp','Home','End'].includes(event.key)) {
            event.preventDefault(); event.stopPropagation();
            if (open !== item) { expand(); return; }
            highlight(event.key === 'Home' ? 0 : event.key === 'End' ? item.options.length - 1 : item.active + (event.key === 'ArrowDown' ? 1 : -1));
          }
        });
        shell.addEventListener('focusout', event => {if (!shell.contains(event.relatedTarget) && open === item) close();});
      });
      document.addEventListener('pointerdown', event => {if (open && !open.shell.contains(event.target)) close();});
      function sync() {
        selectors.forEach(item => {
          item.value.textContent = item.select.selectedOptions[0].textContent;
          item.button.disabled = item.select.disabled;
          if (item.button.disabled && open === item) close();
          item.options.forEach((option, index) => option.setAttribute('aria-selected', String(index === item.select.selectedIndex)));
        });
        form.querySelectorAll('input[type="range"]').forEach(input => {
          input.parentElement.style.setProperty('--position', ((Number(input.value) - Number(input.min)) / (Number(input.max) - Number(input.min)) * 100) + '%');
        });
      }
      return Object.freeze({sync, close});
    }
  });
})();
