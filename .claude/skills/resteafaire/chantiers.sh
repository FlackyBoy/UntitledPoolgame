#!/usr/bin/env bash
# Lists what is still open in TODO.md (⬜ to do, 🔄 in progress), tagged by
# category, so /resteafaire reads a short extract instead of the whole TODO.
# Read-only. Keyword tagging is only a first sort: the skill re-reads the lines.
# Usage: chantiers.sh            -> counts per chantier, then per pistes section
#        chantiers.sh <category> -> open items of that category, with line numbers
#        chantiers.sh tout       -> open items of every category
#        chantiers.sh pistes     -> ideas from pistes.md (not decided), per section
#        chantiers.sh --categories -> category names only

cd "$(git rev-parse --show-toplevel)" || exit 2
cat_arg="${1:-}"

if [ "$cat_arg" != "pistes" ]; then
LC_ALL=C awk -v want="$cat_arg" '
BEGIN {
    # Order = display order. A line can land in several categories.
    n = 0
    names[++n] = "modes";       re["modes"]       = "mode |modes|speed pool|snake|power wash|catch the hole|hungry hole|pocket size|play anywhere|shooter|type de partie|9-ball|14[.]1|boogie"
    names[++n] = "pouvoirs";    re["pouvoirs"]    = "pouvoir|caisse|jeton|powerball"
    names[++n] = "billard";     re["billard"]     = "vis[^ ]*e |tir |distance de jeu|craie|r..?gles de billard|blanche|bille.? |rack|poche"
    names[++n] = "joueur";      re["joueur"]      = "animation|buste|bend goal|prise de la queue|queue debout|queues libres|cueholder|objets tenus|objets ramassables|pr..?hension|ragdoll|manette|seul ..? la manette|movementsettings|locomotion"
    names[++n] = "camera";      re["camera"]      = "cam..?ra|placementtrace"
    names[++n] = "combat";      re["combat"]      = "combat|bagarre|poing|pied|frapp|pnj|jauge|encaissement|attraper ..? porter|coup re..?u"
    names[++n] = "menus";       re["menus"]       = "menu|hud|interface|ui |ui$|textfx|text fx|borne|arcade|sensibilit|sauvegarde"
    names[++n] = "game-feel";   re["game-feel"]   = "game feel|juice|feel |combo|halo|secousse|flash"
    names[++n] = "level-maker"; re["level-maker"] = "level maker|cr..?ateur de niveau|garde-corps|tr..?mie|splines|g..?n..?ration proc|kit d..?finitif|haunted|étage|Étage|escalier|sol en double"
    names[++n] = "dimensions";  re["dimensions"]  = "dimension|environnement|loufoque|prison|station"
    names[++n] = "online";      re["online"]      = "en ligne|online|r..?seau|relay|lobby|netcode"
    names[++n] = "equipe";      re["equipe"]      = "notion|discord|skill|travailler ..? deux|coll..?gue|co..?quipier|prot..?ger `master`|collaborators|d..?p..?t public|sandbox"
}
function strip(s) {
    gsub(/<!--[^>]*-->/, "", s)
    if (match(s, /\*\*[^*]+\*\*/)) t = substr(s, RSTART + 2, RLENGTH - 4)
    else { t = s; sub(/^[ \t]*- */, "", t) }
    sub(/^(\xe2\xac\x9c|\xf0\x9f\x94\x84) */, "", t)
    return substr(t, 1, 110)
}
/^## / {
    section = substr($0, 4)
    skip = (section ~ /^(Notes techniques|Fait)/)
    next
}
skip { next }
/^[ \t]*- / {
    indent = match($0, /[^ \t]/) - 1
    low = tolower(strip($0))
    open = (index($0, "\xe2\xac\x9c") || index($0, "\xf0\x9f\x94\x84"))
    status = index($0, "\xf0\x9f\x94\x84") ? "en cours" : "a faire"
    if (section ~ /^À tester|^\xc3\x80 tester/) status = "a tester"
    if (index($0, "\xe2\x9c\x85")) open = 0
    tags = ""
    for (i = 1; i <= n; i++) if (low ~ re[names[i]]) tags = tags " " names[i]
    if (indent == 0) { parent_tags = tags; parent = strip($0) }
    else tags = parent_tags tags
    if (!open) next
    if (tags == "") tags = " autre"
    kind = (indent == 0) ? "" : "  (sous-tache de : " parent ")"
    delete seen; split(tags, tl, " ")
    for (k in tl) if (tl[k] != "" && !seen[tl[k]]++) {
        c = tl[k]; count[c]++
        rows[c] = rows[c] sprintf("  L%-5d [%s] %s%s\n", NR, status, strip($0), (want != "" ? kind : ""))
    }
}
END {
    names[++n] = "autre"
    if (want == "--categories") { for (i = 1; i <= n; i++) print names[i]; exit }
    for (i = 1; i <= n; i++) {
        c = names[i]
        if (want != "" && want != "tout" && c != want) continue
        if (c == "autre" && !count[c]) continue
        printf "== %s : %d\n", c, count[c] + 0
        if (want != "") printf "%s", rows[c]
    }
}' TODO.md
fi

# Pistes: ideas not decided yet. Shown with the overview, or alone with "pistes".
if [ -z "$cat_arg" ] || [ "$cat_arg" = "pistes" ]; then
    LC_ALL=C awk -v detail="$cat_arg" '
    /^## / { part = ($0 ~ /Suggestions de Claude/) ? "suggestion de Claude" : "piste"; next }
    /^### / {
        h = substr($0, 5); sub(/ *\(.*\) *$/, "", h)
        order[++n] = h; kind[h] = part; next
    }
    # Table rows count too (the header row and the |---| line do not).
    { was_table = in_table; in_table = /^\|/ }
    n && (/^[-*] |^[0-9]+\. / || (in_table && was_table && !/^\|[-: |]+\|?$/)) {
        cnt[order[n]]++
        if (detail != "") {
            t = $0; sub(/^([-*]|[0-9]+\.|\|) */, "", t); sub(/ *\|.*$/, "", t)
            if (match(t, /\*\*[^*]+\*\*/)) t = substr(t, RSTART + 2, RLENGTH - 4)
            rows[order[n]] = rows[order[n]] sprintf("  L%-5d %s\n", NR, substr(t, 1, 110))
        }
    }
    END {
        print "== pistes (rien n est decide)"
        for (i = 1; i <= n; i++) {
            printf "  %s [%s] : %d\n", order[i], kind[order[i]], cnt[order[i]] + 0
            if (detail != "") printf "%s", rows[order[i]]
        }
    }' docs/content/pistes.md
fi
