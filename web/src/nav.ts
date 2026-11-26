// Navigation par le hash de l'URL, sans bibliothèque de routage :
//   #produits                 liste des produits (page d'accueil)
//   #simulation/{id}          simulation d'un produit
//   #configurer/{id}          configuration d'un produit
import { useEffect, useState } from 'react';
import type { NavItem } from '@/components/AppLayout';

export type Screen = 'produits' | 'simulation' | 'configurer';

export interface Route {
	screen: Screen;
	productId: string | null;
}

// Un hash inconnu, ou un écran produit sans id (ancien lien #simulation), mène à la liste
export function parseHash(hash: string): Route {
	const [screen, ...rest] = hash.replace(/^#/, '').split('/');
	const id = rest.join('/');
	if ((screen === 'simulation' || screen === 'configurer') && id) {
		try {
			return { screen, productId: decodeURIComponent(id) };
		} catch {
			return { screen: 'produits', productId: null };
		}
	}
	return { screen: 'produits', productId: null };
}

export function hrefFor(screen: Screen, productId?: string | null): string {
	return screen === 'produits' || !productId ? '#produits' : `#${screen}/${encodeURIComponent(productId)}`;
}

export function useRoute(): Route {
	const [route, setRoute] = useState<Route>(() => parseHash(window.location.hash));

	useEffect(() => {
		const onChange = () => setRoute(parseHash(window.location.hash));
		window.addEventListener('hashchange', onChange);
		return () => window.removeEventListener('hashchange', onChange);
	}, []);

	// Remet l'URL au propre (#simulation sans id -> #produits) sans ajouter d'entrée d'historique
	useEffect(() => {
		const canonical = hrefFor(route.screen, route.productId);
		if (window.location.hash !== canonical) history.replaceState(null, '', canonical);
	}, [route]);

	return route;
}

// Menu de gauche : Produits, puis Simulation et Configurer pour le produit ouvert
export function navFor(current: Screen, productId?: string | null): NavItem[] {
	const items: NavItem[] = [{ label: 'Produits', href: hrefFor('produits'), active: current === 'produits' }];
	if (productId) {
		items.push({ label: 'Simulation', href: hrefFor('simulation', productId), active: current === 'simulation' });
		items.push({ label: 'Configurer', href: hrefFor('configurer', productId), active: current === 'configurer' });
	}
	return items;
}
