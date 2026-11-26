# Chiffrage menuiserie

Outil de chiffrage pour un fabricant de menuiseries : le fabricant décrit son produit (paramètres,
grille de prix, règles), le publie, et le vendeur obtient le prix d'une configuration en direct,
expliqué ligne par ligne.

- **Moteur** (`src/Engine`, C#) : valide un produit et calcule le prix d'une configuration.
- **API** (`src/Api`, ASP.NET Minimal API) : stocke les brouillons et les versions publiées, expose
  le calcul.
- **Interface** (`web/`, React + TypeScript + Vite) : écran **Simulation** (vendeur) et écran
  **Configurer** (fabricant).

## Prérequis

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 22](https://nodejs.org/) ou plus récent (npm est fourni avec)

## Démarrage rapide

Dans deux terminaux, depuis la racine du dépôt :

```bash
# 1. L'API, sur http://localhost:5080
dotnet run --project src/Api
```

```bash
# 2. L'interface, sur http://localhost:5173
cd web
npm install      # la première fois seulement
npm run dev
```

Ouvrir http://localhost:5173 :

- `#simulation` : choisir une configuration et voir le prix, les options impossibles et pourquoi.
- `#configurer` : modifier les paramètres, les règles ou le JSON du produit, puis **Enregistrer** et
  **Publier**.

Au premier démarrage, l'API crée le dossier `data/` et y publie le produit d'exemple
`samples/product-model-v1.json` en version 1.

## Les tests

```bash
dotnet test
```

```bash
cd web
npm run build    # vérifie aussi les types TypeScript
npm run lint
```

## Essayer l'API sans l'interface

`src/Api/requests.http` contient une requête par route, avec la réponse attendue en commentaire.

- **VS Code** : installer l'extension *REST Client*, ouvrir le fichier, cliquer sur `Send Request`
  au-dessus d'une requête.
- **Rider** : rien à installer.

Exemple :

```bash
curl -X POST http://localhost:5080/products/fenetre-pvc-2v/evaluate \
  -H "Content-Type: application/json" \
  -d '{"couleur": "gris", "motorisation": true}'
```

| Route | Rôle |
|---|---|
| `GET /products` | liste des produits |
| `GET /products/{id}/draft` | lire le brouillon |
| `PUT /products/{id}/draft` | enregistrer le brouillon |
| `POST /products/{id}/publish` | publier : `200 { version }`, ou `422 { errors }` si le produit est refusé |
| `GET /products/{id}/published?version=` | lire une version publiée (la dernière par défaut) |
| `POST /products/{id}/evaluate?version=` | calculer le prix d'une configuration |

Une configuration invalide (largeur trop grande...) n'est pas une erreur HTTP : la réponse est un
`200` avec `valid: false` et la liste des erreurs.

## Calculer sans l'API

```bash
dotnet run --project tools/Sandbox                                           # scénarios de référence
dotnet run --project tools/Sandbox -- couleur=gris motorisation=true dormant=50
```

Prix de référence du produit d'exemple :

| Configuration | Résultat |
|---|---|
| par défaut | 419,00 €, valide |
| gris + motorisation | 865,28 €, valide, volet ajouté automatiquement |
| gris + motorisation + dormant 50 | 865,28 € mais **invalide** (dormant trop fin pour le volet) |
| largeur 2400 x hauteur 2100 | pas de prix, dimensions non fabricables |

## Configuration

| Réglage | Où | Défaut |
|---|---|---|
| Dossier des données | `DataDir` dans `src/Api/appsettings.json`, ou variable d'environnement `DataDir` | `data` (à la racine du dépôt) |
| Port de l'API | `src/Api/Properties/launchSettings.json` | `5080` |
| Adresse de l'API vue par l'interface | variable `VITE_API_URL` (fichier `web/.env.local`) | `http://localhost:5080` |

### Le dossier `data/`

```
data/
  fenetre-pvc-2v/
    draft.json   brouillon, réécrit à chaque Enregistrer
    v1.json      version publiée, jamais réécrite
    v2.json
```

- Il n'est pas versionné (`.gitignore`).
- L'API le lit **une seule fois au démarrage** et travaille ensuite en mémoire : ne pas modifier ces
  fichiers à la main pendant qu'elle tourne, passer par l'interface ou par `PUT .../draft`.
- Pour repartir de zéro : arrêter l'API, supprimer `data/`, relancer.

## Organisation du dépôt

```
src/Engine/      moteur : modèle produit, validateur, calcul, stockage (ProductStore)
src/Api/         API HTTP + requests.http
tests/Tests/     tests du moteur et du stockage (xUnit)
tools/Sandbox/   calcul en console, sans l'API
samples/         produit d'exemple, importé au premier démarrage
web/             interface React
  src/api.ts         appels à l'API et types partagés
  src/pages/         écrans Simulation et Configurer
  src/editor/        éditeur du produit (paramètres, règles, JSON)
  src/components/    composants de la charte graphique
```
