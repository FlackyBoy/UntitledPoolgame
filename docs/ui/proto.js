// Shared engine for the interactive UI prototypes.
//
// Page markup: a .stage-box > .stage containing <section class="scr" id="…"
// data-label="…" data-back="…">, with focusable items marked .f
// (data-go="screenId" to navigate, data-action="name" to call a handler).
// The page defines window.PROTO = { enter, focus, actions, beforeGo, events }
// before loading this file.
(function () {
  const P = window.PROTO || {};
  const box = document.querySelector('.stage-box');
  const stage = box.querySelector('.stage');
  const screens = [...stage.querySelectorAll('.scr')];
  let current = null, focused = null, busy = false;

  const items = () => (current ? [...current.querySelectorAll('.f')].filter(el => el.offsetParent !== null) : []);

  function setFocus(el) {
    if (!el || el === focused) return;
    if (focused) focused.classList.remove('focus');
    focused = el;
    el.classList.add('focus');
    P.focus && P.focus(el, current);
  }

  function replay(root) {
    root.querySelectorAll('.anim').forEach(el => { el.style.animation = 'none'; void el.offsetWidth; el.style.animation = ''; });
  }

  function show(id) {
    const next = stage.querySelector('#' + id);
    if (!next || next === current) return;
    if (current) { current.classList.remove('active'); P.leave && P.leave[current.id] && P.leave[current.id](current); }
    current = next;
    focused = null;
    next.classList.add('active');
    replay(next);
    next.querySelectorAll('.f.focus').forEach(el => el.classList.remove('focus'));
    setFocus(next.querySelector('.f.default') || items()[0]);
    bar.querySelectorAll('[data-jump]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.jump === id)));
    renderEvents();
    P.enter && P.enter[id] && P.enter[id](next);
    // "#/id", not "#id": a plain hash equal to the screen's id would make the
    // browser scroll the page to that element.
    try { history.replaceState(null, '', '#/' + id); } catch (e) {}
  }

  function go(id, el) {
    if (busy) return;
    if (P.beforeGo) {
      busy = true;
      P.beforeGo(el, current, () => { busy = false; show(id); });
    } else show(id);
  }

  function activate(el) {
    if (!el || busy) return;
    if (el.dataset.action && P.actions && P.actions[el.dataset.action]) P.actions[el.dataset.action](el, current, show);
    if (el.dataset.go) go(el.dataset.go, el);
  }

  function move(delta) {
    const list = items();
    if (!list.length) return;
    const i = list.indexOf(focused);
    setFocus(list[(i + delta + list.length) % list.length]);
  }

  // ---- Input ----
  document.addEventListener('keydown', e => {
    if (e.target.closest('input, textarea, select')) return;
    const k = e.key.toLowerCase();
    if (['arrowleft', 'arrowup', 'q', 'z', 'a', 'w'].includes(k)) { move(-1); e.preventDefault(); }
    else if (['arrowright', 'arrowdown', 'd', 's'].includes(k)) { move(1); e.preventDefault(); }
    else if (['enter', ' ', 'e'].includes(k)) { activate(focused); e.preventDefault(); }
    else if (['escape', 'backspace'].includes(k) && current && current.dataset.back) { show(current.dataset.back); e.preventDefault(); }
    else if (P.keys && P.keys[k]) { P.keys[k](current); e.preventDefault(); }
  });
  stage.addEventListener('mouseover', e => { const f = e.target.closest('.f'); if (f && current && current.contains(f)) setFocus(f); });
  stage.addEventListener('click', e => { const f = e.target.closest('.f'); if (f && current && current.contains(f)) { setFocus(f); activate(f); } });

  // ---- Demo bar ----
  const bar = document.createElement('div');
  bar.className = 'proto-bar';
  bar.innerHTML = `<div class="grp"><span class="lbl">Écran</span>${screens.map(s => `<button data-jump="${s.id}">${s.dataset.label || s.id}</button>`).join('')}</div>
    <div class="grp" id="proto-events"></div>
    <div class="grp"><button id="proto-full">Plein écran</button></div>`;
  box.after(bar);
  const keys = document.createElement('div');
  keys.className = 'proto-keys';
  keys.innerHTML = 'Naviguer : <kbd>←</kbd><kbd>→</kbd> ou <kbd>ZQSD</kbd> / souris · Valider : <kbd>Entrée</kbd> <kbd>E</kbd> ou clic · Retour : <kbd>Échap</kbd>';
  bar.after(keys);
  bar.addEventListener('click', e => {
    const j = e.target.closest('[data-jump]'); if (j) { busy = false; show(j.dataset.jump); }
    const ev = e.target.closest('[data-ev]'); if (ev && P.events && P.events[ev.dataset.ev]) P.events[ev.dataset.ev].run(current);
  });
  document.getElementById('proto-full').addEventListener('click', () => {
    if (document.fullscreenElement) document.exitFullscreen(); else box.requestFullscreen && box.requestFullscreen();
  });
  function renderEvents() {
    const evs = Object.entries(P.events || {}).filter(([, v]) => !v.on || v.on === (current && current.id));
    document.getElementById('proto-events').innerHTML = evs.length
      ? `<span class="lbl">Simuler</span>${evs.map(([k, v]) => `<button class="ev" data-ev="${k}">${v.label}</button>`).join('')}` : '';
  }

  window.Proto = { show, go, setFocus, get current() { return current; }, stage };
  // "#/game?run=pot,power" opens a screen and plays demo events one after the
  // other — to share a link to a given state, or to screenshot it headless.
  const [start, query] = location.hash.replace(/^#\/?/, '').split('?');
  show(screens.some(s => s.id === start) ? start : screens[0].id);
  const run = new URLSearchParams(query || '').get('run');
  if (run) run.split(',').forEach((ev, i) => setTimeout(() => P.events && P.events[ev] && P.events[ev].run(current), 300 + i * 900));
})();
