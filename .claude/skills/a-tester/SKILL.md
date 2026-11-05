---
name: a-tester
description: Prépare une courte session de test en jeu à partir de la TODO, lit les logs d'Unity après coup, puis met à jour la TODO selon le retour de l'utilisateur. À utiliser quand l'utilisateur demande /a-tester, « quoi tester », veut valider ce qui s'accumule, ou vient de tester et donne son retour.
---

# a-tester — vider la file « compilé, non testé »

Le code avance plus vite que les essais en jeu. Ce skill réduit l'effort de test : peu de points à la fois, regroupés pour limiter les changements de scène, et le maximum vérifié par les logs.

`/a-tester [mode ou sujet]` prépare une session. `/a-tester retour` traite ce que l'utilisateur vient de tester.

## Préparer une session

1. **Collecter** dans `TODO.md` : la section « À tester en jeu » et les lignes 🔄 de « En cours » qui disent « non testé ». Chaque ligne a normalement un **Comment tester**. Si l'utilisateur a nommé un sujet, s'y limiter.
2. **Choisir au plus 5 à 7 points**, dans cet ordre :
   - ce qui bloque autre chose ou une PR à merger ;
   - le plus récent, encore frais ;
   - les points qu'une même scène peut couvrir ;
   - les anciens chantiers à confirmer d'une phrase (« peut-être déjà validé »).
3. **Présenter** dans la conversation, directement utilisable :

   ```
   Mise en place : scène X, composant Lancer sans le menu, Mode = Y
   1. Faire ceci → on doit voir cela
   2. …
   Cheats utiles : C + X (pouvoirs), menu debug (clic gauche maintenu + 4 clics droits)
   Logs lus après coup : [Bowling], [Devices]
   ```

   Chaque point dit quoi faire et quoi observer. Préciser si deux manettes ou un second joueur sont nécessaires. Rappeler les outils à lancer avant (*Ensure Config Assets Exist*, *Générer les jetons de pouvoir*) si un fichier manque.
4. Si un tag de log utile n'est pas activé (*Debug Logs* dans le fichier de réglages), le dire avant le test.

## Traiter le retour

**Lire les logs avant de demander quoi que ce soit.** La console de l'éditeur ouvert s'écrit en direct dans `%LOCALAPPDATA%\Unity\Editor\Editor.log`, qui est énorme. Ne jamais l'ouvrir en entier :

```bash
LOG="$LOCALAPPDATA/Unity/Editor/Editor.log"
tail -c 20000000 "$LOG" | grep -a -E "\[Bowling\]|\[Devices\]" | tail -n 60
tail -c 20000000 "$LOG" | grep -a -E "Exception|Assertion failed" | sort | uniq -c | sort -rn | head -n 20
```

`Editor-prev.log` contient la session précédente, si Unity a été relancé. Les lignes ne sont pas horodatées : les derniers résultats peuvent venir d'une session plus ancienne, à recouper avec ce que dit l'utilisateur. Une exception du log n'accuse pas forcément le point testé : vérifier son origine avant de conclure.

Ne demander à l'utilisateur que ce qu'un log ne peut pas dire : ce qu'il a vu et ressenti.

## Mettre à jour le suivi

**Seul l'utilisateur valide.** Des logs propres sont un indice, jamais une validation.

| Retour | TODO | Notion |
|---|---|---|
| Validé | ✅ avec la date, retiré de « À tester en jeu » | `Fait` |
| Problème | Noter ce qui a été vu, reste 🔄 ou ⬜ | `En cours` ou `À faire` |
| Pas testé | Rien | Rien |
| Problème de ressenti ou réglage | Noter le retour ; proposer une valeur seulement avec un diagnostic | Inchangé |

- Passer aussi la ligne du CHANGELOG et le statut du GDD si la fonctionnalité change de statut.
- Notion est plein pour l'instant : les pages existantes se modifient encore, mais les lignes marquées « pas encore dans Notion » n'ont pas de page. Ne pas réessayer de les créer sans que l'utilisateur ait libéré de la place.
- Terminer par un résumé court : validés, à corriger, restants.

## Après un problème

Ne pas corriger à l'aveugle : observer d'abord (logs, code du plugin), changer une seule chose, ajouter un log si la cause reste floue, et retirer tout de suite ce que l'utilisateur demande de retirer. Chaque correctif redevient « compilé, non testé » et revient dans la file.
