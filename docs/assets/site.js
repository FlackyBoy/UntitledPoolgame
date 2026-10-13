// Shared plumbing for the project site (GitHub Pages, docs/ folder).
// TODO.md and CHANGELOG.md live at the repo root, outside docs/, so they're
// read straight from GitHub: the site always shows whatever is pushed on
// master, with nothing copied by hand. Pages' own content (docs/content/*.md)
// is read relatively.
(function () {
  const RAW = 'https://raw.githubusercontent.com/FlackyBoy/UntitledPoolgame/master/';

  // One entry per area of the site; each area page lists its own sections in
  // a side menu (renderDocPage), so the top bar stays short.
  const PAGES = [
    { key: 'home', href: 'index.html', label: 'Accueil' },
    { key: 'suivi', href: 'suivi.html', label: 'Suivi' },
    { key: 'design', href: 'gdd.html', label: 'Game design' },
    { key: 'config', href: 'config.html', label: 'Réglages' },
    { key: 'outils', href: 'outils.html', label: 'Outils' },
    { key: 'tech', href: 'tech.html', label: 'Technique' },
    { key: 'equipe', href: 'equipe.html', label: 'Process Git' },
  ];

  // base: path prefix back to docs/ for pages in a subfolder (e.g. '../').
  function renderHeader(active, base = '') {
    const header = document.createElement('header');
    header.className = 'site-header';
    const links = PAGES.map(p => p.soon
      ? `<span class="soon" title="Page à venir">${p.label}<small>bientôt</small></span>`
      : `<a href="${base}${p.href}"${p.key === active ? ' aria-current="page"' : ''}>${p.label}</a>`).join('');
    header.innerHTML = `<div class="bar"><a class="brand" href="${base}index.html">Untitled<span>Pool</span>Game</a><nav class="nav">${links}</nav></div>`;
    document.body.prepend(header);
  }

  async function fetchText(url) {
    const res = await fetch(url, { cache: 'no-cache' });
    if (!res.ok) throw new Error(`${res.status} sur ${url}`);
    return res.text();
  }

  function fetchRoot(file) { return fetchText(RAW + file); }

  function slugify(text) {
    return text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '')
      .replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
  }

  function toHtml(md) { return DOMPurify.sanitize(marked.parse(md)); }
  function inlineHtml(md) { return DOMPurify.sanitize(marked.parseInline(md)); }

  let mermaidReady = false;
  async function renderMermaid(root) {
    const blocks = root.querySelectorAll('pre > code.language-mermaid');
    if (!blocks.length || !window.mermaid) return;
    blocks.forEach(code => {
      const div = document.createElement('div');
      div.className = 'mermaid';
      div.textContent = code.textContent;
      code.parentElement.replaceWith(div);
    });
    if (!mermaidReady) {
      const dark = document.documentElement.dataset.theme === 'dark' ||
        (document.documentElement.dataset.theme !== 'light' && matchMedia('(prefers-color-scheme: dark)').matches);
      mermaid.initialize({ startOnLoad: false, theme: dark ? 'dark' : 'neutral', securityLevel: 'strict' });
      mermaidReady = true;
    }
    await mermaid.run({ nodes: root.querySelectorAll('.mermaid') });
  }

  // Heading ids from their text; a repeated title ("Rythme" under two
  // components) gets -2, -3… so each anchor lands on its own heading.
  function assignIds(root) {
    const seen = {};
    root.querySelectorAll('h2, h3, h4').forEach(h => {
      if (h.id) return;
      const base = slugify(h.textContent);
      seen[base] = (seen[base] || 0) + 1;
      h.id = seen[base] > 1 ? `${base}-${seen[base]}` : base;
    });
  }

  // Renders Markdown into el, gives headings ids, optionally fills a TOC.
  async function renderMarkdown(md, el, tocEl) {
    el.innerHTML = toHtml(md);
    assignIds(el);
    if (tocEl) {
      const items = [...el.querySelectorAll('h2')];
      tocEl.innerHTML = items.length
        ? '<b>Sommaire</b>' + items.map(h => `<a href="#${h.id}">${h.textContent}</a>`).join('')
        : '';
    }
    await renderMermaid(el);
  }

  function showError(el, err) {
    const local = location.protocol === 'file:';
    el.innerHTML = `<p class="notice">Contenu indisponible (${err.message}).` +
      (local ? ' Ouvert en local : les navigateurs bloquent ce chargement hors serveur — ouvrir la version GitHub Pages.' : '') + '</p>';
  }

  // --- TODO.md parser ------------------------------------------------------
  // Keeps the "##" sections (minus "Notes techniques"), and every "- " bullet
  // nested by indentation. Status comes from the leading emoji (⬜ 🔄 ✅) or
  // checkbox; bullets without one are plain notes of their parent.
  const STATUS = [
    ['⬜', 'todo'], ['🔄', 'wip'], ['✅', 'done'], ['[ ]', 'todo'], ['[x]', 'done'], ['[X]', 'done'],
  ];
  const SKIPPED_SECTIONS = /^notes techniques/i;

  function parseTodo(md) {
    const sections = [];
    let section = null, skipping = true;
    let stack = [];
    for (const line of md.split(/\r?\n/)) {
      const h = /^##\s+(.*)$/.exec(line);
      if (h) {
        skipping = SKIPPED_SECTIONS.test(h[1].trim());
        section = skipping ? null : { title: h[1].trim(), items: [] };
        if (section) sections.push(section);
        stack = [];
        continue;
      }
      if (skipping || !section) continue;
      const b = /^(\s*)- (.*)$/.exec(line);
      if (!b) continue;
      const indent = b[1].replace(/\t/g, '  ').length;
      let text = b[2].trim(), status = null;
      for (const [mark, s] of STATUS) {
        if (text.startsWith(mark)) { status = s; text = text.slice(mark.length).trim(); break; }
      }
      const node = { status, text, children: [], indent };
      while (stack.length && stack[stack.length - 1].indent >= indent) stack.pop();
      const parent = stack[stack.length - 1];
      (parent ? parent.children : section.items).push(node);
      stack.push(node);
    }
    return sections;
  }

  function countStatuses(sections) {
    const c = { todo: 0, wip: 0, done: 0 };
    const walk = items => items.forEach(i => { if (i.status) c[i.status]++; walk(i.children); });
    sections.forEach(s => walk(s.items));
    return c;
  }

  function splitTitle(text) {
    const bold = /^\*\*(.+?)\*\*\s*[:—–-]?\s*(.*)$/s.exec(text);
    if (bold) return [bold[1], bold[2]];
    const dash = text.indexOf(' — ');
    if (dash > 0 && dash < 160) return [text.slice(0, dash), text.slice(dash + 3)];
    return [text, ''];
  }

  const LABEL = { todo: 'À faire', wip: 'En cours', done: 'Fait' };

  function renderTodoItems(items) {
    const withStatus = items.filter(i => i.status);
    const notes = items.filter(i => !i.status);
    let html = '';
    if (withStatus.length) {
      html += '<ul class="todo-list">' + withStatus.map(i => {
        const [title, body] = splitTitle(i.text);
        return `<li class="todo-item s-${i.status}" data-status="${i.status}">` +
          `<div class="row"><span class="badge">${LABEL[i.status]}</span><div class="text">` +
          `<div class="title">${inlineHtml(title)}</div>` +
          (body ? `<details><summary>Détails</summary>${inlineHtml(body)}</details>` : '') +
          '</div></div>' + renderTodoItems(i.children) + '</li>';
      }).join('') + '</ul>';
    }
    if (notes.length) {
      html += '<ul class="notes">' + notes.map(n => `<li>${inlineHtml(n.text)}${renderTodoItems(n.children)}</li>`).join('') + '</ul>';
    }
    return html;
  }

  function renderTodo(sections, el) {
    const c = countStatuses(sections);
    el.innerHTML =
      `<div class="toolbar" role="group" aria-label="Filtrer">
         <button data-filter="all" aria-pressed="true">Tout</button>
         <button data-filter="todo" aria-pressed="false">À faire</button>
         <button data-filter="wip" aria-pressed="false">En cours</button>
         <button data-filter="done" aria-pressed="false">Fait</button>
         <input type="search" placeholder="Rechercher…" aria-label="Rechercher dans la TODO">
         <span class="counts">${c.todo} à faire · ${c.wip} en cours · ${c.done} faits — lu depuis <a href="https://github.com/FlackyBoy/UntitledPoolgame/blob/master/TODO.md">TODO.md</a> (notes techniques exclues)</span>
       </div>` +
      sections.filter(s => s.items.length).map(s =>
        `<section class="todo-section"><h2>${inlineHtml(s.title)}</h2>${renderTodoItems(s.items)}</section>`).join('');

    let filter = 'all', query = '';
    const apply = () => {
      const items = [...el.querySelectorAll('.todo-item')].reverse(); // children before parents
      items.forEach(li => {
        const own = (filter === 'all' || li.dataset.status === filter) &&
          (!query || li.querySelector(':scope > .row').textContent.toLowerCase().includes(query));
        const childVisible = li.querySelector(':scope > .todo-list > .todo-item:not(.hidden)') !== null;
        li.classList.toggle('hidden', !(own || childVisible));
      });
      el.querySelectorAll('.todo-section').forEach(s =>
        s.classList.toggle('hidden', !s.querySelector('.todo-item:not(.hidden)')));
    };
    el.querySelectorAll('.toolbar button').forEach(b => b.addEventListener('click', () => {
      filter = b.dataset.filter;
      el.querySelectorAll('.toolbar button').forEach(x => x.setAttribute('aria-pressed', String(x === b)));
      apply();
    }));
    el.querySelector('.toolbar input').addEventListener('input', e => { query = e.target.value.trim().toLowerCase(); apply(); });
  }

  // First "## " entry of CHANGELOG.md.
  function latestChangelogEntry(md) {
    const parts = md.split(/^## /m);
    return parts.length > 1 ? '## ' + parts[1] : md;
  }

  // --- Area pages ----------------------------------------------------------
  // An area page = side menu + one section shown at a time, chosen by the
  // hash. A section is { id, label } plus one of:
  //   file: 'content/x.md'  (relative), root: 'X.md' (repo root on master),
  //   render: async el => …  (custom content, e.g. the TODO),
  //   href: 'page.html'      (menu link to another page).
  // { group: 'Titre' } entries are headings in the menu. moved maps old hashes
  // to their new URL so links from before the reorganisation keep working.
  // subnav: every section's h2/h3/h4 are listed in the side menu (instead of
  // the right-hand TOC), for long reference pages.
  function renderDocPage({ sections, moved = {}, subnav = false }) {
    const pages = sections.filter(s => s.id);
    const nav = document.getElementById('doc-nav');
    const body = document.getElementById('doc-body');
    const toc = document.getElementById('doc-toc');

    nav.innerHTML =
      sections.map(s => s.group ? `<div class="group">${s.group}</div>`
        : s.href ? `<a href="${s.href}">${s.label} <span aria-hidden="true">↗</span></a>`
        : `<a href="#${s.id}" data-id="${s.id}">${s.label}</a>`).join('') +
      `<select aria-label="Section">${sections.filter(s => s.id || s.href).map(s =>
        `<option value="${s.id || s.href}">${s.label}</option>`).join('')}</select>`;
    nav.querySelector('select').addEventListener('change', e => {
      const v = e.target.value;
      if (pages.some(s => s.id === v)) location.hash = v; else location.href = v;
    });

    const cache = {};
    const load = section => cache[section.id] ??= section.root ? fetchRoot(section.root) : fetchText(section.file);

    let shown = null;
    async function show(section, headingId) {
      if (shown !== section.id) {
        shown = section.id;
        nav.querySelectorAll('a[data-id]').forEach(a => a.setAttribute('aria-current', String(a.dataset.id === section.id)));
        nav.querySelector('select').value = section.id;
        body.innerHTML = '<p class="loading">Chargement…</p>';
        body.classList.toggle('prose', !section.render); // the TODO has its own styles
        toc.innerHTML = '';
        try {
          if (section.render) await section.render(body);
          else await renderMarkdown(await load(section), body, subnav ? null : toc);
        } catch (err) {
          showError(body, err);
        }
      }
      const target = headingId && document.getElementById(headingId);
      if (target) target.scrollIntoView(); else window.scrollTo(0, 0);
    }

    // Every section's h2/h3/h4, listed under it in the side menu, whichever
    // section is open: a link is "#section:heading" so it can open another
    // section and scroll to the heading in one click.
    async function buildSubnav() {
      for (const section of pages) {
        if (section.render) continue;
        const scratch = document.createElement('div');
        try { scratch.innerHTML = toHtml(await load(section)); } catch { continue; }
        assignIds(scratch);
        const heads = [...scratch.querySelectorAll('h2, h3, h4')];
        if (!heads.length) continue;
        const list = document.createElement('div');
        list.className = 'subnav';
        list.innerHTML = heads.map(h =>
          `<a href="#${section.id}:${h.id}" class="${h.tagName.toLowerCase()}">${h.textContent}</a>`).join('');
        nav.querySelector(`a[data-id="${section.id}"]`).after(list);
      }
    }

    // The section is the hash itself (or "section:heading" from the subnav);
    // any other anchor is a heading of the open section (from the TOC), which
    // the browser scrolls to on its own.
    function route() {
      const hash = location.hash.slice(1);
      if (moved[hash]) { location.replace(moved[hash]); return; }
      const [id, headingId] = hash.split(':');
      const section = pages.find(s => s.id === id);
      if (section) show(section, headingId);
      else if (!shown) show(pages[0]);
    }
    addEventListener('hashchange', route);
    // Clicking the link already in the hash fires no hashchange: scroll anyway.
    nav.addEventListener('click', e => {
      const a = e.target.closest('a[href^="#"]');
      if (a && a.getAttribute('href') === location.hash) route();
    });
    route();
    if (subnav) buildSubnav();
  }

  function renderTodoInto(el) {
    return fetchRoot('TODO.md').then(md => renderTodo(parseTodo(md), el));
  }

  window.Site = { renderHeader, fetchText, fetchRoot, renderMarkdown, showError, parseTodo, countStatuses, renderTodo, renderTodoInto, latestChangelogEntry, renderDocPage };
})();
