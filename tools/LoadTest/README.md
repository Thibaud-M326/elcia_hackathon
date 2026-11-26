# LoadTest : test de charge de l'API

Mesure le temps de réponse de `POST /products/{id}/evaluate`, la seule route appelée en boucle
(à chaque modification du formulaire de la Simulation). Les autres routes (liste, brouillon,
publication) sont rares et ne sont pas testées ici.

Chaque temps est découpé en trois parties, pour savoir où il passe :

| Colonne | Ce qu'elle mesure |
|---|---|
| **moteur** | `Evaluator.Evaluate` seul : le calcul du prix |
| **serveur** | tout le travail de l'API, de l'arrivée de la requête au début de la réponse (moteur compris) |
| **réseau** | total - serveur : transport, TLS, attente dans les files, lecture de la réponse |
| **total** | ce que voit le client, du départ prévu de la requête à la fin de la réponse |

## Démarrage rapide

Dans un premier terminal, lancer une API **en Release**, sur un **dossier de données à part**
(le test crée et publie un produit, voir plus bas) :

```bash
DataDir=/tmp/data-bench dotnet run -c Release --project src/Api --urls http://localhost:5099
```

Dans un second terminal :

```bash
# Débit constant : 5 000 requêtes par minute pendant 60 s
dotnet run -c Release --project tools/LoadTest -- --url http://localhost:5099 --rate 5000 --duration 60

# Débit maximal : combien de requêtes passent en 60 s
dotnet run -c Release --project tools/LoadTest -- --url http://localhost:5099 --max --concurrency 16,64 --duration 60
```

Sans `--url`, l'outil vise `http://localhost:5080` (le port habituel de l'API).

## Les options

| Option | Défaut | Rôle |
|---|---|---|
| `--url` | `http://localhost:5080` | adresse de l'API (locale ou en ligne) |
| `--rate` | `5000` | mode débit constant : requêtes par minute ; plusieurs paliers possibles, ex. `5000,20000,50000` |
| `--max` | (désactivé) | passe en mode débit maximal |
| `--concurrency` | `64` | mode débit maximal : nombre de clients en parallèle ; plusieurs valeurs possibles, ex. `16,64,256` |
| `--duration` | `60` | durée de chaque palier, en secondes |
| `--warmup` | `10` | chauffe avant chaque palier, en secondes, non comptée (`0` pour la désactiver) |
| `--product` | `bench-lourd` | produit chiffré ; `bench-lourd` est créé et publié automatiquement |

## Les deux modes

### Débit constant (par défaut) : "à 5 000 req/min, la latence tient-elle ?"

Les requêtes partent à intervalle fixe (toutes les 12 ms pour 5 000 req/min), **sans attendre les
réponses précédentes**. La latence est comptée depuis l'heure **prévue** du départ.

Pourquoi : avec une boucle "j'envoie, j'attends, j'envoie", un serveur qui ralentit recevrait moins
de requêtes, et le test réussirait en le soulageant. Ici, les requêtes continuent d'arriver au même
rythme, et un ralentissement se voit dans les chiffres.

5 000 req/min correspondent à environ 83 req/s, soit à peu près 80 vendeurs qui tapent en même
temps (le formulaire envoie au plus une requête toutes les 150 ms).

### Débit maximal (`--max`) : "combien de requêtes l'API traite-t-elle ?"

N clients en parallèle envoient chacun une requête, attendent la réponse, puis recommencent
aussitôt, pendant `--duration` secondes. On regarde surtout le **débit réel** obtenu.

