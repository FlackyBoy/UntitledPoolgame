// Shared plumbing for the project site (GitHub Pages, docs/ folder).
// TODO.md and CHANGELOG.md live at the repo root, outside docs/, so they're
// read straight from GitHub: the site always shows whatever is pushed on
// master, with nothing copied by hand. Pages' own content (docs/content/*.md)
// is read relatively.
(function () {
  const RAW = 'https://raw.githubusercontent.com/FlackyBoy/UntitledPoolgame/master/';

  const PAGES = [
    { key: 'home', href: 'index.html', label: 'Accueil' },
    { key: 'gdd', href: 'gdd.html', label: 'TODO & GDD' },
    { key: 'tech', href: 'tech.html', label: 'Doc technique' },
    { key: 'ui', href: 'ui.html', label: 'Propositions UI' },
    { key: 'level', label: 'Outil de niveau', soon: true },
  ];

  function renderHeader(active) {
    const header = document.createElement('header');
    header.className = 'site-header';
    const links = PAGES.map(p => p.soon
      ? `<span class="soon" title="Page à venir">${p.label}<small>bientôt</small></span>`
      : `<a href="${p.href}"${p.key === active ? ' aria-current="page"' : ''}>${p.label}</a>`).join('');
    header.innerHTML = `<div class="bar"><a class="brand" href="index.html">Untitled<span>Pool</span>Game</a><nav class="nav">${links}</nav></div>`;
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

  // Renders Markdown into el, gives headings ids, optionally fills a TOC.
  async function renderMarkdown(md, el, tocEl) {
    el.innerHTML = toHtml(md);
    const headings = el.querySelectorAll('h2, h3');
    headings.forEach(h => { if (!h.id) h.id = slugify(h.textContent); });
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

  window.Site = { renderHeader, fetchText, fetchRoot, renderMarkdown, showError, parseTodo, countStatuses, renderTodo, latestChangelogEntry };
})();
