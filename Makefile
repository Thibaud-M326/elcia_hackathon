# Commandes du projet. Lancer `make` (ou `make help`) pour la liste.
#
# Variables modifiables à l'appel, ex. : make load-test RATE=20000 DURATION=30
#   URL          API visée par le test de charge        (défaut : http://localhost:5080)
#   RATE         requêtes par minute, débit constant    (défaut : 5000 ; paliers : 5000,20000)
#   CONCURRENCY  clients en parallèle, débit maximal    (défaut : 16,64)
#   DURATION     durée de chaque palier, en secondes    (défaut : 60)
#   ARGS         configuration pour `make sandbox`      (ex. : ARGS="couleur=gris motorisation=true")

URL         ?= http://localhost:5080
RATE        ?= 5000
CONCURRENCY ?= 16,64
DURATION    ?= 5
ARGS        ?=

# Données de l'API de test, séparées de data/ (le test de charge y publie des versions)
BENCH_DATA  := .bench/data

.DEFAULT_GOAL := help
.PHONY: help install dev api api-release web build test lint check \
        sandbox bench api-bench load-test load-test-max reset-data clean

help: ## Afficher cette aide
	@echo "Commandes disponibles :"
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "} {printf "  make %-15s %s\n", $$1, $$2}'

# ---------- Installation ----------

install: ## Installer les dépendances (.NET et front)
	dotnet restore
	cd web && npm ci

# ---------- Lancer l'application ----------

dev: ## Lancer l'API et le front ensemble (Ctrl+C arrête les deux)
	@$(MAKE) --no-print-directory -j2 api web

api: ## Lancer l'API sur http://localhost:5080
	dotnet run --project src/Api

api-release: ## Lancer l'API en Release (plus rapide, pour une démo)
	dotnet run -c Release --project src/Api

web: ## Lancer le front sur http://localhost:5173
	cd web && npm run dev

# ---------- Vérifier ----------

build: ## Compiler l'API, les outils et le front
	dotnet build
	cd web && npm run build

test: ## Lancer les tests du moteur et du stockage
	dotnet test

lint: ## Vérifier le front avec oxlint
	cd web && npm run lint

check: test lint ## Tout vérifier avant un push (tests, lint, build du front)
	cd web && npm run build

# ---------- Moteur et performances ----------

sandbox: ## Chiffrer sans l'API (ARGS="couleur=gris motorisation=true" pour une configuration)
	dotnet run --project tools/Sandbox -- $(ARGS)

bench: ## Mesurer le moteur seul (100 000 calculs, Release)
	dotnet run -c Release --project tools/Sandbox -- --benchmark

api-bench: ## Lancer une API de test en Release, port 5080, données dans .bench/ (arrêter make api avant)
	dotnet run -c Release --project src/Api --urls http://localhost:5080 -- --DataDir=$(BENCH_DATA)

load-test: ## Test de charge à débit constant (RATE, DURATION, URL) ; lancer api-bench avant
	dotnet run -c Release --project tools/LoadTest -- --url $(URL) --rate $(RATE) --duration $(DURATION)

load-test-max: ## Test de débit maximal (CONCURRENCY, DURATION, URL) ; lancer api-bench avant
	dotnet run -c Release --project tools/LoadTest -- --url $(URL) --max --concurrency $(CONCURRENCY) --duration $(DURATION)

# ---------- Nettoyer ----------

reset-data: ## Supprimer data/ (brouillons et versions) ; l'exemple est réimporté au prochain lancement
	@printf "Supprimer data/ et tous les produits enregistrés ? [o/N] " && read ok && [ "$$ok" = o ] && rm -rf data && echo "data/ supprimé." || echo "Annulé."

clean: ## Supprimer les fichiers compilés et les données de test (.bench/)
	dotnet clean -v q
	rm -rf web/dist .bench