La latence monte forcément : le serveur est saturé et les requêtes font la queue (c'est la colonne
"réseau" qui grossit, alors qu'il s'agit d'attente). Comparer plusieurs `--concurrency` montre où
commence la saturation : quand le débit ne monte presque plus mais que la latence explose.

## Lire le rapport

```
=== 5000 req/min pendant 60 s ===
Envoyées 5000   réussies 5000   erreurs 0   débit réel 83,3 req/s (5 000 req/min)
                 p50       p95       p99       max
moteur         0,137     0,246     0,336    19,430
serveur        1,222     1,811     2,107    50,668
réseau         0,628     0,873     1,049    38,361
total          1,867     2,650     3,141    52,078
(en millisecondes ; total = du départ prévu de la requête à la fin de la réponse)
```

- **Envoyées / réussies / erreurs** : une erreur est un code HTTP d'erreur, une coupure ou un délai
  dépassé (60 s). Les 5 raisons les plus fréquentes sont listées.
- **Débit réel** : réussites par seconde. En mode débit constant, il doit être égal à l'objectif ;
  s'il est plus bas, le client ou le serveur n'a pas suivi.
- **p50, p95, p99, max** : percentiles. On trie toutes les durées et on lit :
  - **p50** (médiane) : la moitié des requêtes sont plus rapides. C'est l'expérience habituelle.
  - **p95** : 95 % sont plus rapides, 5 % plus lentes.
  - **p99** : 99 % sont plus rapides. C'est l'expérience des vendeurs les moins chanceux.
  - **max** : la plus lente. Souvent une pause ponctuelle de .NET (ramasse-miettes), à ne pas
    surinterpréter.

  On n'utilise pas la moyenne : quelques requêtes très lentes la faussent, et elle ne décrit ni le
  cas habituel ni le pire.
- **Des tirets** (`-`) dans moteur / serveur / réseau : les réponses n'avaient pas l'en-tête
  `Server-Timing`. L'API visée est une ancienne version : la redémarrer après avoir récupéré le code.

## Comment ça marche

### Côté API : l'en-tête `Server-Timing`

`src/Api/Program.cs` ajoute à chaque réponse de `evaluate` :

```
Server-Timing: engine;dur=0.137, server;dur=1.222
```

- `engine` : mesuré dans la route, autour de `Evaluator.Evaluate`, puis rangé dans
  `HttpContext.Items`.
- `server` : mesuré par un middleware, de l'arrivée de la requête jusqu'à `Response.OnStarting`
  (juste avant l'envoi des en-têtes).

C'est un en-tête standard : les navigateurs l'affichent aussi dans l'onglet Réseau des outils de
développement, rubrique Timing.

### Côté outil

1. Un seul `HttpClient` (jusqu'à 512 connexions), qui réutilise les connexions comme un navigateur.
2. Création du produit `bench-lourd`, vérifié par le vrai `Validator` avant d'être envoyé
   (`PUT .../draft` puis `POST .../publish`).
3. 2 000 configurations aléatoires générées à l'avance, avec une graine fixe : chaque exécution envoie
   les mêmes, et la génération n'est pas mesurée.
4. Une première requête de "réveil" : relance un hébergement endormi avant de mesurer.
5. Chauffe (`--warmup`), non comptée : laisse .NET compiler le code et remplir ses caches.
6. Mesure, puis rapport.

### Le produit `bench-lourd` (`HeavyProduct.cs`)

Volontairement lourd, pour que chaque requête fasse tout le travail du moteur :

- **62 paramètres** : largeur, hauteur, 20 listes de 10 choix, 40 options (prix fixe, pourcentage du
  prix de base ou prix par unité) ;
- **108 règles** : 40 disponibilités à 2 conditions, 18 implications en deux chaînes de 10 (cocher
  l'option 1 ajoute les options 2 à 10), 10 incompatibilités, 40 plus-values à 2 conditions ;
- **grille de prix 20 x 20**.

Chaque configuration remplit toutes les saisies et coche environ la moitié des options. Une partie
est volontairement invalide (options incompatibles, options indisponibles) : les vérifications font
aussi partie du coût.

Le produit reste publiable : les implications portent sur les options 1 à 20, les incompatibilités
sur les options 21 à 40, donc aucune option n'en entraîne deux incompatibles.

## Précautions

- **Le test écrit dans l'API visée** : il crée `bench-lourd` et publie une **nouvelle version à chaque
  exécution**. Sur l'API habituelle, le produit apparaît dans l'écran Produits et les versions
  s'empilent dans `data/`. D'où le `DataDir=/tmp/data-bench` du démarrage rapide.
- **Mesurer en Release.** En Debug, le code n'est pas optimisé : les temps sont bien plus longs et
  ne veulent rien dire.
- **Redémarrer l'API après un changement de code** : elle garde la version compilée à son lancement.
- **Même machine** : l'outil et l'API se partagent le processeur. Le débit maximal mesuré en local
  est donc une estimation basse.
- **En local, "réseau" ne mesure presque rien** (aller-retour sur la même machine). Sur Internet, il
  faut compter 20 à 100 ms de plus, et c'est lui qui domine.
- **Hébergement gratuit (Render...)** : peu de processeur, et mise en veille après une période sans
  requête. La première requête peut prendre 30 s ou plus (d'où le réveil et la chauffe). Le débit
  maximal y sera bien plus bas qu'en local.
- `--product` avec un autre produit (ex. `fenetre-pvc-1v`) : il n'est pas créé, mais les
  configurations aléatoires sont faites pour `bench-lourd` et seraient refusées (paramètres
  inconnus). Pour l'instant, garder `bench-lourd`.

## Résultats de référence

Mesurés sur un portable (12 coeurs, WSL), API en Release sur la même machine, produit `bench-lourd`.

**Débit constant**

| Débit | Requêtes | Erreurs | Moteur p50 | Serveur p50 | Total p50 | Total p95 | Total p99 |
|---|---|---|---|---|---|---|---|
| 5 000 req/min (objectif) | 5 000 | 0 | 0,14 ms | 1,2 ms | 1,9 ms | 2,7 ms | 3,1 ms |
| 20 000 req/min | 10 000 | 0 | 0,13 ms | 1,1 ms | 1,6 ms | 2,3 ms | 4,8 ms |
| 50 000 req/min | 25 000 | 0 | 0,17 ms | 1,4 ms | 1,8 ms | 2,4 ms | 10,8 ms |

**Débit maximal (60 s)**

| Clients | Requêtes en 60 s | Débit | Erreurs | Total p50 | Total p95 | Total p99 |
|---|---|---|---|---|---|---|
| 16 | 159 395 | 2 656 req/s | 0 | 4,9 ms | 12,5 ms | 23,6 ms |
| 64 | 194 065 | 3 233 req/s | 0 | 18,1 ms | 35,1 ms | 49,2 ms |

Ce qu'il faut en retenir :

- l'objectif de 5 000 req/min est tenu largement ; la capacité est d'environ 39 fois l'objectif ;
- le moteur ne pèse presque rien (0,14 ms) ; le temps serveur est 8 à 9 fois plus grand. Cause
  probable, à mesurer : `ProductStore.Get` copie le produit entier en passant par JSON à chaque
  requête. Piste d'amélioration : chiffrer directement sur le produit en cache, après avoir vérifié
  que `Evaluate` ne le modifie jamais.
