#!/usr/bin/env bash
# Sorts the working tree into what can go in a commit and what must not, so a
# commit never starts from `git add -A`. Read-only: it stages nothing.
# Usage: triage.sh [--list]   --list prints every file instead of a per-folder count.

cd "$(git rev-parse --show-toplevel)" || exit 2
list_all=0
[ "$1" = "--list" ] && list_all=1

forbidden=(); noise=(); confirm=(); work=(); changed=()

while IFS= read -r -d '' entry; do
    status=${entry:0:2}
    file=${entry:3}
    # Renames carry their old path as an extra field.
    case "$status" in R*|C*) IFS= read -r -d '' _ ;; esac
    case "$status" in *D*) ;; *) changed+=("$file") ;; esac

    case "$file" in
        Assets/Scenes/BarSplitscreen.unity*|Assets/Plugins/LeartesStudios/*|*.csproj|*.slnx)
            forbidden+=("$status $file") ;;
        *.unitypackage.meta|Assets/_Recovery/*)
            noise+=("$status $file") ;;
        Assets/Plugins/*|"Assets/Wolff's Studio"*|ProjectSettings/*|Packages/*|*.unity|*.unity.meta)
            confirm+=("$status $file") ;;
        *)
            work+=("$status $file") ;;
    esac
done < <(git status --porcelain -z -uall)

print_group() {
    local title=$1; shift
    [ "$#" -eq 0 ] && return
    echo
    echo "== $title : $#"
    if [ "$list_all" -eq 1 ]; then
        printf '  %s\n' "$@"
    else
        # Per folder, three levels deep: the tree can hold hundreds of files.
        printf '%s\n' "$@" | cut -c4- | cut -d/ -f1-3 | sort | uniq -c | sort -rn | sed 's/^ */  /'
    fi
}

branch=$(git branch --show-current)
echo "Branche : ${branch:-HEAD detachee}"
[ "$branch" = "master" ] && echo "ATTENTION : sur master, creer une branche avant de committer."
if git rev-parse --verify -q master >/dev/null; then
    echo "Par rapport a master : $(git rev-list --count master..HEAD) commit(s) en avance, $(git rev-list --count HEAD..master) en retard."
fi

print_group "JAMAIS (BarSplitscreen, LeartesStudios, csproj)" "${forbidden[@]}"
print_group "BRUIT (.meta de .unitypackage sans leur paquet, scenes de secours Unity)" "${noise[@]}"
print_group "A CONFIRMER (plugins et packs, reglages du projet, paquets, scenes)" "${confirm[@]}"
print_group "A TRIER PAR TACHE" "${work[@]}"

echo
echo "== Verifications"
problems=0

# A Unity asset and its .meta travel together, or the GUID changes on the next machine.
for file in "${changed[@]}"; do
    case "$file" in
        Assets/*.meta)
            asset=${file%.meta}
            if [ ! -e "$asset" ]; then
                echo "  .meta sans son fichier : $file"; problems=$((problems + 1))
            fi ;;
        Assets/*)
            if [ -f "$file" ] && [ ! -e "$file.meta" ]; then
                echo "  fichier sans .meta : $file"; problems=$((problems + 1))
            fi ;;
    esac
    # GitHub refuses a file over 100 MB.
    if [ -f "$file" ] && [ "$(stat -c%s "$file")" -ge 100000000 ]; then
        echo "  plus de 100 Mo : $file"; problems=$((problems + 1))
    fi
done

# A changed asset using a PROTOFACTOR clip the .gitignore leaves out breaks on
# every other machine. Clips only named in a script are not caught here.
if [ -d Assets/PROTOFACTOR ]; then
    guids=$(mktemp)
    git ls-files -o -i --exclude-standard -z Assets/PROTOFACTOR \
        | grep -z '\.meta$' \
        | xargs -0 -r sed -n 's/^guid: //p' > "$guids"
    if [ -s "$guids" ]; then
        for file in "${changed[@]}"; do
            case "$file" in
                *.controller|*.overrideController|*.prefab|*.anim|*.asset|*.playable|*.unity)
                    if [ -f "$file" ] && grep -qFf "$guids" "$file"; then
                        echo "  utilise un clip PROTOFACTOR hors .gitignore : $file"; problems=$((problems + 1))
                    fi ;;
            esac
        done
    fi
    rm -f "$guids"
fi

[ "$problems" -eq 0 ] && echo "  rien a signaler"
exit 0
